using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services.Notifications;
using ShiftingGuru.Services.Storage;
using ShiftingGuru.Services.Verification;
using ShiftingGuru.ViewModels.Partner;

namespace ShiftingGuru.Services;

public class PartnerService : IPartnerService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _users;
    private readonly IServiceCatalog _catalog;
    private readonly INotificationService _notifications;
    private readonly IEmailVerificationService _emailVerification;
    private readonly IPhoneVerificationService _phoneVerification;
    private readonly IDocumentStorage _storage;
    private readonly ILogger<PartnerService> _logger;

    public PartnerService(
        ApplicationDbContext db,
        UserManager<IdentityUser> users,
        IServiceCatalog catalog,
        INotificationService notifications,
        IEmailVerificationService emailVerification,
        IPhoneVerificationService phoneVerification,
        IDocumentStorage storage,
        ILogger<PartnerService> logger)
    {
        _db = db;
        _users = users;
        _catalog = catalog;
        _notifications = notifications;
        _emailVerification = emailVerification;
        _phoneVerification = phoneVerification;
        _storage = storage;
        _logger = logger;
    }

    public async Task<PartnerRegistrationResult> RegisterAsync(
        PartnerRegistrationViewModel model, CancellationToken ct = default)
    {
        var email = model.Email!.Trim();

        // Stored as the plain 10 digits, so "+91 98765 43210" and "9876543210"
        // count as the same number.
        var phone = MobileDigits(model.Phone);
        if (phone is null)
        {
            return PartnerRegistrationResult.Fail("Enter a valid 10-digit Indian mobile number.");
        }

        // ---------------------------------------------------------------
        // 1. Checks that change nothing. Cheapest first.
        // ---------------------------------------------------------------

        if (await _users.FindByEmailAsync(email) is not null)
        {
            return PartnerRegistrationResult.Fail(
                "An account with that email already exists. Try signing in instead.");
        }

        // EndsWith also catches older partners saved as "+91 ..." before this change.
        if (await _db.Vendors.AsNoTracking().AnyAsync(v => v.Phone.EndsWith(phone), ct))
        {
            return PartnerRegistrationResult.Fail(
                "That mobile number is already registered with another partner account.");
        }

        // Only slugs that exist in the catalog survive. Anything else the
        // browser sent is discarded rather than trusted.
        var chosen = model.ServicesOffered
            .Select(slug => _catalog.GetBySlug(slug))
            .Where(s => s is not null)
            .Select(s => s!)
            .DistinctBy(s => s.Slug)
            .ToList();

        if (chosen.Count == 0)
        {
            return PartnerRegistrationResult.Fail("Select at least one service you provide.");
        }

        // CHANGED: documents are optional. Every file that WAS chosen is checked
        // by its real contents before anything is stored.
        var documents = new List<(VendorDocumentType Type, IFormFile File, InspectedFile Info)>();
        foreach (var (type, file) in DocumentsFrom(model))
        {
            if (file is null || file.Length == 0) continue;

            var allowPdf = type != VendorDocumentType.OfficePhoto;
            var info = await DocumentInspector.InspectAsync(file, allowPdf, ct);
            if (info is null)
            {
                return PartnerRegistrationResult.Fail(allowPdf
                    ? $"Your {Label(type)} must be a JPG, PNG, WEBP or PDF file up to 5 MB."
                    : $"Your {Label(type)} must be a JPG, PNG or WEBP photo up to 5 MB.");
            }

            documents.Add((type, file, info));
        }

        // Asks Firebase directly. Changes nothing, so it runs before the transaction.
        if (!await _phoneVerification.IsVerifiedAsync(model.PhoneVerificationToken, phone, ct))
        {
            return PartnerRegistrationResult.VerificationFailed(
                email: false, phone: true,
                "Your mobile verification has expired or doesn't match this number. Please verify your mobile number again.");
        }

        // ---------------------------------------------------------------
        // 2. Everything that writes, inside ONE database transaction.
        //    If any step fails, the database is left exactly as it was:
        //    no half-created user, and the email proof isn't used up.
        // ---------------------------------------------------------------

        var stored = new List<string>();   // files to clean up if something fails
        Vendor vendor;

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        try
        {
            if (!await _emailVerification.ConsumeTokenAsync(email, model.EmailVerificationToken ?? "", ct))
            {
                await transaction.RollbackAsync(ct);
                return PartnerRegistrationResult.VerificationFailed(
                    email: true, phone: false,
                    "Your email verification has expired or doesn't match this address. Please verify your email again.");
            }

            var user = new IdentityUser
            {
                UserName = email,
                Email = email,
                PhoneNumber = phone,
                EmailConfirmed = true,        // proven by the email code
                PhoneNumberConfirmed = true   // proven by the SMS code
            };

            var created = await _users.CreateAsync(user, model.Password!);
            if (!created.Succeeded)
            {
                await transaction.RollbackAsync(ct);

                // Identity's descriptions cover password rules. They never echo the password.
                return PartnerRegistrationResult.Fail(
                    created.Errors.Select(e => e.Description).ToArray());
            }

            var role = await _users.AddToRoleAsync(user, AdminSeeder.VendorRole);
            if (!role.Succeeded)
            {
                throw new InvalidOperationException(
                    "Couldn't add the Vendor role: " + string.Join("; ", role.Errors.Select(e => e.Description)));
            }

            var now = DateTime.UtcNow;

            vendor = new Vendor
            {
                VendorNumber = await NextVendorNumberAsync(ct),
                IdentityUserId = user.Id,
                BusinessName = model.BusinessName!.Trim(),
                ContactPerson = model.ContactPerson!.Trim(),
                Phone = phone,
                Email = email,
                City = model.City!.Trim(),
                Address = model.Address!.Trim(),
                GstNumber = model.GstNumber!.Trim().ToUpperInvariant(),
                YearsOfExperience = model.YearsOfExperience,
                OperatingLocations = Normalise(model.OperatingLocations),
                AdditionalInformation = Normalise(model.AdditionalInformation),

                EmailVerifiedAt = now,
                PhoneVerifiedAt = now,

                // Server-controlled. Every application starts here.
                Status = VendorStatus.Pending,
                CreatedAt = now
            };

            foreach (var service in chosen)
            {
                vendor.Services.Add(new VendorService
                {
                    ServiceSlug = service.Slug,
                    ServiceName = service.Name
                });
            }

            // File names are made by us, never taken from the upload.
            foreach (var (type, file, info) in documents)
            {
                var objectName = $"vendors/{vendor.VendorNumber}/{Slug(type)}-{Guid.NewGuid():N}{info.Extension}";

                await using (var content = file.OpenReadStream())
                {
                    await _storage.SaveAsync(objectName, content, info.ContentType, ct);
                }
                stored.Add(objectName);

                vendor.Documents.Add(new VendorDocument
                {
                    Type = type,
                    StoragePath = objectName,
                    ContentType = info.ContentType,
                    OriginalFileName = CleanFileName(file.FileName),
                    SizeBytes = file.Length,
                    UploadedAt = now
                });
            }

            _db.Vendors.Add(vendor);
            await _db.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Partner registration failed; rolling everything back.");

            try { await transaction.RollbackAsync(CancellationToken.None); }
            catch (Exception rollbackError) { _logger.LogError(rollbackError, "Rollback also failed."); }

            // Storage isn't part of the database transaction, so tidy it by hand.
            foreach (var objectName in stored)
            {
                try { await _storage.DeleteAsync(objectName, CancellationToken.None); }
                catch (Exception cleanupError)
                {
                    _logger.LogWarning(cleanupError, "Couldn't remove orphaned document {ObjectName}.", objectName);
                }
            }

            return PartnerRegistrationResult.Fail(
                "Sorry, we couldn't complete your registration. Please try again in a moment.");
        }

        // After the commit - an email problem must never undo a saved application.
        try
        {
            await _notifications.PartnerRegisteredAsync(vendor, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Vendor {VendorNumber} saved, but the notification failed.", vendor.VendorNumber);
        }

        return PartnerRegistrationResult.Ok(vendor);
    }

    public async Task<Vendor?> GetByIdentityUserIdAsync(
        string identityUserId, bool tracked = false, CancellationToken ct = default)
    {
        var query = _db.Vendors.Include(v => v.Services).AsQueryable();
        if (!tracked) query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync(v => v.IdentityUserId == identityUserId, ct);
    }

    /// <summary>
    /// Legal status moves. Approving straight from Rejected is allowed so an
    /// admin can reverse a decision; going back to Pending is not, because a
    /// reviewed application shouldn't re-enter the queue.
    /// </summary>
    public IReadOnlyList<VendorStatus> AllowedTransitionsFrom(VendorStatus current) => current switch
    {
        VendorStatus.Pending => new[] { VendorStatus.Approved, VendorStatus.Rejected },
        VendorStatus.Approved => new[] { VendorStatus.Suspended, VendorStatus.Rejected },
        VendorStatus.Suspended => new[] { VendorStatus.Approved, VendorStatus.Rejected },
        VendorStatus.Rejected => new[] { VendorStatus.Approved },
        _ => Array.Empty<VendorStatus>()
    };

    // -----------------------------------------------------------------

    private async Task<string> NextVendorNumberAsync(CancellationToken ct)
    {
        var next = await _db.Database
            .SqlQueryRaw<long>("SELECT nextval('vendor_number_seq') AS \"Value\"")
            .SingleAsync(ct);

        return $"SG-V-{DateTime.UtcNow:yyyyMMdd}-{next:D5}";
    }

    private static IEnumerable<(VendorDocumentType Type, IFormFile? File)> DocumentsFrom(
        PartnerRegistrationViewModel model)
    {
        yield return (VendorDocumentType.GstCertificate, model.GstCertificate);
        yield return (VendorDocumentType.PanCard, model.PanCard);
        yield return (VendorDocumentType.AadhaarFront, model.AadhaarFront);
        yield return (VendorDocumentType.AadhaarBack, model.AadhaarBack);
        yield return (VendorDocumentType.OfficePhoto, model.OfficePhoto);
    }

    private static string Label(VendorDocumentType type) => type switch
    {
        VendorDocumentType.GstCertificate => "GST certificate",
        VendorDocumentType.PanCard => "PAN card",
        VendorDocumentType.AadhaarFront => "Aadhaar card (front)",
        VendorDocumentType.AadhaarBack => "Aadhaar card (back)",
        VendorDocumentType.OfficePhoto => "office photo",
        _ => "document"
    };

    private static string Slug(VendorDocumentType type) => type switch
    {
        VendorDocumentType.GstCertificate => "gst-certificate",
        VendorDocumentType.PanCard => "pan-card",
        VendorDocumentType.AadhaarFront => "aadhaar-front",
        VendorDocumentType.AadhaarBack => "aadhaar-back",
        VendorDocumentType.OfficePhoto => "office-photo",
        _ => "document"
    };

    /// <summary>Last 10 digits of an Indian mobile number, or null if it isn't one.</summary>
    private static string? MobileDigits(string? value)
    {
        var digits = new string((value ?? "").Where(char.IsAsciiDigit).ToArray());
        if (digits.Length > 10) digits = digits[^10..];
        return digits.Length == 10 && digits[0] >= '6' ? digits : null;
    }

    /// <summary>Keeps only the name part, without any folder, and caps the length.</summary>
    private static string CleanFileName(string? name)
    {
        var clean = Path.GetFileName(name ?? "").Trim();
        if (clean.Length == 0) clean = "document";
        return clean.Length > 200 ? clean[..200] : clean;
    }

    private static string? Normalise(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}