using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services.Seo;
using ShiftingGuru.ViewModels.Seo;

namespace ShiftingGuru.Controllers;

/// <summary>
/// One controller and two views for every city. Adding Jaipur is a database
/// row, not a new controller.
/// </summary>
[Route("locations")]
public class LocationsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IServiceCatalog _catalog;
    private readonly ISeoService _seo;

    public LocationsController(ApplicationDbContext db, IServiceCatalog catalog, ISeoService seo)
    {
        _db = db;
        _catalog = catalog;
        _seo = seo;
    }

    // GET /locations
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var locations = await _db.Locations
            .AsNoTracking()
            .Where(l => l.IsPublished)
            .OrderBy(l => l.Name)
            .Select(l => new LocationLinkViewModel { Slug = l.Slug, Name = l.Name, State = l.State })
            .ToListAsync(ct);

        ViewData["Seo"] = new SeoViewModel
        {
            Title = "Moving Services by City",
            Description = "Find moving and logistics services in cities across India. "
                        + "Compare quotes from verified professionals wherever you're moving.",
            Canonical = _seo.Canonical("/locations"),
            Breadcrumbs = new[]
            {
                new BreadcrumbViewModel("Home", _seo.Canonical("/")),
                new BreadcrumbViewModel("Locations")
            }
        };

        return View(new LocationIndexViewModel { Locations = locations });
    }

    // GET /locations/gurgaon
    [HttpGet("{slug}")]
    public async Task<IActionResult> Details(string slug, CancellationToken ct)
    {
        // Uppercase or trailing-slash variants redirect to the canonical form
        // once, rather than serving a duplicate page.
        var normalised = _seo.Slugify(slug);

        if (!string.Equals(slug, normalised, StringComparison.Ordinal))
        {
            return RedirectPermanent($"/locations/{normalised}");
        }

        var location = await _db.Locations
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Slug == normalised && l.IsPublished, ct);

        // Unpublished and nonexistent are the same 404. Draft pages must not
        // be reachable by guessing a slug.
        if (location is null) return NotFound();

        var routes = await _db.Routes
            .AsNoTracking()
            .Where(r => r.IsPublished
                     && (r.FromLocationId == location.Id || r.ToLocationId == location.Id)
                     && r.FromLocation!.IsPublished && r.ToLocation!.IsPublished)
            .OrderBy(r => r.FromLocation!.Name).ThenBy(r => r.ToLocation!.Name)
            .Select(r => new RouteLinkViewModel
            {
                Slug = r.Slug,
                FromName = r.FromLocation!.Name,
                ToName = r.ToLocation!.Name
            })
            .Take(12)
            .ToListAsync(ct);

        var others = await _db.Locations
            .AsNoTracking()
            .Where(l => l.IsPublished && l.Id != location.Id)
            .OrderBy(l => l.Name)
            .Select(l => new LocationLinkViewModel { Slug = l.Slug, Name = l.Name, State = l.State })
            .Take(8)
            .ToListAsync(ct);

        var faqs = await _db.Faqs
            .AsNoTracking()
            .Where(f => f.LocationId == location.Id && f.IsPublished)
            .OrderBy(f => f.DisplayOrder).ThenBy(f => f.Id)
            .Select(f => new FaqViewModel { Question = f.Question, Answer = f.Answer })
            .ToListAsync(ct);

        var canonical = _seo.Canonical($"/locations/{location.Slug}");

        var crumbs = new[]
        {
            new BreadcrumbViewModel("Home", _seo.Canonical("/")),
            new BreadcrumbViewModel("Locations", _seo.Canonical("/locations")),
            new BreadcrumbViewModel(location.Name)
        };

        var jsonLd = new List<string> { _seo.BreadcrumbJsonLd(crumbs) };

        // Only when FAQs are actually rendered below.
        if (_seo.FaqJsonLd(faqs) is { } faqJson) jsonLd.Add(faqJson);

        ViewData["Seo"] = new SeoViewModel
        {
            Title = location.MetaTitle,
            Description = location.MetaDescription,
            Canonical = canonical,
            OgTitle = location.OgTitle,
            OgDescription = location.OgDescription,
            OgImage = location.OgImage,
            Breadcrumbs = crumbs,
            JsonLdBlocks = jsonLd
        };

        return View(new LocationDetailsViewModel
        {
            Location = location,
            Routes = routes,
            OtherLocations = others,
            Faqs = faqs,
            Services = _catalog.GetAll()
        });
    }
}