using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services;
using ShiftingGuru.ViewModels.Admin;

namespace ShiftingGuru.Areas.Admin.Controllers;

/// <summary>
/// CMS for route landing pages. Distinct from the public
/// ShiftingGuru.Controllers.RoutesController - same class name, different
/// namespace and area, so they don't collide.
/// </summary>
[Area("Admin")]
[Route("admin/routes")]
[Authorize(Roles = AdminSeeder.AdminRole)]
public class RoutesController : Controller
{
    private const int PageSize = 25;

    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly ILogger<RoutesController> _logger;

    public RoutesController(
        ApplicationDbContext db, IAuditService audit, ILogger<RoutesController> logger)
    {
        _db = db;
        _audit = audit;
        _logger = logger;
    }

    // GET /admin/routes
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? search, bool? published, int page = 1, CancellationToken ct = default)
    {
        ViewData["Title"] = "Routes";
        if (page < 1) page = 1;

        var query = _db.Routes
            .AsNoTracking()
            .Include(r => r.FromLocation)
            .Include(r => r.ToLocation)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(r =>
                EF.Functions.ILike(r.Slug, term) ||
                EF.Functions.ILike(r.FromLocation!.Name, term) ||
                EF.Functions.ILike(r.ToLocation!.Name, term));
        }

        if (published.HasValue) query = query.Where(r => r.IsPublished == published.Value);

        var total = await query.CountAsync(ct);
        var totalPages = total == 0 ? 1 : (int)Math.Ceiling(total / (double)PageSize);
        if (page > totalPages) page = totalPages;

        var routes = await query
            .OrderBy(r => r.FromLocation!.Name).ThenBy(r => r.ToLocation!.Name)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(ct);

        return View(new AdminRouteListViewModel
        {
            Routes = routes,
            Search = search,
            Published = published,
            Page = page,
            PageSize = PageSize,
            TotalCount = total,
            DraftCount = await _db.Routes.AsNoTracking().CountAsync(r => !r.IsPublished, ct)
        });
    }

    // GET /admin/routes/create
    [HttpGet("create")]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        ViewData["Title"] = "New route";
        return View("Form", await PrepareAsync(new AdminRouteFormViewModel(), ct));
    }

    // POST /admin/routes/create
    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminRouteFormViewModel model, CancellationToken ct)
    {
        ViewData["Title"] = "New route";

        var endpoints = await ValidateEndpointsAsync(model, null, ct);
        if (endpoints is null || !ModelState.IsValid) return View("Form", await PrepareAsync(model, ct));

        var (from, to) = endpoints.Value;

        var route = new MovingRoute
        {
            FromLocationId = from.Id,
            ToLocationId = to.Id,
            Slug = MovingRoute.BuildSlug(from.Slug, to.Slug),
            CreatedAt = DateTime.UtcNow
        };

        Apply(model, route);
        _db.Routes.Add(route);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            // The unique index and the check constraint both land here.
            _logger.LogError(ex, "Route create failed");
            ModelState.AddModelError(string.Empty,
                "Couldn't save that route. It may already exist.");
            return View("Form", await PrepareAsync(model, ct));
        }

        await _audit.RecordAsync(AuditAction.Created, "Route", route.Id,
            $"Created route {from.Name} to {to.Name}", ct);

        TempData["AdminMessage"] = $"Route {from.Name} to {to.Name} created.";
        return RedirectToAction(nameof(Index));
    }

    // GET /admin/routes/5/edit
    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var route = await _db.Routes.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        if (route is null) return View("NotFound");

        ViewData["Title"] = "Edit route";

        return View("Form", await PrepareAsync(new AdminRouteFormViewModel
        {
            Id = route.Id,
            FromLocationId = route.FromLocationId,
            ToLocationId = route.ToLocationId,
            H1 = route.H1,
            ShortDescription = route.ShortDescription,
            Content = route.Content,
            MetaTitle = route.MetaTitle,
            MetaDescription = route.MetaDescription,
            OgTitle = route.OgTitle,
            OgDescription = route.OgDescription,
            OgImage = route.OgImage,
            IsPublished = route.IsPublished,
            CurrentSlug = route.Slug
        }, ct));
    }

    // POST /admin/routes/5/edit
    [HttpPost("{id:int}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AdminRouteFormViewModel model, CancellationToken ct)
    {
        var route = await _db.Routes.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (route is null) return View("NotFound");

        model.Id = id;
        ViewData["Title"] = "Edit route";

        var endpoints = await ValidateEndpointsAsync(model, id, ct);
        if (endpoints is null || !ModelState.IsValid) return View("Form", await PrepareAsync(model, ct));

        var (from, to) = endpoints.Value;

        // Changing either endpoint changes the URL. Worth knowing: the old URL
        // stops working, so only do this before a page has earned traffic.
        route.FromLocationId = from.Id;
        route.ToLocationId = to.Id;
        route.Slug = MovingRoute.BuildSlug(from.Slug, to.Slug);

        Apply(model, route);
        route.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Route update failed for {RouteId}", id);
            ModelState.AddModelError(string.Empty, "Couldn't save those changes. Please try again.");
            return View("Form", await PrepareAsync(model, ct));
        }

        await _audit.RecordAsync(AuditAction.Updated, "Route", route.Id,
            $"Updated route {from.Name} to {to.Name}", ct);

        TempData["AdminMessage"] = "Route updated.";
        return RedirectToAction(nameof(Index));
    }

    // POST /admin/routes/5/publish
    [HttpPost("{id:int}/publish")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePublish(int id, CancellationToken ct)
    {
        var route = await _db.Routes
            .Include(r => r.FromLocation)
            .Include(r => r.ToLocation)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (route is null) return View("NotFound");

        // A route page linking to two draft cities would be an orphan. Publish
        // the cities first.
        if (!route.IsPublished &&
            (route.FromLocation?.IsPublished != true || route.ToLocation?.IsPublished != true))
        {
            TempData["AdminError"] =
                "Publish both cities before publishing this route - the page links to them.";
            return RedirectToAction(nameof(Index));
        }

        route.IsPublished = !route.IsPublished;
        route.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _audit.RecordAsync(
            route.IsPublished ? AuditAction.Published : AuditAction.Unpublished,
            "Route", route.Id,
            $"{(route.IsPublished ? "Published" : "Unpublished")} route {route.Slug}", ct);

        TempData["AdminMessage"] = route.IsPublished
            ? $"Route is live at /{route.Slug}."
            : "Route is now a draft and has been removed from the sitemap.";

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Both endpoints must exist and differ. Checked against the database
    /// rather than trusting the two ids that arrived in the form.
    /// </summary>
    private async Task<(Location From, Location To)?> ValidateEndpointsAsync(
        AdminRouteFormViewModel model, int? excludeId, CancellationToken ct)
    {
        if (model.FromLocationId == model.ToLocationId)
        {
            ModelState.AddModelError(nameof(model.ToLocationId),
                "A route needs two different cities.");
            return null;
        }

        var from = await _db.Locations.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == model.FromLocationId, ct);
        var to = await _db.Locations.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == model.ToLocationId, ct);

        if (from is null || to is null)
        {
            ModelState.AddModelError(string.Empty, "Choose two valid cities.");
            return null;
        }

        var duplicate = await _db.Routes.AnyAsync(r =>
            r.FromLocationId == from.Id && r.ToLocationId == to.Id &&
            (excludeId == null || r.Id != excludeId), ct);

        if (duplicate)
        {
            ModelState.AddModelError(string.Empty,
                $"A route from {from.Name} to {to.Name} already exists.");
            return null;
        }

        return (from, to);
    }

    private async Task<AdminRouteFormViewModel> PrepareAsync(
        AdminRouteFormViewModel model, CancellationToken ct)
    {
        model.Locations = await _db.Locations
            .AsNoTracking()
            .OrderBy(l => l.Name)
            .ToListAsync(ct);

        return model;
    }

    private static void Apply(AdminRouteFormViewModel model, MovingRoute route)
    {
        route.H1 = model.H1!.Trim();
        route.ShortDescription = model.ShortDescription!.Trim();
        route.Content = model.Content!.Trim();
        route.MetaTitle = model.MetaTitle!.Trim();
        route.MetaDescription = model.MetaDescription!.Trim();
        route.OgTitle = Normalise(model.OgTitle);
        route.OgDescription = Normalise(model.OgDescription);
        route.OgImage = Normalise(model.OgImage);
        route.IsPublished = model.IsPublished;
    }

    private static string? Normalise(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}