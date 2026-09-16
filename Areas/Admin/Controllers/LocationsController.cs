using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services;
using ShiftingGuru.Services.Seo;
using ShiftingGuru.ViewModels.Admin;

namespace ShiftingGuru.Areas.Admin.Controllers;

/// <summary>
/// CMS for city landing pages. Publishing here is what makes a page public,
/// put it in the sitemap and bring it into internal linking - there is no
/// separate step to remember.
/// </summary>
[Area("Admin")]
[Route("admin/locations")]
[Authorize(Roles = AdminSeeder.AdminRole)]
public class LocationsController : Controller
{
    private const int PageSize = 25;

    private readonly ApplicationDbContext _db;
    private readonly ISeoService _seo;
    private readonly IAuditService _audit;
    private readonly ILogger<LocationsController> _logger;

    public LocationsController(
        ApplicationDbContext db, ISeoService seo, IAuditService audit,
        ILogger<LocationsController> logger)
    {
        _db = db;
        _seo = seo;
        _audit = audit;
        _logger = logger;
    }

    // GET /admin/locations
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? search, bool? published, int page = 1, CancellationToken ct = default)
    {
        ViewData["Title"] = "Locations";
        if (page < 1) page = 1;

        var query = _db.Locations.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(l =>
                EF.Functions.ILike(l.Name, term) ||
                EF.Functions.ILike(l.Slug, term) ||
                EF.Functions.ILike(l.State, term));
        }

        if (published.HasValue) query = query.Where(l => l.IsPublished == published.Value);

        var total = await query.CountAsync(ct);
        var totalPages = total == 0 ? 1 : (int)Math.Ceiling(total / (double)PageSize);
        if (page > totalPages) page = totalPages;

        var locations = await query
            .OrderBy(l => l.Name)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(ct);

        return View(new AdminLocationListViewModel
        {
            Locations = locations,
            Search = search,
            Published = published,
            Page = page,
            PageSize = PageSize,
            TotalCount = total,
            DraftCount = await _db.Locations.AsNoTracking().CountAsync(l => !l.IsPublished, ct)
        });
    }

    // GET /admin/locations/create
    [HttpGet("create")]
    public IActionResult Create()
    {
        ViewData["Title"] = "New location";
        return View("Form", new AdminLocationFormViewModel { Country = "India" });
    }

    // POST /admin/locations/create
    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminLocationFormViewModel model, CancellationToken ct)
    {
        ViewData["Title"] = "New location";

        var slug = _seo.Slugify(model.Slug ?? "");

        if (await _db.Locations.AnyAsync(l => l.Slug == slug, ct))
        {
            ModelState.AddModelError(nameof(model.Slug), "That slug is already in use.");
        }

        if (!ModelState.IsValid) return View("Form", model);

        var location = new Location { CreatedAt = DateTime.UtcNow };
        Apply(model, location, slug);

        _db.Locations.Add(location);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Location create failed for slug {Slug}", slug);
            ModelState.AddModelError(string.Empty, "Couldn't save that location. Please try again.");
            return View("Form", model);
        }

        await _audit.RecordAsync(AuditAction.Created, nameof(Location), location.Id,
            $"Created location {location.Name}", ct);

        TempData["AdminMessage"] = $"Location {location.Name} created.";
        return RedirectToAction(nameof(Index));
    }

    // GET /admin/locations/5/edit
    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var location = await _db.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id, ct);
        if (location is null) return View("NotFound");

        ViewData["Title"] = $"Edit {location.Name}";

        return View("Form", new AdminLocationFormViewModel
        {
            Id = location.Id,
            Name = location.Name,
            Slug = location.Slug,
            City = location.City,
            State = location.State,
            Country = location.Country,
            H1 = location.H1,
            ShortDescription = location.ShortDescription,
            Content = location.Content,
            MetaTitle = location.MetaTitle,
            MetaDescription = location.MetaDescription,
            OgTitle = location.OgTitle,
            OgDescription = location.OgDescription,
            OgImage = location.OgImage,
            IsPublished = location.IsPublished
        });
    }

    // POST /admin/locations/5/edit
    [HttpPost("{id:int}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AdminLocationFormViewModel model, CancellationToken ct)
    {
        // The id comes from the route, not the form - a tampered hidden field
        // can't redirect the edit at another record.
        var location = await _db.Locations.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (location is null) return View("NotFound");

        model.Id = id;
        ViewData["Title"] = $"Edit {location.Name}";

        var slug = _seo.Slugify(model.Slug ?? "");

        if (await _db.Locations.AnyAsync(l => l.Slug == slug && l.Id != id, ct))
        {
            ModelState.AddModelError(nameof(model.Slug), "That slug is already in use.");
        }

        if (!ModelState.IsValid) return View("Form", model);

        Apply(model, location, slug);
        location.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Location update failed for {LocationId}", id);
            ModelState.AddModelError(string.Empty, "Couldn't save those changes. Please try again.");
            return View("Form", model);
        }

        await _audit.RecordAsync(AuditAction.Updated, nameof(Location), location.Id,
            $"Updated location {location.Name}", ct);

        TempData["AdminMessage"] = $"Location {location.Name} updated.";
        return RedirectToAction(nameof(Index));
    }

    // POST /admin/locations/5/publish
    [HttpPost("{id:int}/publish")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePublish(int id, CancellationToken ct)
    {
        var location = await _db.Locations.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (location is null) return View("NotFound");

        location.IsPublished = !location.IsPublished;
        location.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _audit.RecordAsync(
            location.IsPublished ? AuditAction.Published : AuditAction.Unpublished,
            nameof(Location), location.Id,
            $"{(location.IsPublished ? "Published" : "Unpublished")} location {location.Name}", ct);

        TempData["AdminMessage"] = location.IsPublished
            ? $"{location.Name} is live at /locations/{location.Slug}."
            : $"{location.Name} is now a draft and has been removed from the sitemap.";

        return RedirectToAction(nameof(Index));
    }

    private static void Apply(AdminLocationFormViewModel model, Location location, string slug)
    {
        location.Name = model.Name!.Trim();
        location.Slug = slug;
        location.City = model.City!.Trim();
        location.State = model.State?.Trim() ?? "";
        location.Country = model.Country.Trim();
        location.H1 = model.H1!.Trim();
        location.ShortDescription = model.ShortDescription!.Trim();
        location.Content = model.Content!.Trim();
        location.MetaTitle = model.MetaTitle!.Trim();
        location.MetaDescription = model.MetaDescription!.Trim();
        location.OgTitle = Normalise(model.OgTitle);
        location.OgDescription = Normalise(model.OgDescription);
        location.OgImage = Normalise(model.OgImage);
        location.IsPublished = model.IsPublished;
    }

    private static string? Normalise(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}