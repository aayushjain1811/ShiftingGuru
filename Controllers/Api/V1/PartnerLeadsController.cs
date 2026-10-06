using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services;
using ShiftingGuru.ViewModels.Partner;

namespace ShiftingGuru.Controllers.Api.V1;

/// <summary>
/// NEW (mobile API): the partner's leads - the list, one lead's details,
/// accept and reject.
///
/// A lead reaches a partner only when an admin assigns it on the website
/// (LeadAssignments). Every query here is filtered by CurrentVendor.Id, so a
/// lead assigned to someone else simply isn't found.
///
/// The app's flow:  new -> accept -> quote   (or new -> reject)
/// Customer contact details stay masked until the partner accepts.
///
/// Quotes: submitted and updated through the website's own QuoteService, so
/// the same checks, the same server-side total and the same emails (partner
/// confirmation, customer alert, admin copy) apply as on the website.
///
/// The website's partner pages are unchanged and work as before.
/// </summary>
[Route("api/v1/partner/leads")]
public class PartnerLeadsController : ApiPartnerControllerBase
{
    private const int MaxListSize = 50;

    private static readonly ApiError LeadNotFound = new(
        "This lead isn't available. It may have been withdrawn.", "notFound");

    private static readonly ApiError NotAvailable = new(
        "This lead can't be changed any more. Pull down to refresh your list.", "notAvailable");

    private static readonly ApiError AcceptFirst = new(
        "Accept the lead before sending a quote.", "acceptFirst");

    private static readonly ApiError NoQuote = new(
        "You haven't sent a quote for this lead yet.", "noQuote");

    // India has no daylight saving, so a fixed offset is exact.
    private static readonly TimeSpan IndiaOffset = TimeSpan.FromHours(5.5);

    private readonly ApplicationDbContext _db;
    private readonly IQuoteService _quotes;
    private readonly ILogger<PartnerLeadsController> _logger;

    public PartnerLeadsController(
        ApplicationDbContext db,
        IQuoteService quotes,
        ILogger<PartnerLeadsController> logger)
    {
        _db = db;
        _quotes = quotes;
        _logger = logger;
    }

    // GET /api/v1/partner/leads?tab=new|accepted|quoted|expired
    [HttpGet("")]
    public async Task<IActionResult> List([FromQuery] string? tab, CancellationToken ct)
    {
        var vendorId = CurrentVendor.Id;
        var selected = (tab ?? "new").Trim().ToLowerInvariant();

        if (selected is not ("new" or "accepted" or "quoted" or "expired"))
        {
            return BadRequest(new ApiError("Unknown tab. Use new, accepted, quoted or expired.", "badTab"));
        }

        // One shape with everything the tabs need to decide, worked out in SQL.
        var rows = _db.LeadAssignments
            .AsNoTracking()
            .Where(a => a.VendorId == vendorId
                     && a.Status != AssignmentStatus.Cancelled
                     && a.Status != AssignmentStatus.Declined)
            .Select(a => new LeadRow
            {
                Status = a.Status,
                AssignedAt = a.AssignedAt,
                Lead = a.Lead!,

                HasActiveQuote = _db.Quotes.Any(q =>
                    q.LeadId == a.LeadId
                    && q.VendorId == vendorId
                    && Quote.ActiveStatuses.Contains(q.Status)),

                // Closed for this partner: expired, closed by an admin, or the
                // customer chose a different partner.
                Closed = a.Status == AssignmentStatus.Expired
                      || a.Lead!.Status == LeadStatus.Closed
                      || a.Lead.Status == LeadStatus.Cancelled
                      || (a.Lead.SelectedVendorId != null && a.Lead.SelectedVendorId != vendorId),

                // Won: the customer chose this partner. These belong in Bookings.
                Won = a.Lead!.SelectedVendorId == vendorId
            });

        rows = selected switch
        {
            "new" => rows.Where(r => !r.Closed && !r.Won && !r.HasActiveQuote
                                  && (r.Status == AssignmentStatus.Assigned || r.Status == AssignmentStatus.Viewed)),
            "accepted" => rows.Where(r => !r.Closed && !r.Won && !r.HasActiveQuote
                                       && r.Status == AssignmentStatus.Accepted),
            "quoted" => rows.Where(r => !r.Closed && !r.Won && r.HasActiveQuote),
            _ => rows.Where(r => r.Closed)   // "expired"
        };

        var found = await rows
            .OrderByDescending(r => r.AssignedAt)
            .Take(MaxListSize)
            .ToListAsync(ct);

        // Lists never include customer contact details.
        var result = found
            .Select(r => new PartnerLeadSummaryDto(
                r.Lead.Id, r.Lead.LeadNumber, r.Lead.ServiceSlug, r.Lead.ServiceName,
                r.Lead.MovingFrom, r.Lead.MovingTo, r.Lead.StorageLocation, r.Lead.MovingDate,
                selected, r.AssignedAt, PartnerLeadSummary.For(r.Lead)))
            .ToList();

        return Ok(result);
    }

