using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services;
using ShiftingGuru.ViewModels.Admin;

namespace ShiftingGuru.Areas.Admin.Controllers;

[Area("Admin")]
[Route("admin/faqs")]
[Authorize(Roles = AdminSeeder.AdminRole)]
public class FaqsController : Controller
{
    private const int PageSize = 30;

    private readonly ApplicationDbContext _db;
    private readonly IServiceCatalog _catalog;
    private readonly IAuditService _audit;
    private readonly ILogger<FaqsController> _logger;

    public FaqsController(
        ApplicationDbContext db, IServiceCatalog catalog, IAuditService audit,
        ILogger<FaqsController> logger)
    {
        _db = db;
        _catalog = catalog;
        _audit = audit;
        _logger = logger;
    }

    // GET /admin/faqs
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? search, string? owner, bool? published, int page = 1, CancellationToken ct = default)
    {
        ViewData["Title"] = "FAQs";
        if (page < 1) page = 1;

        var query = _db.Faqs
            .AsNoTracking()
            .Include(f => f.Location)
            .Include(f => f.Route!).ThenInclude(r => r.FromLocation)
            .Include(f => f.Route!).ThenInclude(r => r.ToLocation)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(f =>
                EF.Functions.ILike(f.Question, term) || EF.Functions.ILike(f.Answer, term));
        }

        query = owner switch
        {
            "location" => query.Where(f => f.LocationId != null),
            "route" => query.Where(f => f.RouteId != null),
            "service" => query.Where(f => f.ServiceSlug != null),
            _ => query
        };

        if (published.HasValue) query = query.Where(f => f.IsPublished == published.Value);

        var total = await query.CountAsync(ct);
        var totalPages = total == 0 ? 1 : (int)Math.Ceiling(total / (double)PageSize);
        if (page > totalPages) page = totalPages;

        var faqs = await query
            .OrderBy(f => f.DisplayOrder).ThenByDescending(f => f.Id)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(ct);

        return View(new AdminFaqListViewModel
        {
            Faqs = faqs,
            Search = search,
            Owner = owner,
            Published = published,
            Page = page,
            PageSize = PageSize,
            TotalCount = total
        });
    }

    // GET /admin/faqs/create
    [HttpGet("create")]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        ViewData["Title"] = "New FAQ";
        return View("Form", await PrepareAsync(new AdminFaqFormViewModel { IsPublished = true }, ct));
    }

    // POST /admin/faqs/create
    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminFaqFormViewModel model, CancellationToken ct)
    {
        ViewData["Title"] = "New FAQ";

        if (!await ValidateOwnerAsync(model, ct) || !ModelState.IsValid)
        {
            return View("Form", await PrepareAsync(model, ct));
        }

        var faq = new Faq { CreatedAt = DateTime.UtcNow };
        Apply(model, faq);

        _db.Faqs.Add(faq);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            // The single-owner check constraint lands here if anything slipped past.
            _logger.LogError(ex, "FAQ create failed");
            ModelState.AddModelError(string.Empty, "Couldn't save that FAQ. Please try again.");
            return View("Form", await PrepareAsync(model, ct));
        }

        await _audit.RecordAsync(AuditAction.Created, nameof(Faq), faq.Id,
            $"Created FAQ: {Truncate(faq.Question)}", ct);

        TempData["AdminMessage"] = "FAQ created.";
        return RedirectToAction(nameof(Index));
    }

    // GET /admin/faqs/5/edit
    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var faq = await _db.Faqs.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, ct);
        if (faq is null) return View("NotFound");

        ViewData["Title"] = "Edit FAQ";

        return View("Form", await PrepareAsync(new AdminFaqFormViewModel
        {
            Id = faq.Id,
            Question = faq.Question,
            Answer = faq.Answer,
            OwnerType = faq.LocationId.HasValue ? "location"
                      : faq.RouteId.HasValue ? "route" : "service",
            LocationId = faq.LocationId,
            RouteId = faq.RouteId,
            ServiceSlug = faq.ServiceSlug,
            DisplayOrder = faq.DisplayOrder,
            IsPublished = faq.IsPublished
        }, ct));
    }

    // POST /admin/faqs/5/edit
    [HttpPost("{id:int}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AdminFaqFormViewModel model, CancellationToken ct)
    {
        var faq = await _db.Faqs.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (faq is null) return View("NotFound");

        model.Id = id;
        ViewData["Title"] = "Edit FAQ";

        if (!await ValidateOwnerAsync(model, ct) || !ModelState.IsValid)
        {
            return View("Form", await PrepareAsync(model, ct));
        }

        Apply(model, faq);
        faq.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _audit.RecordAsync(AuditAction.Updated, nameof(Faq), faq.Id,
            $"Updated FAQ: {Truncate(faq.Question)}", ct);

        TempData["AdminMessage"] = "FAQ updated.";
        return RedirectToAction(nameof(Index));
    }

    // POST /admin/faqs/5/publish
    [HttpPost("{id:int}/publish")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePublish(int id, CancellationToken ct)
    {
        var faq = await _db.Faqs.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (faq is null) return View("NotFound");

        faq.IsPublished = !faq.IsPublished;
        faq.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _audit.RecordAsync(
            faq.IsPublished ? AuditAction.Published : AuditAction.Unpublished,
            nameof(Faq), faq.Id, $"{(faq.IsPublished ? "Published" : "Unpublished")} FAQ", ct);

        // Unpublishing also removes it from that page's FAQPage schema, because
        // the JSON-LD is built from the same published query.
        TempData["AdminMessage"] = faq.IsPublished ? "FAQ published." : "FAQ hidden.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Exactly one owner, and it must exist. The database has a check
    /// constraint too, but a clear message beats a constraint violation.
    /// </summary>
    private async Task<bool> ValidateOwnerAsync(AdminFaqFormViewModel model, CancellationToken ct)
    {
        switch (model.OwnerType)
        {
            case "location":
                if (model.LocationId is null ||
                    !await _db.Locations.AnyAsync(l => l.Id == model.LocationId, ct))
                {
                    ModelState.AddModelError(nameof(model.LocationId), "Choose a location.");
                    return false;
                }
                model.RouteId = null;
                model.ServiceSlug = null;
                return true;

            case "route":
                if (model.RouteId is null ||
                    !await _db.Routes.AnyAsync(r => r.Id == model.RouteId, ct))
                {
                    ModelState.AddModelError(nameof(model.RouteId), "Choose a route.");
                    return false;
                }
                model.LocationId = null;
                model.ServiceSlug = null;
                return true;

            case "service":
                if (string.IsNullOrWhiteSpace(model.ServiceSlug) ||
                    _catalog.GetBySlug(model.ServiceSlug) is null)
                {
                    ModelState.AddModelError(nameof(model.ServiceSlug), "Choose a service.");
                    return false;
                }
                model.LocationId = null;
                model.RouteId = null;
                return true;

            default:
                ModelState.AddModelError(nameof(model.OwnerType), "Choose what this FAQ belongs to.");
                return false;
        }
    }

    private async Task<AdminFaqFormViewModel> PrepareAsync(
        AdminFaqFormViewModel model, CancellationToken ct)
    {
        model.Locations = await _db.Locations.AsNoTracking().OrderBy(l => l.Name).ToListAsync(ct);

        model.Routes = await _db.Routes
            .AsNoTracking()
            .Include(r => r.FromLocation)
            .Include(r => r.ToLocation)
            .OrderBy(r => r.Slug)
            .ToListAsync(ct);

        model.Services = _catalog.GetAll();

        return model;
    }

    private static void Apply(AdminFaqFormViewModel model, Faq faq)
    {
        faq.Question = model.Question!.Trim();
        faq.Answer = model.Answer!.Trim();
        faq.LocationId = model.LocationId;
        faq.RouteId = model.RouteId;
        faq.ServiceSlug = string.IsNullOrWhiteSpace(model.ServiceSlug) ? null : model.ServiceSlug;
        faq.DisplayOrder = model.DisplayOrder;
        faq.IsPublished = model.IsPublished;
    }

    private static string Truncate(string value) =>
        value.Length > 60 ? value[..60] + "..." : value;
}