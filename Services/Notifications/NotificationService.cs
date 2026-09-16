using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services.Email;

namespace ShiftingGuru.Services.Notifications;

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _db;
    private readonly NotificationQueue _queue;
    private readonly EmailTemplate _template;
    private readonly EmailOptions _email;
    private readonly AppOptions _app;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        ApplicationDbContext db,
        NotificationQueue queue,
        EmailTemplate template,
        IOptions<EmailOptions> email,
        IOptions<AppOptions> app,
        ILogger<NotificationService> logger)
    {
        _db = db;
        _queue = queue;
        _template = template;
        _email = email.Value;
        _app = app.Value;
        _logger = logger;
    }

    // ---------------------------------------------------------------
    // Lead created
    // ---------------------------------------------------------------
    public async Task LeadCreatedAsync(Lead lead, string? customerAccessUrl, CancellationToken ct = default)
    {
        var route = Route(lead);

        if (!string.IsNullOrWhiteSpace(lead.Email))
        {
            await QueueAsync(NotificationType.CustomerLeadCreated, lead.Email!,
                "We received your ShiftingGuru request", nameof(Lead), lead.Id,
                new EmailContent
                {
                    Heading = "We've received your request",
                    Intro = $"Thanks, {lead.CustomerName}. We're matching your requirement to "
                          + "professionals who handle this kind of work. You'll hear from them directly.",
                    Rows = new[]
                    {
                        new EmailRow("Reference", lead.LeadNumber),
                        new EmailRow("Service", lead.ServiceName),
                        new EmailRow("Route", route),
                        new EmailRow("Date", lead.MovingDate?.ToString("d MMMM yyyy") ?? "Not given")
                    },
                    // The secure token link, issued by ICustomerAccessService.
                    CtaLabel = customerAccessUrl is null ? null : "View your request",
                    CtaUrl = customerAccessUrl,
                    Closing = customerAccessUrl is null
                        ? null
                        : "Keep this link private - anyone with it can see your request and quotes."
                }, ct);
        }

        // Internal. Customer contact details are appropriate here.
        await QueueAdminAsync(NotificationType.AdminNewLead,
            "New ShiftingGuru lead received", nameof(Lead), lead.Id,
            new EmailContent
            {
                Heading = "New lead received",
                Intro = $"{lead.CustomerName} submitted a {lead.ServiceName.ToLowerInvariant()} request.",
                Rows = new[]
                {
                    new EmailRow("Reference", lead.LeadNumber),
                    new EmailRow("Service", lead.ServiceName),
                    new EmailRow("Route", route),
                    new EmailRow("Date", lead.MovingDate?.ToString("d MMM yyyy") ?? "Not given"),
                    new EmailRow("Customer", lead.CustomerName),
                    new EmailRow("Phone", lead.Phone),
                    new EmailRow("Email", lead.Email ?? "Not given")
                },
                CtaLabel = "Open lead",
                CtaUrl = _app.Url($"/admin/leads/{lead.Id}")
            }, ct);
    }

    // ---------------------------------------------------------------
    // Lead assigned to a vendor
    // ---------------------------------------------------------------
    public async Task LeadAssignedAsync(Lead lead, Vendor vendor, CancellationToken ct = default)
    {
        // No customer name, phone or email: the vendor sees those after they
        // sign in, not in an email that could be forwarded anywhere.
        await QueueAsync(NotificationType.VendorLeadAssigned, vendor.Email,
            "New lead assigned to your ShiftingGuru account", nameof(LeadAssignment), lead.Id,
            new EmailContent
            {
                Heading = "You've been assigned a new lead",
                Intro = $"A customer requirement matching your services has been passed to {vendor.BusinessName}. "
                      + "Sign in to see the full details and submit a quote.",
                Rows = new[]
                {
                    new EmailRow("Reference", lead.LeadNumber),
                    new EmailRow("Service", lead.ServiceName),
                    new EmailRow("Route", Route(lead)),
                    new EmailRow("Date", lead.MovingDate?.ToString("d MMM yyyy") ?? "Not given")
                },
                CtaLabel = "View lead",
                CtaUrl = _app.Url($"/partner/leads/{lead.Id}")
            }, ct);
    }

    // ---------------------------------------------------------------
    // Quote submitted
    // ---------------------------------------------------------------
    public async Task QuoteSubmittedAsync(Quote quote, Lead lead, Vendor vendor, CancellationToken ct = default)
    {
        await QueueAsync(NotificationType.VendorQuoteSubmitted, vendor.Email,
            "Your quote has been submitted", nameof(Quote), quote.Id,
            new EmailContent
            {
                Heading = "Quote submitted",
                Intro = "Your quote is now with the customer. You can edit it until they make a decision.",
                Rows = new[]
                {
                    new EmailRow("Quote", quote.QuoteNumber),
                    new EmailRow("Lead", lead.LeadNumber),
                    new EmailRow("Service", lead.ServiceName),
                    new EmailRow("Total", Money.Format(quote.TotalAmount)),
                    new EmailRow("Status", "Submitted")
                },
                CtaLabel = "View quote",
                CtaUrl = _app.Url($"/partner/quotes/{quote.Id}")
            }, ct);

        if (!string.IsNullOrWhiteSpace(lead.Email))
        {
            // No price and no vendor name: the customer compares in the portal,
            // where the full picture is available.
            await QueueAsync(NotificationType.CustomerQuoteAvailable, lead.Email!,
                "A new quote is available for your move", nameof(Quote), quote.Id,
                new EmailContent
                {
                    Heading = "A new quote is in",
                    Intro = "One of our partner providers has responded to your request. "
                          + "Sign in to see what they're offering and compare it with any others.",
                    Rows = new[]
                    {
                        new EmailRow("Reference", lead.LeadNumber),
                        new EmailRow("Service", lead.ServiceName),
                        new EmailRow("Route", Route(lead))
                    },
                    CtaLabel = "View your quotes",
                    CtaUrl = _app.Url("/my-request/quotes")
                }, ct);
        }

        await QueueAdminAsync(NotificationType.AdminNewQuote,
            "New vendor quote submitted", nameof(Quote), quote.Id,
            new EmailContent
            {
                Heading = "New quote submitted",
                Intro = $"{vendor.BusinessName} has quoted on {lead.LeadNumber}.",
                Rows = new[]
                {
                    new EmailRow("Quote", quote.QuoteNumber),
                    new EmailRow("Lead", lead.LeadNumber),
                    new EmailRow("Vendor", vendor.BusinessName),
                    new EmailRow("Service", lead.ServiceName),
                    new EmailRow("Route", Route(lead)),
                    new EmailRow("Total", Money.Format(quote.TotalAmount)),
                    new EmailRow("Submitted", quote.CreatedAt.ToString("d MMM yyyy, HH:mm") + " UTC")
                },
                CtaLabel = "Open quote",
                CtaUrl = _app.Url($"/admin/quotes/{quote.Id}")
            }, ct);
    }

    // ---------------------------------------------------------------
    // Customer chose a vendor
    // ---------------------------------------------------------------
    public async Task VendorSelectedAsync(
        Lead lead, Quote winningQuote, Vendor winningVendor,
        IReadOnlyList<(Quote Quote, Vendor Vendor)> otherQuotes, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(lead.Email))
        {
            await QueueAsync(NotificationType.CustomerVendorSelected, lead.Email!,
                "Your ShiftingGuru vendor has been selected", nameof(Lead), lead.Id,
                new EmailContent
                {
                    Heading = "Your provider is confirmed",
                    Intro = $"You've chosen {winningVendor.BusinessName} for your move. "
                          + "They have your requirement and your contact details.",
                    Rows = new[]
                    {
                        new EmailRow("Reference", lead.LeadNumber),
                        new EmailRow("Provider", winningVendor.BusinessName),
                        new EmailRow("Quote", winningQuote.QuoteNumber),
                        new EmailRow("Amount", Money.Format(winningQuote.TotalAmount)),
                        new EmailRow("Estimated transit",
                            winningQuote.EstimatedDeliveryDays.HasValue
                                ? $"{winningQuote.EstimatedDeliveryDays} day(s)"
                                : "To confirm")
                    },
                    CtaLabel = "View your request",
                    CtaUrl = _app.Url("/my-request"),
                    Closing = "Your provider can now get in touch to arrange the details of your move."
                }, ct);
        }

        await QueueAsync(NotificationType.VendorQuoteAccepted, winningVendor.Email,
            "Your quote has been accepted", nameof(Quote), winningQuote.Id,
            new EmailContent
            {
                Heading = "Your quote was accepted",
                Intro = $"{lead.CustomerName} has chosen {winningVendor.BusinessName} for this move. "
                      + "Their contact details are on the lead.",
                Rows = new[]
                {
                    new EmailRow("Quote", winningQuote.QuoteNumber),
                    new EmailRow("Lead", lead.LeadNumber),
                    new EmailRow("Service", lead.ServiceName),
                    new EmailRow("Route", Route(lead)),
                    new EmailRow("Amount", Money.Format(winningQuote.TotalAmount))
                },
                CtaLabel = "View the lead",
                CtaUrl = _app.Url($"/partner/leads/{lead.Id}")
            }, ct);

        foreach (var (quote, vendor) in otherQuotes)
        {
            // No competitor name, no competitor price. Only that it's closed.
            await QueueAsync(NotificationType.VendorQuoteNotSelected, vendor.Email,
                "Update on your ShiftingGuru quote", nameof(Quote), quote.Id,
                new EmailContent
                {
                    Heading = "This lead has been closed",
                    Intro = "The customer has gone ahead with another partner on this request. "
                          + "Thanks for quoting - we'll keep sending you leads that match your services.",
                    Rows = new[]
                    {
                        new EmailRow("Quote", quote.QuoteNumber),
                        new EmailRow("Lead", lead.LeadNumber),
                        new EmailRow("Status", "Not selected")
                    },
                    CtaLabel = "View your quotes",
                    CtaUrl = _app.Url("/partner/quotes")
                }, ct);
        }

        await QueueAdminAsync(NotificationType.AdminLeadConverted,
            "Lead converted", nameof(Lead), lead.Id,
            new EmailContent
            {
                Heading = "Lead converted",
                Intro = $"{lead.CustomerName} selected {winningVendor.BusinessName}.",
                Rows = new[]
                {
                    new EmailRow("Lead", lead.LeadNumber),
                    new EmailRow("Customer", lead.CustomerName),
                    new EmailRow("Vendor", winningVendor.BusinessName),
                    new EmailRow("Quote", winningQuote.QuoteNumber),
                    new EmailRow("Amount", Money.Format(winningQuote.TotalAmount)),
                    new EmailRow("Converted",
                        (lead.ConvertedAt ?? DateTime.UtcNow).ToString("d MMM yyyy, HH:mm") + " UTC")
                },
                CtaLabel = "Open lead",
                CtaUrl = _app.Url($"/admin/leads/{lead.Id}")
            }, ct);
    }

    // ---------------------------------------------------------------
    // Partner lifecycle
    // ---------------------------------------------------------------
    public async Task PartnerRegisteredAsync(Vendor vendor, CancellationToken ct = default)
    {
        await QueueAsync(NotificationType.PartnerRegistered, vendor.Email,
            "Your ShiftingGuru partner application has been received", nameof(Vendor), vendor.Id,
            new EmailContent
            {
                Heading = "Application received",
                Intro = "Thanks for applying to partner with ShiftingGuru. Our team will review your "
                      + "business details and let you know the outcome. You can't sign in until your "
                      + "application is approved.",
                Rows = new[]
                {
                    new EmailRow("Business", vendor.BusinessName),
                    new EmailRow("Partner number", vendor.VendorNumber),
                    new EmailRow("Status", "Pending review")
                }
            }, ct);

        await QueueAdminAsync(NotificationType.AdminNewVendor,
            "New partner application", nameof(Vendor), vendor.Id,
            new EmailContent
            {
                Heading = "New partner application",
                Intro = $"{vendor.BusinessName} has applied to join.",
                Rows = new[]
                {
                    new EmailRow("Partner number", vendor.VendorNumber),
                    new EmailRow("Business", vendor.BusinessName),
                    new EmailRow("Contact", vendor.ContactPerson),
                    new EmailRow("Phone", vendor.Phone),
                    new EmailRow("Email", vendor.Email),
                    new EmailRow("City", vendor.City),
                    new EmailRow("Services", string.Join(", ", vendor.Services.Select(s => s.ServiceName))),
                    new EmailRow("Applied", vendor.CreatedAt.ToString("d MMM yyyy, HH:mm") + " UTC")
                },
                CtaLabel = "Review partner",
                CtaUrl = _app.Url($"/admin/vendors/{vendor.Id}")
            }, ct);
    }

    public async Task PartnerStatusChangedAsync(Vendor vendor, CancellationToken ct = default)
    {
        // Only approval and rejection are announced. Suspension is a
        // conversation, not an automated email.
        if (vendor.Status == VendorStatus.Approved)
        {
            await QueueAsync(NotificationType.PartnerApproved, vendor.Email,
                "Your ShiftingGuru partner account is approved", nameof(Vendor), vendor.Id,
                new EmailContent
                {
                    Heading = "You're approved",
                    Intro = $"{vendor.BusinessName} is now a ShiftingGuru partner. You can sign in and "
                          + "you'll start receiving leads that match your services and locations.",
                    Rows = new[]
                    {
                        new EmailRow("Business", vendor.BusinessName),
                        new EmailRow("Partner number", vendor.VendorNumber),
                        new EmailRow("Status", "Approved")
                    },
                    CtaLabel = "Sign in",
                    CtaUrl = _app.Url("/partner/login"),
                    Closing = "Keep your profile up to date - it's what we match leads against."
                }, ct);
        }
        else if (vendor.Status == VendorStatus.Rejected)
        {
            await QueueAsync(NotificationType.PartnerRejected, vendor.Email,
                "Update on your ShiftingGuru partner application", nameof(Vendor), vendor.Id,
                new EmailContent
                {
                    Heading = "Application not approved",
                    Intro = "We're not able to approve your partner application at this time. "
                          + "If you think this is a mistake, reply to this email and we'll take another look.",
                    Rows = new[]
                    {
                        new EmailRow("Business", vendor.BusinessName),
                        new EmailRow("Partner number", vendor.VendorNumber),
                        new EmailRow("Status", "Not approved")
                    }
                }, ct);
        }
    }

    // ---------------------------------------------------------------
    // Reviews
    // ---------------------------------------------------------------
    public async Task LeadCompletedAsync(Lead lead, Vendor vendor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(lead.Email)) return;

        await QueueAsync(NotificationType.CustomerReviewInvitation, lead.Email!,
            "How was your ShiftingGuru experience?", nameof(Lead), lead.Id,
            new EmailContent
            {
                Heading = "How did your move go?",
                Intro = $"Your move with {vendor.BusinessName} is marked complete. "
                      + "A short review helps other customers choose well, and helps good "
                      + "providers get more work.",
                Rows = new[]
                {
                    new EmailRow("Reference", lead.LeadNumber),
                    new EmailRow("Provider", vendor.BusinessName),
                    new EmailRow("Route", Route(lead))
                },
                CtaLabel = "Leave a review",
                CtaUrl = _app.Url("/my-request/review")
            }, ct);
    }

    public async Task ReviewSubmittedAsync(
        Review review, Lead lead, Vendor vendor, CancellationToken ct = default)
    {
        var preview = review.Comment.Length > 160 ? review.Comment[..160] + "..." : review.Comment;

        await QueueAdminAsync(NotificationType.AdminNewReview,
            "New customer review awaiting moderation", nameof(Review), review.Id,
            new EmailContent
            {
                Heading = "A review needs moderating",
                Intro = $"{lead.CustomerName} has reviewed {vendor.BusinessName}.",
                Rows = new[]
                {
                    new EmailRow("Lead", lead.LeadNumber),
                    new EmailRow("Vendor", vendor.BusinessName),
                    new EmailRow("Rating", $"{review.Rating} out of 5"),
                    new EmailRow("Title", review.Title ?? "No title"),
                    new EmailRow("Comment", preview)
                },
                CtaLabel = "Moderate review",
                CtaUrl = _app.Url($"/admin/reviews/{review.Id}")
            }, ct);
    }

    public async Task ReviewPublishedAsync(Review review, Vendor vendor, CancellationToken ct = default)
    {
        var preview = review.Comment.Length > 200 ? review.Comment[..200] + "..." : review.Comment;

        // No moderation note, no lead contact details - just the published review.
        await QueueAsync(NotificationType.VendorReviewPublished, vendor.Email,
            "A new customer review has been published", nameof(Review), review.Id,
            new EmailContent
            {
                Heading = "You have a new published review",
                Intro = "A customer review of your business is now live on ShiftingGuru.",
                Rows = new[]
                {
                    new EmailRow("Rating", $"{review.Rating} out of 5"),
                    new EmailRow("Title", review.Title ?? "No title"),
                    new EmailRow("Review", preview)
                },
                CtaLabel = "View your reviews",
                CtaUrl = _app.Url("/partner/reviews")
            }, ct);
    }

    // ---------------------------------------------------------------
    // Plumbing
    // ---------------------------------------------------------------
    private Task QueueAdminAsync(
        NotificationType type, string subject, string entityType, int entityId,
        EmailContent content, CancellationToken ct)
    {
        var recipients = _email.AdminRecipientList;

        if (recipients.Count == 0)
        {
            _logger.LogWarning(
                "No admin notification recipients configured; skipping {Type}.", type);
            return Task.CompletedTask;
        }

        return Task.WhenAll(recipients.Select(r =>
            QueueAsync(type, r, subject, entityType, entityId, content, ct)));
    }

    /// <summary>
    /// Writes the row, then hands the id to the worker. Nothing here throws:
    /// a notification problem must never undo a business action.
    /// </summary>
    private async Task QueueAsync(
        NotificationType type, string recipient, string subject,
        string entityType, int entityId, EmailContent content, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(recipient)) return;

        try
        {
            // Idempotency: same event, same recipient, already recorded. Stops
            // a refreshed page or retried POST sending a second copy.
            var exists = await _db.NotificationLogs.AnyAsync(n =>
                n.Type == type &&
                n.EntityType == entityType &&
                n.EntityId == entityId &&
                n.Recipient == recipient, ct);

            if (exists) return;

            var notification = new NotificationLog
            {
                Type = type,
                Recipient = recipient,
                Subject = subject,
                HtmlBody = _template.RenderHtml(content),
                TextBody = _template.RenderText(content),
                EntityType = entityType,
                EntityId = entityId,
                Status = NotificationStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _db.NotificationLogs.Add(notification);
            await _db.SaveChangesAsync(ct);

            _queue.Enqueue(notification.Id);
        }
        catch (DbUpdateException)
        {
            // The unique index caught a race between two identical events.
            _logger.LogInformation("Duplicate {Type} notification suppressed.", type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Couldn't queue a {Type} notification.", type);
        }
    }

    private static string Route(Lead lead) =>
        lead.MovingFrom is null
            ? lead.StorageLocation ?? "-"
            : $"{lead.MovingFrom} to {lead.MovingTo}";
}