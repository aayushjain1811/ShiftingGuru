using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services;
using ShiftingGuru.Services.Email;
using ShiftingGuru.Services.Notifications;

namespace ShiftingGuru.Controllers.Api.V1;

/// <summary>
/// NEW (mobile API): the customer's moving requests - creating one, the
/// lists, and one request's details. Every query is filtered by
/// CurrentCustomer.Id, so a customer only ever sees their own requests.
///
/// Creating one does exactly what the website's quote form does after it is
/// submitted: the website's own LeadService saves it, a customer access link
/// is issued, and the same "new request" emails go to the customer and the
/// admin team. The admin then assigns it to partners as usual.
/// </summary>
[Route("api/v1/customer/requests")]
public class CustomerRequestsController : ApiCustomerControllerBase
{
    private static readonly TimeSpan IndiaOffset = TimeSpan.FromHours(5.5);

    private const int MaxListSize = 50;

    private readonly ApplicationDbContext _db;
    private readonly IServiceCatalog _catalog;
    private readonly ILeadService _leads;
    private readonly ICustomerAccessService _access;
    private readonly INotificationService _notifications;
    private readonly AppOptions _app;
    private readonly ILogger<CustomerRequestsController> _logger;

    public CustomerRequestsController(
        ApplicationDbContext db,
        IServiceCatalog catalog,
        ILeadService leads,
        ICustomerAccessService access,
        INotificationService notifications,
        IOptions<AppOptions> app,
        ILogger<CustomerRequestsController> logger)
    {
        _db = db;
        _catalog = catalog;
        _leads = leads;
        _access = access;
        _notifications = notifications;
        _app = app.Value;
        _logger = logger;
    }

