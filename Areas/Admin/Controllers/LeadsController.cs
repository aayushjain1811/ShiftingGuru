using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services;
using ShiftingGuru.Services.Notifications;
using ShiftingGuru.ViewModels.Admin;

namespace ShiftingGuru.Areas.Admin.Controllers;

[Area("Admin")]
[Route("admin/leads")]
[Authorize(Roles = AdminSeeder.AdminRole)]
public class LeadsController : Controller
{
    private const int PageSize = 25;

    private readonly ApplicationDbContext _db;
    private readonly IServiceCatalog _catalog;
    private readonly ILeadAssignmentService _assignments;
    private readonly INotificationService _notifications;
    private readonly IAuditService _audit;
    private readonly ILogger<LeadsController> _logger;

    public LeadsController(
        ApplicationDbContext db,
        IServiceCatalog catalog,
        ILeadAssignmentService assignments,
        INotificationService notifications,
        IAuditService audit,
        ILogger<LeadsController> logger)
    {
        _db = db;
        _catalog = catalog;
        _assignments = assignments;
        _notifications = notifications;
        _audit = audit;
        _logger = logger;
    }

    // GET /admin/leads?search=&service=&status=&from=&to=&page=
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? search, string? service, LeadStatus? status, bool? assigned,
        DateOnly? from, DateOnly? to, int page = 1, CancellationToken ct = default)
    {
        if (page < 1) page = 1;

        // IQueryable: nothing runs until ToListAsync below, so every filter
        // ends up in one SQL statement.
        var query = _db.Leads.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";

            // ILike is PostgreSQL's case-insensitive LIKE. Using it keeps the
            // comparison in the database instead of pulling rows into memory.
            query = query.Where(l =>
                EF.Functions.ILike(l.LeadNumber, term) ||
                EF.Functions.ILike(l.CustomerName, term) ||
                EF.Functions.ILike(l.Phone, term) ||
                (l.Email != null && EF.Functions.ILike(l.Email, term)));
        }

        // Only accept a slug that exists in the catalog.
        if (!string.IsNullOrWhiteSpace(service) && _catalog.GetBySlug(service) is { } matched)
        {
            query = query.Where(l => l.ServiceSlug == matched.Slug);
        }
        else
        {
            service = null;
        }

        if (status.HasValue && Enum.IsDefined(status.Value))
        {
            query = query.Where(l => l.Status == status.Value);
        }

        // Translated to EXISTS / NOT EXISTS - no rows loaded to count.
        if (assigned == true)
        {
            query = query.Where(l => l.Assignments.Any(a => a.Status != AssignmentStatus.Cancelled));
        }
        else if (assigned == false)
        {
            query = query.Where(l => !l.Assignments.Any(a => a.Status != AssignmentStatus.Cancelled));
        }

        if (from.HasValue)
        {
            var fromUtc = from.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(l => l.CreatedAt >= fromUtc);
        }

        if (to.HasValue)
        {
            var toUtc = to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(l => l.CreatedAt < toUtc);
        }

        var total = await query.CountAsync(ct);

        // Clamp the page so ?page=9999 lands on the last real page.
        var totalPages = total == 0 ? 1 : (int)Math.Ceiling(total / (double)PageSize);
        if (page > totalPages) page = totalPages;

        var leads = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(ct);

        // One grouped query for the whole page's assignment counts, rather
        // than a query per row.
        var leadIds = leads.Select(l => l.Id).ToList();

        var counts = await _db.LeadAssignments
            .AsNoTracking()
            .Where(a => leadIds.Contains(a.LeadId) && a.Status != AssignmentStatus.Cancelled)
            .GroupBy(a => a.LeadId)
            .Select(g => new { LeadId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.LeadId, x => x.Count, ct);

        return View(new AdminLeadListViewModel
        {
            Leads = leads,
            Assigned = assigned,
            AssignmentCounts = counts,
            Search = search,
            ServiceSlug = service,
            Status = status,
            FromDate = from,
            ToDate = to,
            Page = page,
            PageSize = PageSize,
            TotalCount = total,
            Services = _catalog.GetAll()
        });
    }

    // GET /admin/leads/42
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var lead = await _db.Leads
            .AsNoTracking()
            .Include(l => l.SelectedVendor)
            .Include(l => l.SelectedQuote)
            .FirstOrDefaultAsync(l => l.Id == id, ct);

        if (lead is null) return View("NotFound");

        var quotes = await _db.Quotes
            .AsNoTracking()
            .Include(q => q.Vendor)
            .Where(q => q.LeadId == id)
            .OrderBy(q => q.TotalAmount)
            .ToListAsync(ct);

        return View(new AdminLeadDetailsViewModel
        {
            Lead = lead,
            ServiceDetails = BuildServiceDetails(lead),
            AvailableStatuses = Enum.GetValues<LeadStatus>(),
            Assignment = await _assignments.BuildAssignmentPanelAsync(lead, ct),
            Quotes = quotes
        });
    }

    // POST /admin/leads/42/status
    [HttpPost("{id:int}/status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, LeadStatus status, CancellationToken ct)
    {
        // Model binding turns an unknown value into 0 (New), so check the raw
        // value is a real member rather than trusting the bound enum.
        if (!Enum.IsDefined(status))
        {
            TempData["AdminError"] = "That status isn't recognised.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // Tracked here because we're writing.
        var lead = await _db.Leads.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lead is null) return View("NotFound");

        if (lead.Status != status)
        {
            lead.Status = status;
            lead.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _db.SaveChangesAsync(ct);
                TempData["AdminMessage"] = $"Status updated to {status}.";

                await _audit.RecordAsync(AuditAction.StatusChanged, nameof(Lead), lead.Id,
                    $"Lead {lead.LeadNumber} moved to {status}", ct);
            }
            catch (Exception ex)
            {
                // No lead contents in the log - the id is enough to investigate.
                _logger.LogError(ex, "Failed to update status for lead {LeadId}", id);
                TempData["AdminError"] = "Couldn't save that change. Please try again.";
            }
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    // POST /admin/leads/42/complete
    [HttpPost("{id:int}/complete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id, CancellationToken ct)
    {
        var lead = await _db.Leads
            .Include(l => l.SelectedVendor)
            .FirstOrDefaultAsync(l => l.Id == id, ct);

        if (lead is null) return View("NotFound");

        // Only a converted lead can be completed. Nothing else makes sense -
        // there'd be no vendor to have done the work.
        if (lead.Status != LeadStatus.Converted)
        {
            TempData["AdminError"] = $"Only a converted lead can be marked complete (this one is {lead.Status}).";
            return RedirectToAction(nameof(Details), new { id });
        }

        lead.Status = LeadStatus.Completed;
        lead.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync(ct);
            TempData["AdminMessage"] = "Lead marked complete. The customer can now leave a review.";

            await _audit.RecordAsync(AuditAction.StatusChanged, nameof(Lead), lead.Id,
                $"Lead {lead.LeadNumber} marked complete", ct);

            if (lead.SelectedVendor is not null)
            {
                await _notifications.LeadCompletedAsync(lead, lead.SelectedVendor, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete lead {LeadId}", id);
            TempData["AdminError"] = "Couldn't save that change. Please try again.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    // POST /admin/leads/42/assign
    [HttpPost("{id:int}/assign")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(int id, int[] vendorIds, CancellationToken ct)
    {
        var result = await _assignments.AssignAsync(id, vendorIds ?? Array.Empty<int>(), ct);

        if (!result.Succeeded)
        {
            TempData["AdminError"] = result.Error;
        }
        else if (result.Total == 0)
        {
            TempData["AdminError"] = "Those vendors were already assigned to this lead.";
        }
        else
        {
            var message = $"Assigned to {result.Total} vendor{(result.Total == 1 ? "" : "s")}.";
            if (result.Skipped > 0) message += $" {result.Skipped} skipped.";
            TempData["AdminMessage"] = message;

            await _audit.RecordAsync(AuditAction.Assigned, nameof(Lead), id,
                $"Assigned lead to {result.Total} vendor(s)", ct);
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    // POST /admin/leads/42/assignments/7/cancel
    [HttpPost("{id:int}/assignments/{assignmentId:int}/cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelAssignment(int id, int assignmentId, CancellationToken ct)
    {
        var cancelled = await _assignments.CancelAsync(id, assignmentId, ct);

        TempData[cancelled ? "AdminMessage" : "AdminError"] = cancelled
            ? "Assignment withdrawn."
            : "Couldn't withdraw that assignment.";

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Builds the label/value list for the fields that apply to this lead,
    /// dropping anything empty. Keeps the view free of service logic.
    /// </summary>
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