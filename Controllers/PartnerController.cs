using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services;
using ShiftingGuru.Services.Email;
using ShiftingGuru.Services.Payments;
using ShiftingGuru.Services.Verification;
using ShiftingGuru.ViewModels.Partner;

namespace ShiftingGuru.Controllers;

/// <summary>
/// The public-facing partner pages. Signed-in partner functionality lives in
/// the Partner area; this is just the front door.
///
/// Flow: fill the form and verify email + mobile -> "Start 7-day free trial"
/// saves the application -> payment page for the registration fee -> success.
/// </summary>
public class PartnerController : Controller
{
    private const long MaxRequestBytes = 28 * 1024 * 1024;

    // How long the "complete your registration" link keeps working.
    private static readonly TimeSpan PaymentLinkLifetime = TimeSpan.FromDays(7);

    private readonly IServiceCatalog _catalog;
    private readonly IPartnerService _partners;
    private readonly IEmailVerificationService _emailVerification;
    private readonly IRegistrationPaymentService _payments;
    private readonly IEmailService _email;
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;
    private readonly ITimeLimitedDataProtector _linkProtector;
    private readonly ILogger<PartnerController> _logger;

    public PartnerController(
        IServiceCatalog catalog,
        IPartnerService partners,
        IEmailVerificationService emailVerification,
        IRegistrationPaymentService payments,
        IEmailService email,
        ApplicationDbContext db,
        IConfiguration config,
        IDataProtectionProvider dataProtection,
        ILogger<PartnerController> logger)
    {
        _catalog = catalog;
        _partners = partners;
        _emailVerification = emailVerification;
        _payments = payments;
        _email = email;
        _db = db;
        _config = config;
        _linkProtector = dataProtection.CreateProtector("PartnerPaymentLink").ToTimeLimitedDataProtector();
        _logger = logger;
    }

    // GET /join-as-partner
    [HttpGet("/join-as-partner")]
    public IActionResult Join() => View(Prepare(new PartnerRegistrationViewModel()));

