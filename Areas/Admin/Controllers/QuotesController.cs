using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services;
using ShiftingGuru.ViewModels.Admin;

namespace ShiftingGuru.Areas.Admin.Controllers;

[Area("Admin")]
[Route("admin/quotes")]
[Authorize(Roles = AdminSeeder.AdminRole)]
public class QuotesController : Controller
{
    private const int PageSize = 25;

    private readonly ApplicationDbContext _db;
    private readonly IServiceCatalog _catalog;
    private readonly IQuoteService _quotes;
    private readonly IAuditService _audit;

    public QuotesController(
        ApplicationDbContext db, IServiceCatalog catalog, IQuoteService quotes, IAuditService audit)
    {
        _db = db;
        _catalog = catalog;
        _quotes = quotes;
        _audit = audit;
    }

    // GET /admin/quotes
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? search, QuoteStatus? status, string? service, int? vendorId,
        DateOnly? from, DateOnly? to, int page = 1, CancellationToken ct = default)
    {
        ViewData["Title"] = "Quotes";
        if (page < 1) page = 1;

        var query = _db.Quotes.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(q =>
                EF.Functions.ILike(q.QuoteNumber, term) ||
                EF.Functions.ILike(q.Lead!.LeadNumber, term) ||
                EF.Functions.ILike(q.Vendor!.BusinessName, term));
        }

        if (status.HasValue && Enum.IsDefined(status.Value))
        {
            query = query.Where(q => q.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(service) && _catalog.GetBySlug(service) is { } matched)
        {
            query = query.Where(q => q.Lead!.ServiceSlug == matched.Slug);
        }
        else
        {
            service = null;
        }

        if (vendorId.HasValue)
        {
            query = query.Where(q => q.VendorId == vendorId.Value);
        }

        if (from.HasValue)
        {
            var fromUtc = from.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(q => q.CreatedAt >= fromUtc);
        }

        if (to.HasValue)
        {
            var toUtc = to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(q => q.CreatedAt < toUtc);
        }

        var total = await query.CountAsync(ct);

        var totalPages = total == 0 ? 1 : (int)Math.Ceiling(total / (double)PageSize);
        if (page > totalPages) page = totalPages;

        var rows = await query
            .OrderByDescending(q => q.CreatedAt)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(q => new AdminQuoteRow
            {
                QuoteId = q.Id,
                QuoteNumber = q.QuoteNumber,
                LeadNumber = q.Lead!.LeadNumber,
                VendorName = q.Vendor!.BusinessName,
                ServiceName = q.Lead.ServiceName,
                MovingFrom = q.Lead.MovingFrom,
                MovingTo = q.Lead.MovingTo,
                StorageLocation = q.Lead.StorageLocation,
                TotalAmount = q.TotalAmount,
                Status = q.Status,
                CreatedAt = q.CreatedAt
            })
            .ToListAsync(ct);

        // Small projection for the vendor filter dropdown.
        var vendors = await _db.Vendors
            .AsNoTracking()
            .Where(v => v.Quotes.Any())
            .OrderBy(v => v.BusinessName)
            .Select(v => new { v.Id, v.BusinessName })
            .ToListAsync(ct);

        return View(new AdminQuoteListViewModel
        {
            Quotes = rows,
            Search = search,
            Status = status,
            ServiceSlug = service,
            VendorId = vendorId,
            FromDate = from,
            ToDate = to,
            Page = page,
            PageSize = PageSize,
            TotalCount = total,
            Services = _catalog.GetAll(),
            Vendors = vendors.Select(v => (v.Id, v.BusinessName)).ToList()
        });
    }

    // GET /admin/quotes/42
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var quote = await _db.Quotes
            .AsNoTracking()
            .Include(q => q.Lead)
            .Include(q => q.Vendor)
            .FirstOrDefaultAsync(q => q.Id == id, ct);

        if (quote?.Lead is null || quote.Vendor is null) return View("NotFound");

        ViewData["Title"] = quote.QuoteNumber;

        return View(new AdminQuoteDetailsViewModel
        {
            Quote = quote,
            Lead = quote.Lead,
            Vendor = quote.Vendor,
            ServiceDetails = BuildServiceDetails(quote.Lead),
            AllowedTransitions = _quotes.AllowedTransitionsFrom(quote.Status)
        });
    }

    // POST /admin/quotes/42/status
    [HttpPost("{id:int}/status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, QuoteStatus status, CancellationToken ct)
    {
        var changed = await _quotes.ChangeStatusAsync(id, status, ct);

        TempData[changed ? "AdminMessage" : "AdminError"] = changed
            ? $"Quote marked {status}."
            : "That status change isn't allowed from the quote's current state.";

        if (changed)
        {
            await _audit.RecordAsync(AuditAction.StatusChanged, nameof(Quote), id,
                $"Quote moved to {status}", ct);
        }

        return RedirectToAction(nameof(Details), new { id });
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