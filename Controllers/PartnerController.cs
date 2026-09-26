using Microsoft.AspNetCore.Mvc;
using ShiftingGuru.Data;
using ShiftingGuru.Services;
using ShiftingGuru.Services.Payments;
using ShiftingGuru.Services.Verification;
using ShiftingGuru.ViewModels.Partner;

namespace ShiftingGuru.Controllers;

/// <summary>
/// The public-facing partner pages. Signed-in partner functionality lives in
/// the Partner area; this is just the front door.
/// </summary>
public class PartnerController : Controller
{
    private const long MaxRequestBytes = 28 * 1024 * 1024;

    private readonly IServiceCatalog _catalog;
    private readonly IPartnerService _partners;
    private readonly IEmailVerificationService _emailVerification;
    private readonly IRegistrationPaymentService _payments;
    private readonly ILogger<PartnerController> _logger;

    public PartnerController(
        IServiceCatalog catalog,
        IPartnerService partners,
        IEmailVerificationService emailVerification,
        IRegistrationPaymentService payments,
        ILogger<PartnerController> logger)
    {
        _catalog = catalog;
        _partners = partners;
        _emailVerification = emailVerification;
        _payments = payments;
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

            if (result.ResetPayment)
            {
                ModelState.Remove(nameof(model.RegistrationOrderId));
                model.RegistrationOrderId = null;
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }
            return View(Prepare(model));
        }

        // Only the business name crosses over - no contact details in TempData.
        TempData["PartnerBusinessName"] = result.Vendor!.BusinessName;
        TempData["PartnerVendorNumber"] = result.Vendor.VendorNumber;

        return RedirectToAction(nameof(Success));
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

    // NEW
    // POST /join-as-partner/payment/start   (called by partner-join.js)
    // Creates the Razorpay order. Only for an email that has just been verified.
    [HttpPost("/join-as-partner/payment/start")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartPayment([FromBody] PaymentStartRequest request, CancellationToken ct)
    {
        var result = await _payments.StartAsync(request?.Email ?? "", request?.EmailToken ?? "", ct);

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
    // POST /join-as-partner/payment/confirm   (called by partner-join.js)
    [HttpPost("/join-as-partner/payment/confirm")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmPayment([FromBody] PaymentConfirmRequest request, CancellationToken ct)
    {
        var result = await _payments.ConfirmAsync(
            request?.OrderId ?? "", request?.PaymentId ?? "", request?.Signature ?? "", ct);

        return result.Succeeded
            ? Ok(new { orderId = result.Token })
            : BadRequest(new { error = result.Error });
    }

    // GET /join-as-partner/success
    [HttpGet("/join-as-partner/success")]
    public IActionResult Success()
    {
        var vendorNumber = TempData["PartnerVendorNumber"] as string;
        if (string.IsNullOrEmpty(vendorNumber)) return RedirectToAction(nameof(Join));

        ViewData["Title"] = "Application received";
        ViewData["VendorNumber"] = vendorNumber;
        ViewData["BusinessName"] = TempData["PartnerBusinessName"] as string;

        return View();
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

/// <summary>NEW: what partner-join.js sends to start the registration fee payment.</summary>
public record PaymentStartRequest(string? Email, string? EmailToken);

/// <summary>NEW: what the Razorpay window hands back after a successful payment.</summary>
public record PaymentConfirmRequest(string? OrderId, string? PaymentId, string? Signature);