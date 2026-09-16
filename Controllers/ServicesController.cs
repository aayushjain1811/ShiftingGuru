using Microsoft.AspNetCore.Mvc;
using ShiftingGuru.Data;
using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Services.Seo;
using ShiftingGuru.ViewModels;
using ShiftingGuru.ViewModels.Seo;

namespace ShiftingGuru.Controllers;

/// <summary>
/// One controller for every service. The slug in the URL selects the data;
/// the view is the same for all of them.
/// </summary>
[Route("services")]
public class ServicesController : Controller
{
    private readonly IServiceCatalog _catalog;
    private readonly ApplicationDbContext _db;
    private readonly ISeoService _seo;

    public ServicesController(IServiceCatalog catalog, ApplicationDbContext db, ISeoService seo)
    {
        _catalog = catalog;
        _db = db;
        _seo = seo;
    }

    // GET /services
    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Seo"] = new SeoViewModel
        {
            Title = "Moving & Logistics Services",
            Description = "Browse ShiftingGuru's moving and logistics services. Home shifting, "
                        + "office relocation, vehicle transport, goods transportation and storage.",
            Canonical = _seo.Canonical("/services"),
            Breadcrumbs = new[]
            {
                new BreadcrumbViewModel("Home", _seo.Canonical("/")),
                new BreadcrumbViewModel("Services")
            }
        };

        return View(new ServicesIndexViewModel { Services = _catalog.GetAll() });
    }

    // GET /services/home-shifting
    [HttpGet("{slug}")]
    public async Task<IActionResult> Details(string slug, CancellationToken ct = default)
    {
        var service = _catalog.GetBySlug(slug);

        // Unknown or inactive slug returns a real 404 rather than an empty page,
        // which matters for SEO as much as for users.
        if (service is null) return NotFound();

        // Canonical slug check: /services/Home-Shifting redirects to the
        // lowercase form so search engines only see one URL per service.
        if (!string.Equals(slug, service.Slug, StringComparison.Ordinal))
        {
            return RedirectToActionPermanent(nameof(Details), new { slug = service.Slug });
        }

        var model = new ServiceDetailsViewModel
        {
            Service = service,
            RelatedServices = _catalog.GetAll()
                .Where(s => s.Id != service.Id)
                .Take(3)
                .ToList()
        };

        // Service-specific FAQs, if an admin has written any.
        var faqs = await _db.Faqs
            .AsNoTracking()
            .Where(f => f.ServiceSlug == service.Slug && f.IsPublished)
            .OrderBy(f => f.DisplayOrder).ThenBy(f => f.Id)
            .Select(f => new FaqViewModel { Question = f.Question, Answer = f.Answer })
            .ToListAsync(ct);

        model.Faqs = faqs;

        // Cities with published pages, so service pages link into the cluster.
        model.Locations = await _db.Locations
            .AsNoTracking()
            .Where(l => l.IsPublished)
            .OrderBy(l => l.Name)
            .Select(l => new LocationLinkViewModel { Slug = l.Slug, Name = l.Name, State = l.State })
            .Take(10)
            .ToListAsync(ct);

        var crumbs = new[]
        {
            new BreadcrumbViewModel("Home", _seo.Canonical("/")),
            new BreadcrumbViewModel("Services", _seo.Canonical("/services")),
            new BreadcrumbViewModel(service.Name)
        };

        var jsonLd = new List<string>
        {
            _seo.BreadcrumbJsonLd(crumbs),

            // Service schema built only from real catalog values.
            System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["@context"] = "https://schema.org",
                ["@type"] = "Service",
                ["name"] = service.Name,
                ["serviceType"] = service.Name,
                ["description"] = service.MetaDescription,
                ["url"] = _seo.Canonical($"/services/{service.Slug}"),
                ["areaServed"] = "IN",
                ["provider"] = new Dictionary<string, object>
                {
                    ["@type"] = "Organization",
                    ["name"] = "ShiftingGuru"
                }
            }).Replace("<", "\\u003c")
        };

        if (_seo.FaqJsonLd(faqs) is { } faqJson) jsonLd.Add(faqJson);

        ViewData["Seo"] = new SeoViewModel
        {
            Title = service.MetaTitle,
            Description = service.MetaDescription,
            Canonical = _seo.Canonical($"/services/{service.Slug}"),
            Breadcrumbs = crumbs,
            JsonLdBlocks = jsonLd
        };

        return View(model);
    }
}