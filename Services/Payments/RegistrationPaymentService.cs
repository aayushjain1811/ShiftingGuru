using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services.Notifications;
using ShiftingGuru.Services.Verification;

namespace ShiftingGuru.Services.Payments;

public record PaymentStartResult(
    bool Succeeded,
    string? Error = null,
    string? KeyId = null,
    string? OrderId = null,
    long AmountPaise = 0,
    bool AlreadyPaid = false);

public interface IRegistrationPaymentService
{
    decimal FeeRupees { get; }

    /// <summary>Creates a Razorpay order for a submitted application, unless it's already paid.</summary>
    Task<PaymentStartResult> StartAsync(int vendorId, CancellationToken ct = default);

    /// <summary>Called after the payment window closes successfully. Double-checks with Razorpay.</summary>
    Task<VerificationResult> ConfirmAsync(
        int vendorId, string orderId, string paymentId, string signature, CancellationToken ct = default);

    /// <summary>Called by the Razorpay webhook. Safe to call more than once.</summary>
    Task MarkPaidFromWebhookAsync(string orderId, string paymentId, long amountPaise, CancellationToken ct = default);
}

/// <summary>
/// The partner registration fee, paid AFTER the application is submitted, on
/// its own page. Every payment belongs to one application (Vendor). Paying
/// sets Vendor.RegistrationFeePaidAt, which the admin sees before approving.
/// </summary>
public class RegistrationPaymentService : IRegistrationPaymentService
{
    private const int MaxOrdersPerApplicationPerHour = 10;

    private readonly ApplicationDbContext _db;
    private readonly IRazorpayClient _razorpay;
    private readonly RazorpayOptions _options;
    private readonly INotificationService _notifications;
    private readonly ILogger<RegistrationPaymentService> _logger;

    public RegistrationPaymentService(
        ApplicationDbContext db,
        IRazorpayClient razorpay,
        IOptions<RazorpayOptions> options,
        INotificationService notifications,
        ILogger<RegistrationPaymentService> logger)
    {
        _db = db;
        _razorpay = razorpay;
        _options = options.Value;
        _notifications = notifications;
        _logger = logger;
    }

    public decimal FeeRupees => _options.RegistrationFeeRupees;

    // The amount always comes from here, on the server. Whatever the browser
    // sends is never used, so nobody can pay Rs 1 instead of Rs 229.
    private long FeePaise => (long)Math.Round(_options.RegistrationFeeRupees * 100m);

    public async Task<PaymentStartResult> StartAsync(int vendorId, CancellationToken ct = default)
    {
        var vendor = await _db.Vendors.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vendorId, ct);
        if (vendor is null) return new PaymentStartResult(false, "We couldn't find your application.");

        if (vendor.RegistrationFeePaidAt is not null)
        {
            return new PaymentStartResult(true, AlreadyPaid: true);
        }

        if (!_options.IsConfigured)
        {
            _logger.LogError("Registration payment requested but Razorpay keys are not configured.");
            return new PaymentStartResult(false, "Online payment isn't available right now. Please try again later.");
        }

        var hourAgo = DateTime.UtcNow.AddHours(-1);
        var recent = await _db.RegistrationPayments.CountAsync(p => p.VendorId == vendorId && p.CreatedAt > hourAgo, ct);
        if (recent >= MaxOrdersPerApplicationPerHour)
        {
            return new PaymentStartResult(false, "Too many payment attempts. Please try again in an hour.");
        }

        string orderId;
        try
        {
            orderId = await _razorpay.CreateOrderAsync(FeePaise, vendor.VendorNumber, vendor.Email, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Couldn't create a Razorpay order for vendor {VendorNumber}.", vendor.VendorNumber);
            return new PaymentStartResult(false, "Couldn't start the payment. Please try again.");
        }

        _db.RegistrationPayments.Add(new RegistrationPayment
        {
            Email = vendor.Email.Trim().ToLowerInvariant(),
            RazorpayOrderId = orderId,
            Amount = _options.RegistrationFeeRupees,
            Currency = "INR",
            Status = RegistrationPaymentStatus.Created,
            CreatedAt = DateTime.UtcNow,
            VendorId = vendor.Id
        });
        await _db.SaveChangesAsync(ct);

        return new PaymentStartResult(true, KeyId: _razorpay.KeyId, OrderId: orderId, AmountPaise: FeePaise);
    }

