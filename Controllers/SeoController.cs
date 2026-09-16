using Microsoft.AspNetCore.Mvc;
using ShiftingGuru.Services.Seo;

namespace ShiftingGuru.Controllers;

/// <summary>Technical SEO endpoints. XML and text generation lives in the service.</summary>
public class SeoController : Controller
{
    private readonly ISeoService _seo;

    public SeoController(ISeoService seo) => _seo = seo;

    // GET /sitemap.xml
    [HttpGet("/sitemap.xml")]
    [ResponseCache(Duration = 3600)]   // regenerated hourly at most
    public async Task<IActionResult> Sitemap(CancellationToken ct)
    {
        var entries = await _seo.GetSitemapEntriesAsync(ct);
        return Content(_seo.RenderSitemapXml(entries), "application/xml");
    }

    // GET /robots.txt
    [HttpGet("/robots.txt")]
    [ResponseCache(Duration = 86400)]
    public IActionResult Robots() => Content(_seo.RenderRobotsTxt(), "text/plain");
}