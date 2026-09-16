namespace ShiftingGuru.Models;

/// <summary>
/// One outbound notification. Written before any send is attempted, so a
/// crash mid-send leaves a Pending row the startup sweep can pick up.
/// </summary>
public class NotificationLog
{
    public int Id { get; set; }

    public NotificationType Type { get; set; }

    /// <summary>Recipient email. No other contact details are stored here.</summary>
    public string Recipient { get; set; } = "";

    public string Subject { get; set; } = "";

    /// <summary>Rendered HTML, so a retry doesn't have to rebuild it.</summary>
    public string HtmlBody { get; set; } = "";

    public string? TextBody { get; set; }

    // ---- what this notification is about, for idempotency and the admin UI ----
    public string EntityType { get; set; } = "";
    public int EntityId { get; set; }

    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;

    public int AttemptCount { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? SentAt { get; set; }

    /// <summary>Provider error, trimmed. Never contains credentials.</summary>
    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; }
}

public enum NotificationStatus
{
    Pending,
    Sent,
    Failed
}

public enum NotificationType
{
    // Customer
    CustomerLeadCreated,
    CustomerQuoteAvailable,
    CustomerVendorSelected,

    // Vendor
    VendorLeadAssigned,
    VendorQuoteSubmitted,
    VendorQuoteAccepted,
    VendorQuoteNotSelected,
    PartnerRegistered,
    PartnerApproved,
    PartnerRejected,

    CustomerReviewInvitation,
    VendorReviewPublished,

    // Admin
    AdminNewLead,
    AdminNewReview,
    AdminNewVendor,
    AdminNewQuote,
    AdminLeadConverted
}