using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services;

namespace ShiftingGuru.Controllers.Api.V1;

/// <summary>
/// NEW (mobile API): the customer's quotes, choosing a partner, and bookings.
///
/// Uses the website's own QuoteSelectionService for everything:
/// - which quotes a customer may see (the same statuses as /my-request/quotes)
/// - choosing one: one database transaction that converts the request,
///   accepts the chosen quote, marks the others "not selected" and emails
///   everyone - and if two taps arrive at once, exactly one wins.
///
/// Every lookup is tied to CurrentCustomer.Id through the request.
/// </summary>
[Route("api/v1/customer")]
public class CustomerQuotesController : ApiCustomerControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IQuoteSelectionService _selection;
    private readonly IReviewService _reviews;

    public CustomerQuotesController(
        ApplicationDbContext db, IQuoteSelectionService selection, IReviewService reviews)
    {
        _db = db;
        _selection = selection;
        _reviews = reviews;
    }

    // GET /api/v1/customer/requests/42/quotes
    [HttpGet("requests/{requestId:int}/quotes")]
    public async Task<IActionResult> QuotesForRequest(int requestId, CancellationToken ct)
    {
        var lead = await OwnLeadAsync(requestId, ct);
        if (lead is null) return NotFound(new ApiError("That request couldn't be found.", "notFound"));

        // Cheapest first - the website's own rule for what a customer may see.
        var quotes = await _selection.GetCustomerVisibleQuotesAsync(lead.Id, ct);

        var result = new List<CustomerQuoteDto>();
        foreach (var quote in quotes)
        {
            result.Add(await ToDtoAsync(quote, lead, ct));
        }

        return Ok(result);
    }

    // GET /api/v1/customer/quotes/7
    [HttpGet("quotes/{quoteId:int}")]
    public async Task<IActionResult> GetQuote(int quoteId, CancellationToken ct)
    {
        var lead = await LeadOfQuoteAsync(quoteId, ct);
        if (lead is null) return NotFound(new ApiError("That quote isn't available.", "notFound"));

        var quote = await _selection.GetCustomerVisibleQuoteAsync(lead.Id, quoteId, ct);
        if (quote is null) return NotFound(new ApiError("That quote isn't available.", "notFound"));

        return Ok(await ToDtoAsync(quote, lead, ct));
    }

    // POST /api/v1/customer/quotes/7/select
    [HttpPost("quotes/{quoteId:int}/select")]
    public async Task<IActionResult> Select(int quoteId, CancellationToken ct)
    {
        var lead = await LeadOfQuoteAsync(quoteId, ct);
        if (lead is null) return NotFound(new ApiError("That quote isn't available.", "notFound"));

        // The website's own selection, unchanged.
        var result = await _selection.SelectAsync(lead.Id, quoteId, ct);
        if (!result.Succeeded)
        {
            return Conflict(new ApiError(result.Error ?? "That quote can't be chosen any more.", "notSelectable"));
        }

        var booking = await BookingQuery()
            .FirstOrDefaultAsync(l => l.Id == lead.Id, ct);

        return booking is null
            ? StatusCode(StatusCodes.Status500InternalServerError,
                new ApiError("Your choice was saved, but we couldn't load the booking. Pull down to refresh.", "reloadNeeded"))
            : Ok(ToBooking(booking));
    }

    // GET /api/v1/customer/bookings
    [HttpGet("bookings")]
    public async Task<IActionResult> Bookings(CancellationToken ct)
    {
        var bookings = await BookingQuery()
            .Where(l => l.Status == LeadStatus.Converted || l.Status == LeadStatus.Completed)
            .OrderByDescending(l => l.ConvertedAt)
            .Take(50)
            .ToListAsync(ct);

        return Ok(bookings.Select(ToBooking).ToList());
    }

    // GET /api/v1/customer/bookings/42
    [HttpGet("bookings/{id:int}")]
    public async Task<IActionResult> GetBooking(int id, CancellationToken ct)
    {
        var booking = await BookingQuery()
            .Where(l => l.Status == LeadStatus.Converted || l.Status == LeadStatus.Completed)
            .FirstOrDefaultAsync(l => l.Id == id, ct);

        return booking is null
            ? NotFound(new ApiError("That booking couldn't be found.", "notFound"))
            : Ok(ToBooking(booking));
    }

    // -----------------------------------------------------------------

    private Task<Lead?> OwnLeadAsync(int leadId, CancellationToken ct) =>
        _db.Leads.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == leadId && l.CustomerId == CurrentCustomer.Id, ct);

    /// <summary>The request a quote belongs to - only if it's this customer's.</summary>
    private Task<Lead?> LeadOfQuoteAsync(int quoteId, CancellationToken ct) =>
        _db.Quotes.AsNoTracking()
            .Where(q => q.Id == quoteId && q.Lead!.CustomerId == CurrentCustomer.Id)
            .Select(q => q.Lead)
            .FirstOrDefaultAsync(ct);

    /// <summary>This customer's requests where a partner was chosen.</summary>
    private IQueryable<Lead> BookingQuery() =>
        _db.Leads.AsNoTracking()
            .Include(l => l.SelectedVendor)
            .Include(l => l.SelectedQuote)
            .Where(l => l.CustomerId == CurrentCustomer.Id
                     && l.SelectedVendorId != null
                     && l.SelectedQuoteId != null);

    private async Task<CustomerQuoteDto> ToDtoAsync(Quote quote, Lead lead, CancellationToken ct)
    {
        var vendor = quote.Vendor!;
        var rating = await _reviews.GetVendorSummaryAsync(vendor.Id, ct);

        return new CustomerQuoteDto(
            quote.Id,
            lead.Id,
            quote.QuoteNumber,
            new CustomerQuoteVendorDto(
                vendor.Id,
                vendor.BusinessName,
                vendor.Status == VendorStatus.Approved,   // documents checked by the team
                rating.AverageRating,
                rating.ApprovedCount,
                vendor.YearsOfExperience),
            CustomerViews.LinesOf(quote),
            quote.TotalAmount,
            quote.EstimatedDeliveryDays,
            quote.EstimatedPickupDate,
            quote.EstimatedDeliveryDate,
            quote.VendorNotes,
            CustomerViews.QuoteStatus(quote.Status),
            Selectable: !lead.IsConverted
                     && lead.Status != LeadStatus.Completed
                     && Models.Quote.SelectableStatuses.Contains(quote.Status),
            quote.CreatedAt);
    }

    private static CustomerBookingDto ToBooking(Lead lead) => new(
        lead.Id,
        lead.LeadNumber,
        lead.SelectedQuote!.Id,
        lead.SelectedQuote.QuoteNumber,
        lead.ServiceSlug,
        lead.ServiceName,
        lead.MovingFrom,
        lead.MovingTo,
        lead.StorageLocation,
        lead.MovingDate,
        lead.SelectedQuote.TotalAmount,
        lead.Status == LeadStatus.Completed ? "completed" : "confirmed",
        lead.SelectedVendor!.BusinessName,
        lead.SelectedVendor.Phone,
        lead.UpdatedAt ?? lead.ConvertedAt ?? lead.CreatedAt);
}