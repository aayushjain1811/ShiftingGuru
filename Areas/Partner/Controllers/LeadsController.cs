using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services;
using ShiftingGuru.ViewModels.Partner;

namespace ShiftingGuru.Areas.Partner.Controllers;

/// <summary>
/// A vendor's own assigned leads.
///
/// Every query here is filtered by CurrentVendor.Id, which comes from the
/// signed-in Identity user via PartnerControllerBase - never from a route
/// value, form field or query string.
/// </summary>
[Route("partner/leads")]
public class LeadsController : PartnerControllerBase
{
    private const int PageSize = 20;

    private readonly ApplicationDbContext _db;
    private readonly IQuoteService _quotes;
    private readonly ILogger<LeadsController> _logger;

    public LeadsController(
        IPartnerService partners,
        UserManager<IdentityUser> users,
        ApplicationDbContext db,
        IQuoteService quotes,
        ILogger<LeadsController> logger)
        : base(partners, users)
    {
        _db = db;
        _quotes = quotes;
        _logger = logger;
    }

    // GET /partner/leads
    [HttpGet("")]
    public async Task<IActionResult> Index(
        AssignmentStatus? status, int page = 1, CancellationToken ct = default)
    {
        ViewData["Title"] = "My leads";
        if (page < 1) page = 1;

        var query = _db.LeadAssignments
            .AsNoTracking()
            .Where(a => a.VendorId == CurrentVendor.Id
                     && a.Status != AssignmentStatus.Cancelled);

        if (status.HasValue && Enum.IsDefined(status.Value))
        {
            query = query.Where(a => a.Status == status.Value);
        }

        var total = await query.CountAsync(ct);

        var totalPages = total == 0 ? 1 : (int)Math.Ceiling(total / (double)PageSize);
        if (page > totalPages) page = totalPages;

        // Projected, so customer name, phone and email never leave the database
        // for the list view. They belong on the details page only.
        var rows = await query
            .OrderByDescending(a => a.AssignedAt)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(a => new PartnerLeadRow
            {
                LeadId = a.LeadId,
                LeadNumber = a.Lead!.LeadNumber,
                ServiceName = a.Lead.ServiceName,
                MovingFrom = a.Lead.MovingFrom,
                MovingTo = a.Lead.MovingTo,
                StorageLocation = a.Lead.StorageLocation,
                MovingDate = a.Lead.MovingDate,
                Status = a.Status,
                AssignedAt = a.AssignedAt
            })
            .ToListAsync(ct);

        return View(new PartnerLeadListViewModel
        {
            Leads = rows,
            Status = status,
            Page = page,
            PageSize = PageSize,
            TotalCount = total
        });
    }

    // GET /partner/leads/42
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        // The vendor filter is part of the lookup itself. A lead assigned to
        // someone else simply isn't found - the response is identical to a
        // lead that doesn't exist, so nothing is revealed either way.
        var assignment = await _db.LeadAssignments
            .Include(a => a.Lead)
            .FirstOrDefaultAsync(a =>
                a.LeadId == id &&
                a.VendorId == CurrentVendor.Id &&
                a.Status != AssignmentStatus.Cancelled, ct);

        if (assignment?.Lead is null) return View("NotFound");

        // Mark as viewed only now, after authorization succeeded - not when
        // the list was loaded.
        if (assignment.Status == AssignmentStatus.Assigned)
        {
            assignment.Status = AssignmentStatus.Viewed;
            assignment.ViewedAt = DateTime.UtcNow;
            assignment.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                // Not worth failing the page over - the vendor still gets the lead.
                _logger.LogError(ex, "Couldn't mark assignment {AssignmentId} as viewed", assignment.Id);
            }
        }

        ViewData["Title"] = assignment.Lead.LeadNumber;

        // Drives which button the page shows: Submit, View or Edit.
        var quote = await _db.Quotes
            .AsNoTracking()
            .Where(q => q.LeadId == id && q.VendorId == CurrentVendor.Id)
            .OrderByDescending(q => q.CreatedAt)
            .FirstOrDefaultAsync(ct);

        return View(new PartnerLeadDetailsViewModel
        {
            Lead = assignment.Lead,
            Assignment = assignment,
            ServiceDetails = BuildServiceDetails(assignment.Lead),
            ExistingQuote = quote
        });
    }

    // GET /partner/leads/42/quote
    [HttpGet("{id:int}/quote")]
    public async Task<IActionResult> Quote(int id, CancellationToken ct)
    {
        var lead = await LoadAssignedLeadAsync(id, ct);
        if (lead is null) return View("NotFound");

        var active = await _db.Quotes
            .AsNoTracking()
            .AnyAsync(q => q.LeadId == id
                        && q.VendorId == CurrentVendor.Id
                        && ShiftingGuru.Models.Quote.ActiveStatuses.Contains(q.Status), ct);

        if (active)
        {
            TempData["PartnerMessage"] = "You have already submitted a quote for this lead.";
            return RedirectToAction(nameof(Details), new { id });
        }

        ViewData["Title"] = "Submit a quote";
        return View(new SubmitQuoteViewModel { Lead = lead });
    }

    // POST /partner/leads/42/quote
    [HttpPost("{id:int}/quote")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Quote(int id, SubmitQuoteViewModel model, CancellationToken ct)
    {
        var lead = await LoadAssignedLeadAsync(id, ct);
        if (lead is null) return View("NotFound");

        model.Lead = lead;
        ViewData["Title"] = "Submit a quote";

        if (!ModelState.IsValid) return View(model);

        // The service re-checks the assignment and recalculates the total.
        var result = await _quotes.SubmitAsync(id, CurrentVendor.Id, model, ct);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(model);
        }

        TempData["PartnerMessage"] = $"Quote {result.Quote!.QuoteNumber} submitted.";
        return Redirect($"/partner/quotes/{result.Quote.Id}");
    }

    /// <summary>Returns the lead only if it's actively assigned to this vendor.</summary>
    private async Task<Lead?> LoadAssignedLeadAsync(int leadId, CancellationToken ct)
    {
        var assignment = await _db.LeadAssignments
            .AsNoTracking()
            .Include(a => a.Lead)
            .FirstOrDefaultAsync(a => a.LeadId == leadId
                                   && a.VendorId == CurrentVendor.Id
                                   && a.Status != AssignmentStatus.Cancelled, ct);

        return assignment?.Lead;
    }

    private static IReadOnlyList<(string, string)> BuildServiceDetails(Lead lead)
    {
        var candidates = new (string Label, string? Value)[]
        {
            ("Property type", lead.PropertyType),
            ("Approximate size", lead.MoveSize),
            ("Office size", lead.OfficeSize),
            ("Desks / employees", lead.DeskCount),
            ("Vehicle type", lead.VehicleType),
            ("Brand and model", lead.VehicleModel),
            ("Vehicle condition", lead.VehicleCondition),
            ("Type of goods", lead.GoodsType),
            ("Load details", lead.LoadDetails),
            ("Vehicle requirement", lead.VehicleRequirement),
            ("Storage type", lead.StorageType),
            ("Storage size", lead.StorageSize),
            ("Expected duration", lead.StorageDuration)
        };

        return candidates
            .Where(c => !string.IsNullOrWhiteSpace(c.Value))
            .Select(c => (c.Label, c.Value!))
            .ToList();
    }
}