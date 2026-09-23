using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services;
using ShiftingGuru.Services.Seo;
using ShiftingGuru.Services.Storage;
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

    // NEW: a hero photo is the biggest thing on the page. Past ~2 MB it
    // visibly slows the page on mobile and hurts the Google speed score.
    private const long MaxHeroBytes = 2 * 1024 * 1024;

    private readonly ApplicationDbContext _db;
    private readonly ISeoService _seo;
    private readonly IAuditService _audit;
    private readonly IDocumentStorage _storage;
    private readonly ILogger<LocationsController> _logger;

    public LocationsController(
        ApplicationDbContext db, ISeoService seo, IAuditService audit,
        IDocumentStorage storage, ILogger<LocationsController> logger)
    {
        _db = db;
        _seo = seo;
        _audit = audit;
        _storage = storage;
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
    [RequestSizeLimit(4 * 1024 * 1024)]
    public async Task<IActionResult> Create(
        AdminLocationFormViewModel model, IFormFile? heroPhoto, CancellationToken ct)
    {
        ViewData["Title"] = "New location";

        var slug = _seo.Slugify(model.Slug ?? "");

        if (await _db.Locations.AnyAsync(l => l.Slug == slug, ct))
        {
            ModelState.AddModelError(nameof(model.Slug), "That slug is already in use.");
        }

        var photo = await CheckHeroPhotoAsync(heroPhoto, ct);

        if (!ModelState.IsValid) return View("Form", model);

        var location = new Location { CreatedAt = DateTime.UtcNow };
        Apply(model, location, slug);

        string? uploaded = null;
        if (heroPhoto is not null && photo is not null)
        {
            uploaded = await UploadHeroAsync(heroPhoto, photo, slug, ct);
            if (uploaded is null) return View("Form", model);
            location.HeroImagePath = uploaded;
        }

        _db.Locations.Add(location);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Location create failed for slug {Slug}", slug);
            await DeleteQuietlyAsync(uploaded);
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
        ViewData["HeroImageUrl"] = location.HeroImageUrl;

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
    [RequestSizeLimit(4 * 1024 * 1024)]
    public async Task<IActionResult> Edit(
        int id, AdminLocationFormViewModel model, IFormFile? heroPhoto, bool removeHeroPhoto,
        CancellationToken ct)
    {
        // The id comes from the route, not the form - a tampered hidden field
        // can't redirect the edit at another record.
        var location = await _db.Locations.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (location is null) return View("NotFound");

        model.Id = id;
        ViewData["Title"] = $"Edit {location.Name}";
        ViewData["HeroImageUrl"] = location.HeroImageUrl;

        var oldHeroPath = location.HeroImagePath;
        var slug = _seo.Slugify(model.Slug ?? "");

        if (await _db.Locations.AnyAsync(l => l.Slug == slug && l.Id != id, ct))
        {
            ModelState.AddModelError(nameof(model.Slug), "That slug is already in use.");
        }

        var photo = await CheckHeroPhotoAsync(heroPhoto, ct);

        if (!ModelState.IsValid) return View("Form", model);

        Apply(model, location, slug);
        location.UpdatedAt = DateTime.UtcNow;

        // A new photo wins over "remove".
        string? uploaded = null;
        if (heroPhoto is not null && photo is not null)
        {
            uploaded = await UploadHeroAsync(heroPhoto, photo, slug, ct);
            if (uploaded is null) return View("Form", model);
            location.HeroImagePath = uploaded;
        }
        else if (removeHeroPhoto)
        {
            location.HeroImagePath = null;
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Location update failed for {LocationId}", id);
            await DeleteQuietlyAsync(uploaded);
            ModelState.AddModelError(string.Empty, "Couldn't save those changes. Please try again.");
            return View("Form", model);
        }

        // Only after the database points at the new photo (or none) is the
        // old file removed, so the live page never shows a broken image.
        if (oldHeroPath is not null && oldHeroPath != location.HeroImagePath)
        {
            await DeleteQuietlyAsync(oldHeroPath);
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

    // -----------------------------------------------------------------

    /// <summary>NEW: null with no photo chosen, or when the photo is rejected (error added).</summary>
    private async Task<InspectedFile?> CheckHeroPhotoAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return null;

        if (file.Length > MaxHeroBytes)
        {
            ModelState.AddModelError("heroPhoto",
                "That photo is larger than 2 MB. Export it about 1920 pixels wide as WEBP or JPG.");
            return null;
        }

        // Checks the file's real contents, not its name.
        var info = await DocumentInspector.InspectAsync(file, allowPdf: false, ct);
        if (info is null)
        {
            ModelState.AddModelError("heroPhoto", "Upload a JPG, PNG or WEBP photo.");
        }
        return info;
    }

    /// <summary>NEW: stores the photo under a fresh name. Null (with an error) if storage fails.</summary>
    private async Task<string?> UploadHeroAsync(
        IFormFile file, InspectedFile info, string slug, CancellationToken ct)
    {
        // A new name every time, so browsers that cached the old photo for a
        // year still see the new one immediately.
        var path = $"media/locations/{slug}-{Guid.NewGuid():N}{info.Extension}";

        try
        {
            await using var content = file.OpenReadStream();
            await _storage.SaveAsync(path, content, info.ContentType, ct);
            return path;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Couldn't store hero photo for location {Slug}", slug);
            ModelState.AddModelError("heroPhoto", "Couldn't upload that photo. Please try again.");
            return null;
        }
    }

    private async Task DeleteQuietlyAsync(string? path)
    {
        if (path is null) return;

        try
        {
            await _storage.DeleteAsync(path, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Couldn't delete old hero photo {Path}", path);
        }
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