    // POST /api/v1/customer/requests
    [HttpPost("")]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRequest request, CancellationToken ct)
    {
        // Only services that exist in the catalog - same rule as the website.
        var service = string.IsNullOrWhiteSpace(request.ServiceSlug) ? null : _catalog.GetBySlug(request.ServiceSlug);
        if (service is null)
        {
            return BadRequest(new ApiError("Choose a service.", "badService"));
        }

        var storage = service.Slug == "warehouse-storage";

        if (string.IsNullOrWhiteSpace(request.MovingFrom) || (!storage && string.IsNullOrWhiteSpace(request.MovingTo)))
        {
            return BadRequest(new ApiError(
                storage ? "Choose the city where you need storage." : "Choose both cities.", "badLocation"));
        }

        var todayIndia = DateOnly.FromDateTime(DateTime.UtcNow + IndiaOffset);
        if (request.MovingDate is { } date && (date < todayIndia || date > todayIndia.AddYears(1)))
        {
            return BadRequest(new ApiError("Choose a moving date between today and a year from now.", "badDate"));
        }

        if (request.Details.Count > 30 || request.Inventory.Count > 100)
        {
            return BadRequest(new ApiError("That request has too many answers.", "tooLarge"));
        }

        var input = AppRequestMapper.Map(CurrentCustomer, service, request);

        Models.Lead lead;
        try
        {
            lead = await _leads.CreateAppLeadAsync(input, service, ct);
        }
        catch (Exception ex)
        {
            // Logged without customer details.
            _logger.LogError(ex, "Failed to save an app request for service {ServiceSlug}", service.Slug);
            return StatusCode(StatusCodes.Status500InternalServerError, new ApiError(
                "Sorry, we couldn't submit your request just now. Please try again in a moment.", "saveFailed"));
        }

        // The same follow-up as the website: an access link (so the customer
        // can also open the request on the website) and the "new request"
        // emails. Sent after the save; a failure here never loses the request.
        try
        {
            var link = await _access.IssueAsync(lead.Id, ct);
            await _notifications.LeadCreatedAsync(lead, _app.Url(link.Url), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Couldn't send the new-request notifications for lead {LeadId}", lead.Id);
        }

        return Ok(new CustomerRequestCreatedDto(lead.Id, lead.LeadNumber));
    }

    // GET /api/v1/customer/requests?group=active|completed|cancelled&limit=3
    [HttpGet("")]
    public async Task<IActionResult> List([FromQuery] string? group, [FromQuery] int? limit, CancellationToken ct)
    {
        var selected = (group ?? "").Trim().ToLowerInvariant();
        if (selected is not ("" or "active" or "completed" or "cancelled"))
        {
            return BadRequest(new ApiError("Unknown group. Use active, completed or cancelled.", "badGroup"));
        }

        // Website requests made with the customer's verified number since they
        // registered become theirs too - checked each time the list loads.
        await LinkWebsiteRequestsAsync(ct);

        var query = _db.Leads.AsNoTracking().Where(l => l.CustomerId == CurrentCustomer.Id);

        query = selected switch
        {
            "active" => query.Where(l => l.Status != LeadStatus.Completed
                                      && l.Status != LeadStatus.Closed
                                      && l.Status != LeadStatus.Cancelled),
            "completed" => query.Where(l => l.Status == LeadStatus.Completed),
            "cancelled" => query.Where(l => l.Status == LeadStatus.Closed || l.Status == LeadStatus.Cancelled),
            _ => query
        };

        var take = Math.Clamp(limit ?? MaxListSize, 1, MaxListSize);

        var rows = await query
            .OrderByDescending(l => l.CreatedAt)
            .Take(take)
            .Select(l => new
            {
                Lead = l,
                VisibleQuotes = _db.Quotes.Count(q =>
                    q.LeadId == l.Id && Quote.CustomerVisibleStatuses.Contains(q.Status))
            })
            .ToListAsync(ct);

        return Ok(rows.Select(r => new CustomerRequestDto(
                r.Lead.Id, r.Lead.LeadNumber, r.Lead.ServiceSlug, r.Lead.ServiceName,
                r.Lead.MovingFrom, r.Lead.MovingTo, r.Lead.StorageLocation, r.Lead.MovingDate,
                CustomerViews.RequestStatus(r.Lead.Status, r.VisibleQuotes),
                r.VisibleQuotes, r.Lead.CreatedAt))
            .ToList());
    }

    // GET /api/v1/customer/requests/42
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        // The customer filter is part of the lookup: someone else's request
        // looks exactly like one that doesn't exist.
        var lead = await _db.Leads
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == id && l.CustomerId == CurrentCustomer.Id, ct);

        if (lead is null)
        {
            return NotFound(new ApiError("That request couldn't be found.", "notFound"));
        }

        var visible = await _db.Quotes.AsNoTracking()
            .CountAsync(q => q.LeadId == lead.Id && Quote.CustomerVisibleStatuses.Contains(q.Status), ct);

        return Ok(new CustomerRequestDetailsDto(
            lead.Id, lead.LeadNumber, lead.ServiceSlug, lead.ServiceName,
            lead.MovingFrom, lead.MovingTo, lead.StorageLocation, lead.MovingDate,
            CustomerViews.RequestStatus(lead.Status, visible), visible, lead.CreatedAt,
            PartnerLeadSummary.DetailsFor(lead),
            lead.AdditionalRequirements,
            lead.SelectedQuoteId));
    }

    /// <summary>Links website requests made with this customer's verified mobile number.</summary>
    private async Task LinkWebsiteRequestsAsync(CancellationToken ct)
    {
        var phone = CurrentCustomer.Phone;

        try
        {
            await _db.Leads
                .Where(l => l.CustomerId == null && (l.Phone == phone || l.Phone.EndsWith(phone)))
                .ExecuteUpdateAsync(set => set.SetProperty(l => l.CustomerId, CurrentCustomer.Id), ct);
        }
        catch (Exception ex)
        {
            // Not worth failing the list over.
            _logger.LogError(ex, "Couldn't link website requests for customer {CustomerId}", CurrentCustomer.Id);
        }
    }
}