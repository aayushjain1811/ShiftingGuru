using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
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

    /// <summary>Creates a Razorpay order for a VERIFIED email, or returns an unused earlier payment.</summary>
    Task<PaymentStartResult> StartAsync(string email, string emailToken, CancellationToken ct = default);

    /// <summary>Called after the payment window closes successfully. Double-checks with Razorpay.</summary>
    Task<VerificationResult> ConfirmAsync(string orderId, string paymentId, string signature, CancellationToken ct = default);

    /// <summary>Called by the Razorpay webhook. Safe to call more than once.</summary>
    Task MarkPaidFromWebhookAsync(string orderId, string paymentId, long amountPaise, CancellationToken ct = default);

    /// <summary>
    /// Used when the application is submitted: the paid, unused payment for this
    /// email and order, marked as used (NOT saved - the caller saves it together
    /// with the new Vendor, inside the same transaction). Null if there isn't one.
    /// </summary>
    Task<RegistrationPayment?> ConsumeAsync(string email, string orderId, CancellationToken ct = default);
}

public class RegistrationPaymentService : IRegistrationPaymentService
{
    private const int MaxOrdersPerEmailPerHour = 10;

    private readonly ApplicationDbContext _db;
    private readonly IRazorpayClient _razorpay;
    private readonly IEmailVerificationService _emailVerification;
    private readonly RazorpayOptions _options;
    private readonly ILogger<RegistrationPaymentService> _logger;

    public RegistrationPaymentService(
        ApplicationDbContext db,
        IRazorpayClient razorpay,
        IEmailVerificationService emailVerification,
        IOptions<RazorpayOptions> options,
        ILogger<RegistrationPaymentService> logger)
    {
        _db = db;
        _razorpay = razorpay;
        _emailVerification = emailVerification;
        _options = options.Value;
        _logger = logger;
    }

    public decimal FeeRupees => _options.RegistrationFeeRupees;

    // The amount always comes from here, on the server. Whatever the browser
    // sends is never used, so nobody can pay Rs 1 instead of Rs 229.
    private long FeePaise => (long)Math.Round(_options.RegistrationFeeRupees * 100m);

    public async Task<PaymentStartResult> StartAsync(string email, string emailToken, CancellationToken ct = default)
    {
        var address = email.Trim().ToLowerInvariant();

        // Nobody is charged until their email is verified.
        if (!await _emailVerification.IsTokenValidAsync(address, emailToken, ct))
        {
            return new PaymentStartResult(false, "Verify your email address first, then pay.");
        }

        if (!_options.IsConfigured)
        {
            _logger.LogError("Registration payment requested but Razorpay keys are not configured.");
            return new PaymentStartResult(false, "Online payment isn't available right now. Please try again later.");
        }

        // Already paid earlier and not used yet (closed the tab, came back):
        // reuse that payment instead of charging twice.
        var unused = await _db.RegistrationPayments.AsNoTracking()
            .Where(p => p.Email == address && p.Status == RegistrationPaymentStatus.Paid && p.UsedAt == null)
            .OrderByDescending(p => p.PaidAt)
            .FirstOrDefaultAsync(ct);

        if (unused is not null)
        {
            return new PaymentStartResult(true, OrderId: unused.RazorpayOrderId, AlreadyPaid: true);
        }

        var hourAgo = DateTime.UtcNow.AddHours(-1);
        var recent = await _db.RegistrationPayments.CountAsync(p => p.Email == address && p.CreatedAt > hourAgo, ct);
        if (recent >= MaxOrdersPerEmailPerHour)
        {
            return new PaymentStartResult(false, "Too many payment attempts. Please try again in an hour.");
        }

        string orderId;
        try
        {
            var receipt = "reg-" + Guid.NewGuid().ToString("N")[..16];
            orderId = await _razorpay.CreateOrderAsync(FeePaise, receipt, address, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Couldn't create a Razorpay order for {Email}.", address);
            return new PaymentStartResult(false, "Couldn't start the payment. Please try again.");
        }

        _db.RegistrationPayments.Add(new RegistrationPayment
        {
            Email = address,
            RazorpayOrderId = orderId,
            Amount = _options.RegistrationFeeRupees,
            Currency = "INR",
            Status = RegistrationPaymentStatus.Created,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);

        return new PaymentStartResult(true, KeyId: _razorpay.KeyId, OrderId: orderId, AmountPaise: FeePaise);
    }

    public async Task<VerificationResult> ConfirmAsync(
        string orderId, string paymentId, string signature, CancellationToken ct = default)
    {
        // 1. The signature proves this answer really came from Razorpay's window.
        if (!_razorpay.IsValidCheckoutSignature(orderId, paymentId, signature))
        {
            _logger.LogWarning("Invalid Razorpay signature for order {OrderId}.", orderId);
            return VerificationResult.Fail("We couldn't confirm that payment. If money was deducted, contact us with your payment ID.");
        }

        var payment = await _db.RegistrationPayments.FirstOrDefaultAsync(p => p.RazorpayOrderId == orderId, ct);
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

        payment.Status = RegistrationPaymentStatus.Paid;
        payment.RazorpayPaymentId = paymentId;
        payment.PaidAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

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

        payment.Status = RegistrationPaymentStatus.Paid;
        payment.RazorpayPaymentId = paymentId;
        payment.PaidAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<RegistrationPayment?> ConsumeAsync(string email, string orderId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(orderId)) return null;

        var address = email.Trim().ToLowerInvariant();
        var payment = await _db.RegistrationPayments.FirstOrDefaultAsync(p => p.RazorpayOrderId == orderId.Trim(), ct);

        if (payment is null ||
            payment.Status != RegistrationPaymentStatus.Paid ||
            payment.UsedAt is not null ||
            payment.Email != address)
        {
            return null;
        }

        payment.UsedAt = DateTime.UtcNow;
        return payment;
    }
}