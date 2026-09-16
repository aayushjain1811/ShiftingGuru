using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services.Notifications;
using ShiftingGuru.ViewModels.Partner;

namespace ShiftingGuru.Services;

public class QuoteService : IQuoteService
{
    private readonly ApplicationDbContext _db;
    private readonly INotificationService _notifications;
    private readonly ILogger<QuoteService> _logger;

    public QuoteService(
        ApplicationDbContext db,
        INotificationService notifications,
        ILogger<QuoteService> logger)
    {
        _db = db;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<QuoteResult> SubmitAsync(
        int leadId, int vendorId, SubmitQuoteViewModel model, CancellationToken ct = default)
    {
        // The assignment IS the authorization. No assignment, no quote - and
        // the check is a query, not a trusted form field.
        var assigned = await _db.LeadAssignments
            .AsNoTracking()
            .AnyAsync(a => a.LeadId == leadId
                        && a.VendorId == vendorId
                        && a.Status != AssignmentStatus.Cancelled, ct);

        if (!assigned)
        {
            return QuoteResult.Fail("This lead isn't assigned to your business.");
        }

        var existing = await _db.Quotes
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.LeadId == leadId
                                   && q.VendorId == vendorId
                                   && Quote.ActiveStatuses.Contains(q.Status), ct);

        if (existing is not null)
        {
            return QuoteResult.Fail("You have already submitted a quote for this lead.");
        }

        var quote = new Quote
        {
            QuoteNumber = await NextQuoteNumberAsync(ct),
            LeadId = leadId,
            VendorId = vendorId,

            BasePrice = model.BasePrice,
            PackingCharges = model.PackingCharges,
            TransportationCharges = model.TransportationCharges,
            LoadingUnloadingCharges = model.LoadingUnloadingCharges,
            AdditionalCharges = model.AdditionalCharges,

            EstimatedPickupDate = model.EstimatedPickupDate,
            EstimatedDeliveryDate = model.EstimatedDeliveryDate,
            EstimatedDeliveryDays = model.EstimatedDeliveryDays,
            VendorNotes = string.IsNullOrWhiteSpace(model.VendorNotes) ? null : model.VendorNotes.Trim(),

            Status = QuoteStatus.Submitted,
            CreatedAt = DateTime.UtcNow
        };

        // Recomputed here, never read from the request.
        quote.TotalAmount = CalculateTotal(quote);

        _db.Quotes.Add(quote);

        await PromoteLeadIfAppropriateAsync(leadId, ct);

        try
        {
            await _db.SaveChangesAsync(ct);

            // After the save. Vendor confirmation, customer alert, admin copy.
            await NotifyQuoteSubmittedAsync(quote, ct);

            return QuoteResult.Ok(quote);
        }
        catch (DbUpdateException ex)
        {
            // The filtered unique index catches a race where two requests
            // slip past the check above at the same moment.
            _logger.LogError(ex, "Quote insert failed for lead {LeadId}", leadId);
            return QuoteResult.Fail("You have already submitted a quote for this lead.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Quote insert failed for lead {LeadId}", leadId);
            return QuoteResult.Fail("Couldn't save your quote. Please try again.");
        }
    }