    public async Task<VerificationResult> ConfirmAsync(
        int vendorId, string orderId, string paymentId, string signature, CancellationToken ct = default)
    {
        // 1. The signature proves this answer really came from Razorpay's window.
        if (!_razorpay.IsValidCheckoutSignature(orderId, paymentId, signature))
        {
            _logger.LogWarning("Invalid Razorpay signature for order {OrderId}.", orderId);
            return VerificationResult.Fail("We couldn't confirm that payment. If money was deducted, contact us with your payment ID.");
        }

        var payment = await _db.RegistrationPayments
            .FirstOrDefaultAsync(p => p.RazorpayOrderId == orderId && p.VendorId == vendorId, ct);

        if (payment is null) return VerificationResult.Fail("We couldn't find that payment. Please try again.");
        if (payment.Status == RegistrationPaymentStatus.Paid) return VerificationResult.Ok(orderId);

        // 2. Ask Razorpay directly: right order, right amount, money captured.
        var info = await _razorpay.GetPaymentAsync(paymentId, ct);
        if (info is null || info.OrderId != orderId || info.AmountPaise != FeePaise || info.Currency != "INR")
        {
            _logger.LogWarning("Razorpay payment {PaymentId} doesn't match order {OrderId}.", paymentId, orderId);
            return VerificationResult.Fail("We couldn't confirm that payment. If money was deducted, contact us with your payment ID.");
        }

        if (info.Status != "captured")
        {
            // Normally captured within seconds. The webhook records it when it is.
            return VerificationResult.Fail("Your payment is still processing. Wait a minute, then click Pay again - you won't be charged twice.");
        }

        await MarkPaidAsync(payment, paymentId, ct);
        return VerificationResult.Ok(orderId);
    }

    public async Task MarkPaidFromWebhookAsync(
        string orderId, string paymentId, long amountPaise, CancellationToken ct = default)
    {
        var payment = await _db.RegistrationPayments.FirstOrDefaultAsync(p => p.RazorpayOrderId == orderId, ct);

        // Not ours (another product on the same Razorpay account), or already recorded.
        if (payment is null || payment.Status != RegistrationPaymentStatus.Created) return;

        if (amountPaise != (long)Math.Round(payment.Amount * 100m))
        {
            _logger.LogWarning("Webhook amount {Amount} doesn't match order {OrderId}.", amountPaise, orderId);
            return;
        }

        await MarkPaidAsync(payment, paymentId, ct);
    }

    private async Task MarkPaidAsync(RegistrationPayment payment, string paymentId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        payment.Status = RegistrationPaymentStatus.Paid;
        payment.RazorpayPaymentId = paymentId;
        payment.PaidAt = now;
        payment.UsedAt = now;   // tied to its application from the start

        Vendor? activated = null;

        if (payment.VendorId is int vendorId)
        {
            var vendor = await _db.Vendors.FirstOrDefaultAsync(v => v.Id == vendorId, ct);
            if (vendor is not null && vendor.RegistrationFeePaidAt is null)
            {
                vendor.RegistrationFeePaidAt = now;
            }

            // CHANGED: paying is what turns a draft into a real application.
            if (vendor is not null && vendor.Status == VendorStatus.AwaitingPayment)
            {
                vendor.Status = VendorStatus.Pending;
                vendor.UpdatedAt = now;
                activated = vendor;
            }
        }

        await _db.SaveChangesAsync(ct);

        // Only now does the team hear about the application - never for unpaid ones.
        if (activated is not null)
        {
            try
            {
                await _notifications.PartnerRegisteredAsync(activated, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Vendor {VendorNumber} paid, but the notification failed.", activated.VendorNumber);
            }
        }
    }
}