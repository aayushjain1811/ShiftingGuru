using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services.Notifications;

namespace ShiftingGuru.Services.Push;

/// <summary>
/// NEW (mobile apps): adds push notifications to every event the website
/// already emails about - without changing NotificationService at all.
///
/// It wraps the real NotificationService: each method first does exactly what
/// it always did (the emails), then sends the matching push to the partner's
/// or customer's phones. Because every part of the site already calls
/// INotificationService, website actions (an admin assigning a lead, marking a
/// move complete, approving a review) now reach the apps too.
///
/// Admin-only events (new partner, new review to moderate) stay email-only.
/// Each push carries a PushCategory, so people's notification switches are
/// respected; account notices (approved, paused) have none and always go.
/// </summary>
public class PushingNotificationService : INotificationService
{
    private readonly NotificationService _inner;
    private readonly IPushSender _push;
    private readonly ApplicationDbContext _db;

    public PushingNotificationService(NotificationService inner, IPushSender push, ApplicationDbContext db)
    {
        _inner = inner;
        _push = push;
        _db = db;
    }

    // ---- A customer made a request ----
    public async Task LeadCreatedAsync(Lead lead, string? customerAccessUrl, CancellationToken ct = default)
    {
        await _inner.LeadCreatedAsync(lead, customerAccessUrl, ct);

        await ToCustomerAsync(lead, new PushMessage(
            "Request received",
            $"We're finding verified partners for your {lead.ServiceName.ToLowerInvariant()}. Quotes usually arrive within a few hours.",
            $"/request/{lead.Id}", PushCategory.Bookings), ct);
    }

    // ---- The team sent a lead to a partner ----
    public async Task LeadAssignedAsync(Lead lead, Vendor vendor, CancellationToken ct = default)
    {
        await _inner.LeadAssignedAsync(lead, vendor, ct);

        await _push.SendAsync(new[] { vendor.IdentityUserId }, new PushMessage(
            $"New lead: {lead.ServiceName}",
            $"{Where(lead)}{When(lead)}. Accept it and send your quote before others do.",
            $"/lead/{lead.Id}", PushCategory.Leads), ct);
    }

    // ---- A partner sent a quote ----
    public async Task QuoteSubmittedAsync(Quote quote, Lead lead, Vendor vendor, CancellationToken ct = default)
    {
        await _inner.QuoteSubmittedAsync(quote, lead, vendor, ct);

        await ToCustomerAsync(lead, new PushMessage(
            "New quote received",
            $"{vendor.BusinessName} quoted {Money.Format(quote.TotalAmount)} for your {lead.ServiceName.ToLowerInvariant()}. Compare and choose.",
            $"/request/{lead.Id}", PushCategory.Quotes), ct);
    }

    // ---- The customer chose a partner ----
    public async Task VendorSelectedAsync(
        Lead lead, Quote winningQuote, Vendor winningVendor,
        IReadOnlyList<(Quote Quote, Vendor Vendor)> otherQuotes, CancellationToken ct = default)
    {
        await _inner.VendorSelectedAsync(lead, winningQuote, winningVendor, otherQuotes, ct);

        await _push.SendAsync(new[] { winningVendor.IdentityUserId }, new PushMessage(
            "You won the job!",
            $"{lead.CustomerName} chose your quote of {Money.Format(winningQuote.TotalAmount)}. Call them to plan the move.",
            $"/lead/{lead.Id}", PushCategory.Quotes), ct);

        await ToCustomerAsync(lead, new PushMessage(
            "Booking confirmed",
            $"{winningVendor.BusinessName} will contact you to plan your move.",
            $"/booking/{lead.Id}", PushCategory.Bookings), ct);

        if (otherQuotes.Count > 0)
        {
            await _push.SendAsync(otherQuotes.Select(o => o.Vendor.IdentityUserId), new PushMessage(
                "Customer chose another partner",
                $"{lead.LeadNumber} went to another partner this time. Keep quoting - new leads keep coming.",
                $"/lead/{lead.Id}", PushCategory.Quotes), ct);
        }
    }

    // ---- New partner application (admin only - email stays as it was) ----
    public Task PartnerRegisteredAsync(Vendor vendor, CancellationToken ct = default) =>
        _inner.PartnerRegisteredAsync(vendor, ct);

    // ---- The team approved, suspended or rejected a partner ----
    public async Task PartnerStatusChangedAsync(Vendor vendor, CancellationToken ct = default)
    {
        await _inner.PartnerStatusChangedAsync(vendor, ct);

        var message = vendor.Status switch
        {
            VendorStatus.Approved => new PushMessage(
                "Your partner account is active",
                "You can now receive leads and send quotes.", "/(vendor)/home"),
            VendorStatus.Suspended => new PushMessage(
                "Your partner account is paused",
                "Please contact ShiftingGuru support."),
            VendorStatus.Rejected => new PushMessage(
                "Partner application update",
                "Please check your email for details from ShiftingGuru."),
            _ => null
        };

        if (message is not null)
        {
            await _push.SendAsync(new[] { vendor.IdentityUserId }, message, ct);
        }
    }

    // ---- The team marked the move complete ----
    public async Task LeadCompletedAsync(Lead lead, Vendor vendor, CancellationToken ct = default)
    {
        await _inner.LeadCompletedAsync(lead, vendor, ct);

        await _push.SendAsync(new[] { vendor.IdentityUserId }, new PushMessage(
            "Job marked complete",
            $"{lead.LeadNumber} is complete. Well done!",
            $"/lead/{lead.Id}", PushCategory.Bookings), ct);

        await ToCustomerAsync(lead, new PushMessage(
            "How was your move?",
            $"Your move with {vendor.BusinessName} is complete. Tap to rate them.",
            $"/review/{lead.Id}", PushCategory.Reviews), ct);
    }

    // ---- New review to moderate (admin only - email stays as it was) ----
    public Task ReviewSubmittedAsync(Review review, Lead lead, Vendor vendor, CancellationToken ct = default) =>
        _inner.ReviewSubmittedAsync(review, lead, vendor, ct);

    // ---- A review of the partner went live ----
    public async Task ReviewPublishedAsync(Review review, Vendor vendor, CancellationToken ct = default)
    {
        await _inner.ReviewPublishedAsync(review, vendor, ct);

        await _push.SendAsync(new[] { vendor.IdentityUserId }, new PushMessage(
            "New review",
            $"{review.CustomerNameSnapshot} gave you {review.Rating} star{(review.Rating == 1 ? "" : "s")}.",
            "/(vendor)/profile", PushCategory.Reviews), ct);
    }

    // -----------------------------------------------------------------

    /// <summary>Pushes to the app customer who owns this request, if there is one.</summary>
    private async Task ToCustomerAsync(Lead lead, PushMessage message, CancellationToken ct)
    {
        if (lead.CustomerId is null) return;   // website-only customer: email only, as before

        var userId = await _db.Customers.AsNoTracking()
            .Where(c => c.Id == lead.CustomerId)
            .Select(c => c.IdentityUserId)
            .FirstOrDefaultAsync(ct);

        if (userId is not null) await _push.SendAsync(new[] { userId }, message, ct);
    }

    private static string Where(Lead lead) =>
        lead.StorageLocation is { } storage ? $"Storage in {storage}"
        : $"{lead.MovingFrom ?? "?"} to {lead.MovingTo ?? "?"}";

    private static string When(Lead lead) =>
        lead.MovingDate is { } date ? $", {date:d MMM}" : "";
}