    // GET /api/v1/partner/leads/42
    [HttpGet("{leadId:int}")]
    public async Task<IActionResult> Details(int leadId, CancellationToken ct)
    {
        var assignment = await LoadAssignmentAsync(leadId, ct);
        if (assignment?.Lead is null) return NotFound(LeadNotFound);

        // Marked viewed only now, after the ownership check - like the website.
        if (assignment.Status == AssignmentStatus.Assigned)
        {
            var now = DateTime.UtcNow;
            assignment.Status = AssignmentStatus.Viewed;
            assignment.ViewedAt = now;
            assignment.UpdatedAt = now;

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                // Not worth failing the screen over - the partner still gets the lead.
                _logger.LogError(ex, "Couldn't mark assignment {AssignmentId} as viewed", assignment.Id);
            }
        }

        return Ok(await BuildDetailsAsync(assignment, ct));
    }

    // POST /api/v1/partner/leads/42/accept
    [HttpPost("{leadId:int}/accept")]
    public async Task<IActionResult> Accept(int leadId, CancellationToken ct)
    {
        var assignment = await LoadAssignmentAsync(leadId, ct);
        if (assignment?.Lead is null) return NotFound(LeadNotFound);

        // Pressing Accept twice is harmless.
        if (assignment.Status == AssignmentStatus.Accepted)
        {
            return Ok(await BuildDetailsAsync(assignment, ct));
        }

        var state = await StateOfAsync(assignment, ct);
        if (!state.CanAccept) return Conflict(NotAvailable);

        var now = DateTime.UtcNow;
        assignment.Status = AssignmentStatus.Accepted;
        assignment.ViewedAt ??= now;
        assignment.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);

        return Ok(await BuildDetailsAsync(assignment, ct));
    }

    // POST /api/v1/partner/leads/42/reject
    [HttpPost("{leadId:int}/reject")]
    public async Task<IActionResult> Reject(int leadId, CancellationToken ct)
    {
        var assignment = await LoadAssignmentAsync(leadId, ct);
        if (assignment?.Lead is null) return NotFound(LeadNotFound);

        if (assignment.Status == AssignmentStatus.Declined)
        {
            return Ok(new ApiMessage("Lead rejected."));
        }

        var state = await StateOfAsync(assignment, ct);
        if (!state.CanReject) return Conflict(NotAvailable);

        assignment.Status = AssignmentStatus.Declined;
        assignment.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Ok(new ApiMessage("Lead rejected."));
    }

    // GET /api/v1/partner/leads/42/quote
    // This partner's latest quote on the lead, to fill the form when updating.
    [HttpGet("{leadId:int}/quote")]
    public async Task<IActionResult> GetQuote(int leadId, CancellationToken ct)
    {
        var assignment = await LoadAssignmentAsync(leadId, ct);
        if (assignment?.Lead is null) return NotFound(LeadNotFound);

        var quote = await _db.Quotes
            .AsNoTracking()
            .Where(q => q.LeadId == leadId && q.VendorId == CurrentVendor.Id)
            .OrderByDescending(q => q.CreatedAt)
            .FirstOrDefaultAsync(ct);

        return quote is null ? NotFound(NoQuote) : Ok(PartnerQuoteDto.From(quote));
    }

    // POST /api/v1/partner/leads/42/quote
    [HttpPost("{leadId:int}/quote")]
    public async Task<IActionResult> SubmitQuote(int leadId, [FromBody] PartnerQuoteRequest request, CancellationToken ct)
    {
        var assignment = await LoadAssignmentAsync(leadId, ct);
        if (assignment?.Lead is null) return NotFound(LeadNotFound);

        var state = await StateOfAsync(assignment, ct);

        if (state.Quote is not null && Quote.ActiveStatuses.Contains(state.Quote.Status))
        {
            return Conflict(new ApiError("You've already sent a quote for this lead. Update it instead.", "alreadyQuoted"));
        }

        if (!state.CanQuote)
        {
            return Conflict(state.Status == "new" ? AcceptFirst : NotAvailable);
        }

        if (CheckQuote(request) is { } problem) return BadRequest(problem);

        // The website's own service: re-checks the assignment, recalculates
        // the total, moves the lead to Quoted and sends the emails.
        var result = await _quotes.SubmitAsync(leadId, CurrentVendor.Id, ToModel(request), ct);

        return result.Succeeded
            ? Ok(PartnerQuoteDto.From(result.Quote!))
            : BadRequest(new ApiError(result.Error ?? "Couldn't send your quote. Please try again.", "quoteFailed"));
    }

    // PUT /api/v1/partner/leads/42/quote
    [HttpPut("{leadId:int}/quote")]
    public async Task<IActionResult> UpdateQuote(int leadId, [FromBody] PartnerQuoteRequest request, CancellationToken ct)
    {
        var assignment = await LoadAssignmentAsync(leadId, ct);
        if (assignment?.Lead is null) return NotFound(LeadNotFound);

        var state = await StateOfAsync(assignment, ct);
        var quote = state.Quote;

        if (quote is null || !Quote.ActiveStatuses.Contains(quote.Status)) return NotFound(NoQuote);

        if (!state.CanQuote || !quote.IsEditableByVendor)
        {
            return Conflict(new ApiError("This quote can no longer be changed.", "notEditable"));
        }

        if (CheckQuote(request) is { } problem) return BadRequest(problem);

        var result = await _quotes.UpdateAsync(quote.Id, CurrentVendor.Id, ToModel(request), ct);

        return result.Succeeded
            ? Ok(PartnerQuoteDto.From(result.Quote!))
            : BadRequest(new ApiError(result.Error ?? "Couldn't save your changes. Please try again.", "quoteFailed"));
    }

    // -----------------------------------------------------------------

    /// <summary>This partner's assignment for the lead, tracked so it can be changed. Null if not theirs.</summary>
    private Task<LeadAssignment?> LoadAssignmentAsync(int leadId, CancellationToken ct) =>
        _db.LeadAssignments
            .Include(a => a.Lead)
            .FirstOrDefaultAsync(a => a.LeadId == leadId
                                   && a.VendorId == CurrentVendor.Id
                                   && a.Status != AssignmentStatus.Cancelled, ct);

    /// <summary>Works out what this partner may do with the lead right now.</summary>
    private async Task<LeadState> StateOfAsync(LeadAssignment assignment, CancellationToken ct)
    {
        var lead = assignment.Lead!;
        var vendorId = CurrentVendor.Id;

        var quote = await _db.Quotes
            .AsNoTracking()
            .Where(q => q.LeadId == lead.Id && q.VendorId == vendorId)
            .OrderByDescending(q => q.CreatedAt)
            .FirstOrDefaultAsync(ct);

        var hasActiveQuote = quote is not null && Quote.ActiveStatuses.Contains(quote.Status);
        var won = lead.SelectedVendorId == vendorId;

        var closed = assignment.Status == AssignmentStatus.Expired
                  || lead.Status is LeadStatus.Closed or LeadStatus.Cancelled
                  || (lead.SelectedVendorId is not null && !won);

        var open = !closed && !won;
        var waiting = assignment.Status is AssignmentStatus.Assigned or AssignmentStatus.Viewed;
        var accepted = assignment.Status == AssignmentStatus.Accepted;
        var declined = assignment.Status == AssignmentStatus.Declined;

        var status =
            closed ? "expired" :
            declined ? "rejected" :
            hasActiveQuote || won ? "quoted" :
            accepted ? "accepted" :
            "new";

        return new LeadState(
            Quote: quote,
            Status: status,
            // Contact details: once accepted, once quoted (including quotes sent
            // from the website), or once the customer chose this partner.
            ContactVisible: !closed && !declined && (accepted || hasActiveQuote || won),
            CanAccept: open && waiting && !hasActiveQuote,
            CanReject: open && (waiting || accepted) && !hasActiveQuote,
            CanQuote: open && ((accepted && !hasActiveQuote)
                            || (hasActiveQuote && quote!.IsEditableByVendor)));
    }

    private async Task<PartnerLeadDetailsDto> BuildDetailsAsync(LeadAssignment assignment, CancellationToken ct)
    {
        var lead = assignment.Lead!;
        var state = await StateOfAsync(assignment, ct);

        var customer = state.ContactVisible
            ? new PartnerLeadCustomerDto(
                true,
                lead.CustomerName,
                lead.Phone,
                lead.Email,
                lead.MovingFrom ?? lead.StorageLocation,
                lead.PreferredContactMethod)
            : new PartnerLeadCustomerDto(
                false,
                Review.ToDisplayName(lead.CustomerName),   // "Aayush Jain" -> "Aayush J."
                MaskPhone(lead.Phone),
                null,
                lead.MovingFrom ?? lead.StorageLocation,
                null);

        var quote = state.Quote is null
            ? null
            : new PartnerLeadQuoteDto(
                state.Quote.Id,
                state.Quote.QuoteNumber,
                state.Quote.TotalAmount,
                state.Quote.Status.ToString(),
                state.Quote.IsEditableByVendor);

        return new PartnerLeadDetailsDto(
            lead.Id, lead.LeadNumber, lead.ServiceSlug, lead.ServiceName,
            lead.MovingFrom, lead.MovingTo, lead.StorageLocation, lead.MovingDate,
            state.Status, assignment.AssignedAt,
            PartnerLeadSummary.For(lead),
            PartnerLeadSummary.DetailsFor(lead),
            lead.AdditionalRequirements,
            customer,
            quote,
            state.CanAccept, state.CanReject, state.CanQuote);
    }

    /// <summary>Checks that [Range] attributes can't express. Null when the quote is fine.</summary>
    private static ApiError? CheckQuote(PartnerQuoteRequest request)
    {
        var total = request.BasePrice + request.PackingCharges + request.TransportationCharges
                  + request.LoadingUnloadingCharges + request.AdditionalCharges;

        if (total <= 0)
        {
            return new ApiError("Enter at least one price - the total can't be ₹0.", "emptyQuote");
        }

        var todayIndia = DateOnly.FromDateTime(DateTime.UtcNow + IndiaOffset);

        if (request.EstimatedPickupDate is { } pickup && pickup < todayIndia)
        {
            return new ApiError("The pickup date can't be in the past.", "badDate");
        }

        if (request.EstimatedPickupDate is { } from && request.EstimatedDeliveryDate is { } to && to < from)
        {
            return new ApiError("The delivery date can't be before the pickup date.", "badDate");
        }

        return null;
    }

    /// <summary>The API request in the shape the website's QuoteService takes.</summary>
    private static SubmitQuoteViewModel ToModel(PartnerQuoteRequest request) => new()
    {
        BasePrice = request.BasePrice,
        PackingCharges = request.PackingCharges,
        TransportationCharges = request.TransportationCharges,
        LoadingUnloadingCharges = request.LoadingUnloadingCharges,
        AdditionalCharges = request.AdditionalCharges,
        EstimatedPickupDate = request.EstimatedPickupDate,
        EstimatedDeliveryDate = request.EstimatedDeliveryDate,
        EstimatedDeliveryDays = request.EstimatedDeliveryDays,
        VendorNotes = string.IsNullOrWhiteSpace(request.VendorNotes) ? null : request.VendorNotes.Trim()
    };

    /// <summary>"9876543210" -> "98XXXXXXXX". Only the first two digits are ever shown.</summary>
    private static string MaskPhone(string phone) =>
        phone.Length <= 2 ? "XXXXXXXXXX" : phone[..2] + new string('X', phone.Length - 2);

    /// <summary>One row of the list query. A class, so EF can filter and sort on it in SQL.</summary>
    private sealed class LeadRow
    {
        public AssignmentStatus Status { get; init; }
        public DateTime AssignedAt { get; init; }
        public Lead Lead { get; init; } = null!;
        public bool HasActiveQuote { get; init; }
        public bool Closed { get; init; }
        public bool Won { get; init; }
    }

    private sealed record LeadState(
        Quote? Quote,
        string Status,
        bool ContactVisible,
        bool CanAccept,
        bool CanReject,
        bool CanQuote);
}