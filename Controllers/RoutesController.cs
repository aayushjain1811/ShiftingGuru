using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Services.Seo;
using ShiftingGuru.ViewModels.Seo;

namespace ShiftingGuru.Controllers;

public class RoutesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IServiceCatalog _catalog;
    private readonly ISeoService _seo;

    public RoutesController(ApplicationDbContext db, IServiceCatalog catalog, ISeoService seo)
    {
        _db = db;
        _catalog = catalog;
        _seo = seo;
    }

    // GET /routes
    [HttpGet("/routes")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var routes = await _db.Routes
            .AsNoTracking()
            .Where(r => r.IsPublished && r.FromLocation!.IsPublished && r.ToLocation!.IsPublished)
            .OrderBy(r => r.FromLocation!.Name).ThenBy(r => r.ToLocation!.Name)
            .Select(r => new RouteLinkViewModel
            {
                Slug = r.Slug,
                FromName = r.FromLocation!.Name,
                ToName = r.ToLocation!.Name
            })
            .ToListAsync(ct);

        ViewData["Seo"] = new SeoViewModel
        {
            Title = "Popular Moving Routes",
            Description = "Compare moving and logistics quotes on long-distance routes across India.",
            Canonical = _seo.Canonical("/routes"),
            Breadcrumbs = new[]
            {
                new BreadcrumbViewModel("Home", _seo.Canonical("/")),
                new BreadcrumbViewModel("Routes")
            }
        };

        return View(new RouteIndexViewModel { Routes = routes });
    }

    // GET /delhi-to-bangalore
    //
    // Sits at the site root, so the pattern is constrained to "<words>-to-<words>".
    // Without that, this action would swallow /quote, /services and every other
    // single-segment URL.
    //
    // The brackets are doubled because [ and ] are token delimiters in a route
    // template - [[ ]] is how you write a literal bracket. Routing unescapes
    // them before the regex engine sees the pattern.
    [HttpGet("/{slug:regex(^[[a-z0-9]]+(-[[a-z0-9]]+)*-to-[[a-z0-9]]+(-[[a-z0-9]]+)*$)}")]
    public async Task<IActionResult> Details(string slug, CancellationToken ct)
    {
        var route = await _db.Routes
            .AsNoTracking()
            .Include(r => r.FromLocation)
            .Include(r => r.ToLocation)
            .FirstOrDefaultAsync(r => r.Slug == slug
                                   && r.IsPublished
                                   && r.FromLocation!.IsPublished
                                   && r.ToLocation!.IsPublished, ct);

        if (route?.FromLocation is null || route.ToLocation is null) return NotFound();

        // Other corridors from the same origin, plus the reverse direction.
        var related = await _db.Routes
            .AsNoTracking()
            .Where(r => r.Id != route.Id
                     && r.IsPublished
                     && r.FromLocation!.IsPublished && r.ToLocation!.IsPublished
                     && (r.FromLocationId == route.FromLocationId
                      || r.ToLocationId == route.ToLocationId
                      || (r.FromLocationId == route.ToLocationId && r.ToLocationId == route.FromLocationId)))
            .OrderBy(r => r.FromLocation!.Name).ThenBy(r => r.ToLocation!.Name)
            .Select(r => new RouteLinkViewModel
            {
                Slug = r.Slug,
                FromName = r.FromLocation!.Name,
                ToName = r.ToLocation!.Name
            })
            .Take(8)
            .ToListAsync(ct);

        var faqs = await _db.Faqs
            .AsNoTracking()
            .Where(f => f.RouteId == route.Id && f.IsPublished)
            .OrderBy(f => f.DisplayOrder).ThenBy(f => f.Id)
            .Select(f => new FaqViewModel { Question = f.Question, Answer = f.Answer })
            .ToListAsync(ct);

        var crumbs = new[]
        {
            new BreadcrumbViewModel("Home", _seo.Canonical("/")),
            new BreadcrumbViewModel("Routes", _seo.Canonical("/routes")),
            new BreadcrumbViewModel($"{route.FromLocation.Name} to {route.ToLocation.Name}")
        };

        var jsonLd = new List<string> { _seo.BreadcrumbJsonLd(crumbs) };
        if (_seo.FaqJsonLd(faqs) is { } faqJson) jsonLd.Add(faqJson);

        ViewData["Seo"] = new SeoViewModel
        {
            Title = route.MetaTitle,
            Description = route.MetaDescription,
            Canonical = _seo.Canonical($"/{route.Slug}"),
            OgTitle = route.OgTitle,
            OgDescription = route.OgDescription,
            OgImage = route.OgImage,
            Breadcrumbs = crumbs,
            JsonLdBlocks = jsonLd
        };

        return View(new RouteDetailsViewModel
        {
            Route = route,
            RelatedRoutes = related,
            Faqs = faqs,
            Services = _catalog.GetAll()
        });
    }
}