    public async Task<QuoteResult> UpdateAsync(
        int quoteId, int vendorId, SubmitQuoteViewModel model, CancellationToken ct = default)
    {
        // Ownership is part of the lookup: another vendor's quote is simply
        // not found here.
        var quote = await _db.Quotes
            .FirstOrDefaultAsync(q => q.Id == quoteId && q.VendorId == vendorId, ct);

        if (quote is null) return QuoteResult.Fail("That quote couldn't be found.");

        if (!quote.IsEditableByVendor)
        {
            return QuoteResult.Fail($"A {quote.Status} quote can no longer be edited.");
        }

        quote.BasePrice = model.BasePrice;
        quote.PackingCharges = model.PackingCharges;
        quote.TransportationCharges = model.TransportationCharges;
        quote.LoadingUnloadingCharges = model.LoadingUnloadingCharges;
        quote.AdditionalCharges = model.AdditionalCharges;

        quote.EstimatedPickupDate = model.EstimatedPickupDate;
        quote.EstimatedDeliveryDate = model.EstimatedDeliveryDate;
        quote.EstimatedDeliveryDays = model.EstimatedDeliveryDays;
        quote.VendorNotes = string.IsNullOrWhiteSpace(model.VendorNotes) ? null : model.VendorNotes.Trim();

        quote.TotalAmount = CalculateTotal(quote);
        quote.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync(ct);
            return QuoteResult.Ok(quote);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Quote update failed for quote {QuoteId}", quoteId);
            return QuoteResult.Fail("Couldn't save your changes. Please try again.");
        }
    }

    public async Task<bool> ChangeStatusAsync(int quoteId, QuoteStatus status, CancellationToken ct = default)
    {
        var quote = await _db.Quotes.FirstOrDefaultAsync(q => q.Id == quoteId, ct);
        if (quote is null) return false;

        if (!Enum.IsDefined(status) || !AllowedTransitionsFrom(quote.Status).Contains(status))
        {
            return false;
        }

        quote.Status = status;
        quote.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Quote status change failed for quote {QuoteId}", quoteId);
            return false;
        }
    }

    /// <summary>
    /// Deliberately one-directional out of the terminal states: a rejected
    /// quote can be reopened for review, but nothing jumps straight back to
    /// Submitted as though the decision never happened.
    /// </summary>
    public IReadOnlyList<QuoteStatus> AllowedTransitionsFrom(QuoteStatus current) => current switch
    {
        QuoteStatus.Draft => new[] { QuoteStatus.Submitted, QuoteStatus.Cancelled },
        QuoteStatus.Submitted => new[] { QuoteStatus.UnderReview, QuoteStatus.Accepted, QuoteStatus.Rejected, QuoteStatus.Expired, QuoteStatus.Cancelled },
        QuoteStatus.UnderReview => new[] { QuoteStatus.Accepted, QuoteStatus.Rejected, QuoteStatus.Expired, QuoteStatus.Cancelled },
        QuoteStatus.Accepted => new[] { QuoteStatus.Cancelled },
        QuoteStatus.Rejected => new[] { QuoteStatus.UnderReview },
        QuoteStatus.Expired => new[] { QuoteStatus.UnderReview },
        QuoteStatus.Cancelled => Array.Empty<QuoteStatus>(),
        _ => Array.Empty<QuoteStatus>()
    };

    private async Task NotifyQuoteSubmittedAsync(Quote quote, CancellationToken ct)
    {
        var lead = await _db.Leads.AsNoTracking().FirstOrDefaultAsync(l => l.Id == quote.LeadId, ct);
        var vendor = await _db.Vendors.AsNoTracking().FirstOrDefaultAsync(v => v.Id == quote.VendorId, ct);

        if (lead is null || vendor is null) return;

        await _notifications.QuoteSubmittedAsync(quote, lead, vendor, ct);
    }

    private static decimal CalculateTotal(Quote quote) =>
        quote.BasePrice
        + quote.PackingCharges
        + quote.TransportationCharges
        + quote.LoadingUnloadingCharges
        + quote.AdditionalCharges;

    /// <summary>
    /// Moves the lead to Quoted when the first quote lands - but only from a
    /// status where that makes sense. A Closed or Converted lead is left alone.
    /// </summary>
    private async Task PromoteLeadIfAppropriateAsync(int leadId, CancellationToken ct)
    {
        var lead = await _db.Leads.FirstOrDefaultAsync(l => l.Id == leadId, ct);
        if (lead is null) return;

        var promotable = lead.Status is LeadStatus.New
            or LeadStatus.Contacted or LeadStatus.Assigned or LeadStatus.InProgress;

        if (!promotable) return;

        lead.Status = LeadStatus.Quoted;
        lead.UpdatedAt = DateTime.UtcNow;
    }

    private async Task<string> NextQuoteNumberAsync(CancellationToken ct)
    {
        var next = await _db.Database
            .SqlQueryRaw<long>("SELECT nextval('quote_number_seq') AS \"Value\"")
            .SingleAsync(ct);

        return $"SG-Q-{DateTime.UtcNow:yyyyMMdd}-{next:D5}";
    }
}