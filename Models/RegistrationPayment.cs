namespace ShiftingGuru.Models;

/// <summary>
/// One attempt to pay the partner registration fee through Razorpay.
///
/// The fee is paid BEFORE the application is submitted, so at that point
/// there is no Vendor yet. The payment is tied to the verified email, and
/// linked to the Vendor when the application is saved. A paid row with no
/// Vendor means someone paid but never finished - the admin payments page
/// lists these so they can be followed up or refunded.
/// </summary>
public class RegistrationPayment
{
    public long Id { get; set; }

    /// <summary>The verified email the fee was paid for. Trimmed, lower-case.</summary>
    public string Email { get; set; } = "";

    /// <summary>Razorpay's order id, e.g. order_Nf3k.... Unique.</summary>
    public string RazorpayOrderId { get; set; } = "";

    /// <summary>Razorpay's payment id, e.g. pay_Nf3k.... Set once paid.</summary>
    public string? RazorpayPaymentId { get; set; }

    /// <summary>In rupees, e.g. 229.00. Razorpay itself works in paise (22900).</summary>
    public decimal Amount { get; set; }

    public string Currency { get; set; } = "INR";

    public RegistrationPaymentStatus Status { get; set; } = RegistrationPaymentStatus.Created;

    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }

    /// <summary>Set when the payment is spent on a submitted application. Single use.</summary>
    public DateTime? UsedAt { get; set; }

    public int? VendorId { get; set; }
    public Vendor? Vendor { get; set; }
}

public enum RegistrationPaymentStatus
{
    /// <summary>Order created, payment window opened, not paid (yet).</summary>
    Created,

    /// <summary>Money received and confirmed with Razorpay.</summary>
    Paid,

    /// <summary>Refunded by an admin in the Razorpay dashboard.</summary>
    Refunded
}