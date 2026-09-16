using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services.Notifications;
using ShiftingGuru.ViewModels.Partner;

namespace ShiftingGuru.Services;

public class PartnerService : IPartnerService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _users;
    private readonly IServiceCatalog _catalog;
    private readonly INotificationService _notifications;
    private readonly ILogger<PartnerService> _logger;

    public PartnerService(
        ApplicationDbContext db,
        UserManager<IdentityUser> users,
        IServiceCatalog catalog,
        INotificationService notifications,
        ILogger<PartnerService> logger)
    {
        _db = db;
        _users = users;
        _catalog = catalog;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<PartnerRegistrationResult> RegisterAsync(
        PartnerRegistrationViewModel model, CancellationToken ct = default)
    {
        var email = model.Email!.Trim();
        var phone = model.Phone!.Trim();

        if (await _users.FindByEmailAsync(email) is not null)
        {
            return PartnerRegistrationResult.Fail(
                "An account with that email already exists. Try signing in instead.");
        }

        if (await _db.Vendors.AsNoTracking().AnyAsync(v => v.Phone == phone, ct))
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

        var user = new IdentityUser
        {
            UserName = email,
            Email = email,
            PhoneNumber = phone,
            EmailConfirmed = true   // no email infrastructure yet; approval is the real gate
        };

        var created = await _users.CreateAsync(user, model.Password!);

        if (!created.Succeeded)
        {
            // Identity's descriptions cover password rules. They never echo the password.
            return PartnerRegistrationResult.Fail(
                created.Errors.Select(e => e.Description).ToArray());
        }

        try
        {
            await _users.AddToRoleAsync(user, AdminSeeder.VendorRole);

            var vendor = new Vendor
            {
                VendorNumber = await NextVendorNumberAsync(ct),
                IdentityUserId = user.Id,
                BusinessName = model.BusinessName!.Trim(),
                ContactPerson = model.ContactPerson!.Trim(),
                Phone = phone,
                Email = email,
                City = model.City!.Trim(),
                Address = model.Address!.Trim(),
                GstNumber = Normalise(model.GstNumber),
                YearsOfExperience = model.YearsOfExperience,
                OperatingLocations = Normalise(model.OperatingLocations),
                AdditionalInformation = Normalise(model.AdditionalInformation),

                // Server-controlled. Every application starts here.
                Status = VendorStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            foreach (var service in chosen)
            {
                vendor.Services.Add(new VendorService
                {
                    ServiceSlug = service.Slug,
                    ServiceName = service.Name
                });
            }

            _db.Vendors.Add(vendor);
            await _db.SaveChangesAsync(ct);

            // After the vendor row commits - not after the Identity user alone.
            await _notifications.PartnerRegisteredAsync(vendor, ct);

            return PartnerRegistrationResult.Ok(vendor);
        }
        catch (Exception ex)
        {
            // The Identity user exists but the Vendor row failed. Remove the
            // orphan so the applicant can try again with the same email.
            _logger.LogError(ex, "Vendor record creation failed; rolling back the Identity user.");
            await _users.DeleteAsync(user);

            return PartnerRegistrationResult.Fail(
                "Sorry, we couldn't complete your registration. Please try again in a moment.");
        }
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

    private async Task<string> NextVendorNumberAsync(CancellationToken ct)
    {
        var next = await _db.Database
            .SqlQueryRaw<long>("SELECT nextval('vendor_number_seq') AS \"Value\"")
            .SingleAsync(ct);

        return $"SG-V-{DateTime.UtcNow:yyyyMMdd}-{next:D5}";
    }

    private static string? Normalise(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}