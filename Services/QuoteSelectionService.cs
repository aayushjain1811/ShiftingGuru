using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services.Notifications;

namespace ShiftingGuru.Services;

public class QuoteSelectionService : IQuoteSelectionService
{
    private readonly ApplicationDbContext _db;
    private readonly INotificationService _notifications;
    private readonly ILogger<QuoteSelectionService> _logger;

    public QuoteSelectionService(
        ApplicationDbContext db,
        INotificationService notifications,
        ILogger<QuoteSelectionService> logger)
    {
        _db = db;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Quote>> GetCustomerVisibleQuotesAsync(
        int leadId, CancellationToken ct = default) =>
        await _db.Quotes
            .AsNoTracking()
            .Include(q => q.Vendor!)
                .ThenInclude(v => v.Services)
            .Where(q => q.LeadId == leadId && Quote.CustomerVisibleStatuses.Contains(q.Status))
            .OrderBy(q => q.TotalAmount)
            .ToListAsync(ct);

    public async Task<Quote?> GetCustomerVisibleQuoteAsync(
        int leadId, int quoteId, CancellationToken ct = default) =>
        // The lead filter is part of the lookup, so another customer's quote
        // is indistinguishable from one that doesn't exist.
        await _db.Quotes
            .AsNoTracking()
            .Include(q => q.Vendor!)
                .ThenInclude(v => v.Services)
            .FirstOrDefaultAsync(q =>
                q.Id == quoteId &&
                q.LeadId == leadId &&
                Quote.CustomerVisibleStatuses.Contains(q.Status), ct);

    public async Task<SelectionResult> SelectAsync(
        int leadId, int quoteId, CancellationToken ct = default)
    {
        // Quote must belong to this lead and still be selectable. Checked
        // against the database, never against anything the browser sent.
        var quote = await _db.Quotes
            .AsNoTracking()
            .Include(q => q.Vendor)
            .FirstOrDefaultAsync(q => q.Id == quoteId && q.LeadId == leadId, ct);

        if (quote is null) return SelectionResult.Fail("That quote couldn't be found.");

        if (!Quote.SelectableStatuses.Contains(quote.Status))
        {
            return SelectionResult.Fail("That quote is no longer available to choose.");
        }

        if (quote.Vendor is null || quote.Vendor.Status != VendorStatus.Approved)
        {
            return SelectionResult.Fail("That provider is no longer available.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        try
        {
            // The whole race is decided here. UPDATE ... WHERE Status <>
            // 'Converted' is atomic in PostgreSQL, so of two simultaneous
            // requests exactly one updates a row and the other gets zero.
            var converted = await _db.Leads
                .Where(l => l.Id == leadId && l.Status != LeadStatus.Converted)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(l => l.Status, LeadStatus.Converted)
                    .SetProperty(l => l.SelectedVendorId, quote.VendorId)
                    .SetProperty(l => l.SelectedQuoteId, quote.Id)
                    .SetProperty(l => l.ConvertedAt, DateTime.UtcNow)
                    .SetProperty(l => l.UpdatedAt, DateTime.UtcNow), ct);

            if (converted == 0)
            {
                await transaction.RollbackAsync(ct);
                return SelectionResult.Fail("This request has already been completed.");
            }

            // The winner.
            await _db.Quotes
                .Where(q => q.Id == quoteId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(q => q.Status, QuoteStatus.Accepted)
                    .SetProperty(q => q.UpdatedAt, DateTime.UtcNow), ct);

            // Everyone else who was still in the running. NotSelected rather
            // than Rejected: nothing was wrong with their quote.
            await _db.Quotes
                .Where(q => q.LeadId == leadId
                         && q.Id != quoteId
                         && Quote.SelectableStatuses.Contains(q.Status))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(q => q.Status, QuoteStatus.NotSelected)
                    .SetProperty(q => q.UpdatedAt, DateTime.UtcNow), ct);

            await transaction.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Vendor selection failed for lead {LeadId}", leadId);
            return SelectionResult.Fail("Something went wrong completing your choice. Please try again.");
        }

        // Only after the transaction has committed. The losing vendors are
        // read back from the database rather than assumed.
        var lead = await _db.Leads.AsNoTracking().FirstOrDefaultAsync(l => l.Id == leadId, ct);

        var others = await _db.Quotes
            .AsNoTracking()
            .Include(q => q.Vendor)
            .Where(q => q.LeadId == leadId
                     && q.Id != quoteId
                     && q.Status == QuoteStatus.NotSelected)
            .ToListAsync(ct);

        if (lead is not null)
        {
            await _notifications.VendorSelectedAsync(
                lead, quote, quote.Vendor,
                others.Where(o => o.Vendor is not null)
                      .Select(o => (o, o.Vendor!))
                      .ToList(), ct);
        }

        return SelectionResult.Ok(quote);
    }
}