    // POST /join-as-partner
    // Five files of up to 5 MB each, plus the form. The cap stays under Cloud
    // Run's 32 MB request limit, so an oversized upload gets a clean error.
    [HttpPost("/join-as-partner")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    public async Task<IActionResult> Join(PartnerRegistrationViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(Prepare(model));

        var result = await _partners.RegisterAsync(model, ct);

        if (!result.Succeeded)
        {
            // A rejected proof must be redone, so clear it and the page shows
            // "Send code" again. ModelState.Remove is needed because the form
            // re-displays the value it received rather than the model's.
            if (result.ResetEmailVerification)
            {
                ModelState.Remove(nameof(model.EmailVerificationToken));
                model.EmailVerificationToken = null;
            }

            if (result.ResetPhoneVerification)
            {
                ModelState.Remove(nameof(model.PhoneVerificationToken));
                model.PhoneVerificationToken = null;
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }
            return View(Prepare(model));
        }

        var vendor = result.Vendor!;
        var token = _linkProtector.Protect(vendor.Id.ToString(), PaymentLinkLifetime);

        // So the partner can come back and pay if they close the page.
        await SendPaymentLinkAsync(vendor, token, ct);

        return Redirect($"/join-as-partner/payment?t={Uri.EscapeDataString(token)}");
    }

    // NEW
    // GET /join-as-partner/payment?t=...
    [HttpGet("/join-as-partner/payment")]
    public async Task<IActionResult> Payment(string? t, CancellationToken ct)
    {
        ViewData["Title"] = "Complete your registration";
        ViewData["Robots"] = "noindex, nofollow";

        var vendor = await VendorFromTokenAsync(t, ct);
        if (vendor is null)
        {
            return View(new PartnerPaymentViewModel { LinkExpired = true });
        }

        if (vendor.RegistrationFeePaidAt is not null)
        {
            return Redirect($"/join-as-partner/success?t={Uri.EscapeDataString(t!)}");
        }

        return View(new PartnerPaymentViewModel
        {
            Token = t!,
            VendorNumber = vendor.VendorNumber,
            BusinessName = vendor.BusinessName,
            ContactName = vendor.ContactPerson,
            Email = vendor.Email,
            Phone = vendor.Phone,
            FeeRupees = _payments.FeeRupees,
            TrialDays = Vendor.FreeTrialDays
        });
    }

    // NEW
    // POST /join-as-partner/payment/start   (called by the payment page)
    [HttpPost("/join-as-partner/payment/start")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartPayment([FromBody] PaymentStartRequest request, CancellationToken ct)
    {
        var vendorId = ReadToken(request?.Token);
        if (vendorId is null) return BadRequest(new { error = "This payment link has expired. Please contact support." });

        var result = await _payments.StartAsync(vendorId.Value, ct);
        if (!result.Succeeded) return BadRequest(new { error = result.Error });

        return Ok(new
        {
            alreadyPaid = result.AlreadyPaid,
            orderId = result.OrderId,
            keyId = result.KeyId,
            amountPaise = result.AmountPaise
        });
    }

    // NEW
    // POST /join-as-partner/payment/confirm   (called by the payment page)
    [HttpPost("/join-as-partner/payment/confirm")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmPayment([FromBody] PaymentConfirmRequest request, CancellationToken ct)
    {
        var vendorId = ReadToken(request?.Token);
        if (vendorId is null) return BadRequest(new { error = "This payment link has expired. Please contact support." });

        var result = await _payments.ConfirmAsync(
            vendorId.Value, request?.OrderId ?? "", request?.PaymentId ?? "", request?.Signature ?? "", ct);

        return result.Succeeded
            ? Ok(new { redirect = $"/join-as-partner/success?t={Uri.EscapeDataString(request!.Token!)}" })
            : BadRequest(new { error = result.Error });
    }

    // POST /join-as-partner/email-code/send   (called by partner-join.js)
    [HttpPost("/join-as-partner/email-code/send")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendEmailCode([FromBody] EmailCodeRequest request, CancellationToken ct)
    {
        var result = await _emailVerification.SendCodeAsync(request?.Email ?? "", ct);
        return result.Succeeded ? Ok() : BadRequest(new { error = result.Error });
    }

    // POST /join-as-partner/email-code/verify   (called by partner-join.js)
    [HttpPost("/join-as-partner/email-code/verify")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyEmailCode([FromBody] EmailCodeRequest request, CancellationToken ct)
    {
        var result = await _emailVerification.VerifyCodeAsync(request?.Email ?? "", request?.Code ?? "", ct);
        return result.Succeeded ? Ok(new { token = result.Token }) : BadRequest(new { error = result.Error });
    }

    // GET /join-as-partner/success?t=...
    [HttpGet("/join-as-partner/success")]
    public async Task<IActionResult> Success(string? t, CancellationToken ct)
    {
        var vendor = await VendorFromTokenAsync(t, ct);
        if (vendor is null) return RedirectToAction(nameof(Join));

        // Not paid yet: back to the payment page.
        if (vendor.RegistrationFeePaidAt is null)
        {
            return Redirect($"/join-as-partner/payment?t={Uri.EscapeDataString(t!)}");
        }

        ViewData["Title"] = "Application received";
        ViewData["Robots"] = "noindex, nofollow";
        ViewData["VendorNumber"] = vendor.VendorNumber;
        ViewData["BusinessName"] = vendor.BusinessName;

        return View();
    }

    // -----------------------------------------------------------------

    private int? ReadToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        try
        {
            // Fails if the link was changed by hand or is older than 7 days.
            return int.TryParse(_linkProtector.Unprotect(token), out var id) ? id : null;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private async Task<Vendor?> VendorFromTokenAsync(string? token, CancellationToken ct)
    {
        var vendorId = ReadToken(token);
        return vendorId is null
            ? null
            : await _db.Vendors.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vendorId, ct);
    }

    private async Task SendPaymentLinkAsync(Vendor vendor, string token, CancellationToken ct)
    {
        var baseUrl = (_config["App:BaseUrl"] ?? "https://www.shiftingguru.com").TrimEnd('/');
        var link = $"{baseUrl}/join-as-partner/payment?t={Uri.EscapeDataString(token)}";
        var fee = "Rs " + _payments.FeeRupees.ToString("0.##");

        var name = WebUtility.HtmlEncode(vendor.ContactPerson);
        var number = WebUtility.HtmlEncode(vendor.VendorNumber);
        var safeLink = WebUtility.HtmlEncode(link);

        var html = $"""
            <div style="font-family:Arial,Helvetica,sans-serif;max-width:520px;margin:0 auto;padding:32px 24px;color:#182238">
              <p style="margin:0 0 12px;font-size:15px">Hi {name},</p>
              <p style="margin:0 0 12px;font-size:15px">Your ShiftingGuru partner application <strong>{number}</strong> has been saved.</p>
              <p style="margin:0 0 20px;font-size:15px">To complete it, pay the one-time {fee} registration fee. Your {Vendor.FreeTrialDays}-day free trial starts when our team approves your application.</p>
              <p style="margin:0 0 24px"><a href="{safeLink}" style="display:inline-block;background:#3156C6;color:#ffffff;text-decoration:none;padding:12px 22px;border-radius:8px;font-weight:bold">Complete registration</a></p>
              <p style="margin:0;font-size:13px;color:#667085">If you have already paid, you can ignore this email. This link works for 7 days.</p>
            </div>
            """;

        var text =
            $"Hi {vendor.ContactPerson},\n\nYour ShiftingGuru partner application {vendor.VendorNumber} has been saved.\n" +
            $"To complete it, pay the one-time {fee} registration fee: {link}\n\n" +
            "If you have already paid, you can ignore this email. This link works for 7 days.";

        try
        {
            await _email.SendAsync(vendor.Email, "Complete your ShiftingGuru partner registration", html, text, ct);
        }
        catch (Exception ex)
        {
            // Never block the partner because an email failed.
            _logger.LogError(ex, "Couldn't send the payment link to vendor {VendorNumber}.", vendor.VendorNumber);
        }
    }

    private PartnerRegistrationViewModel Prepare(PartnerRegistrationViewModel model)
    {
        model.AvailableServices = _catalog.GetAll();

        ViewData["Title"] = "Become a Partner";
        ViewData["MetaDescription"] =
            "Register your moving or logistics business with ShiftingGuru and receive "
            + "relevant customer enquiries from across India.";
        ViewData["Canonical"] = "https://www.shiftingguru.com/join-as-partner";

        return model;
    }
}

/// <summary>What partner-join.js sends to the two email-code endpoints.</summary>
public record EmailCodeRequest(string? Email, string? Code);

/// <summary>What the payment page sends to start a payment.</summary>
public record PaymentStartRequest(string? Token);

/// <summary>What the Razorpay window hands back after a successful payment, plus the page's link token.</summary>
public record PaymentConfirmRequest(string? Token, string? OrderId, string? PaymentId, string? Signature);