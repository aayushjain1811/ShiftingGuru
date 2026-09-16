using ShiftingGuru.Models;

namespace ShiftingGuru.Services.Notifications;

/// <summary>
/// Business-level notifications. Callers describe what happened; this decides
/// who hears about it and what they're told.
///
/// Every method persists a NotificationLog row and returns immediately - none
/// of them block a web request on SMTP, and none of them throw.
/// </summary>
public interface INotificationService
{
    Task LeadCreatedAsync(Lead lead, string? customerAccessUrl, CancellationToken ct = default);

    Task LeadAssignedAsync(Lead lead, Vendor vendor, CancellationToken ct = default);

    Task QuoteSubmittedAsync(Quote quote, Lead lead, Vendor vendor, CancellationToken ct = default);

    Task VendorSelectedAsync(
        Lead lead, Quote winningQuote, Vendor winningVendor,
        IReadOnlyList<(Quote Quote, Vendor Vendor)> otherQuotes, CancellationToken ct = default);

    Task PartnerRegisteredAsync(Vendor vendor, CancellationToken ct = default);

    Task PartnerStatusChangedAsync(Vendor vendor, CancellationToken ct = default);

    /// <summary>The move is complete; invite the customer to review.</summary>
    Task LeadCompletedAsync(Lead lead, Vendor vendor, CancellationToken ct = default);

    /// <summary>A review arrived and needs moderating.</summary>
    Task ReviewSubmittedAsync(Review review, Lead lead, Vendor vendor, CancellationToken ct = default);

    /// <summary>A review went live. Told to the vendor only.</summary>
    Task ReviewPublishedAsync(Review review, Vendor vendor, CancellationToken ct = default);
}