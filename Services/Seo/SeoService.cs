using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShiftingGuru.Data;
using ShiftingGuru.Services.Email;
using ShiftingGuru.ViewModels.Seo;

namespace ShiftingGuru.Services.Seo;

public partial class SeoService : ISeoService
{
    private readonly ApplicationDbContext _db;
    private readonly IServiceCatalog _catalog;
    private readonly AppOptions _app;

    private static readonly JsonSerializerOptions JsonLdOptions = new()
    {
        // Relaxed encoder keeps readable output; every value still goes through
        // JSON string escaping, so a quote in content can't break the block.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    public SeoService(ApplicationDbContext db, IServiceCatalog catalog, IOptions<AppOptions> app)
    {
        _db = db;
        _catalog = catalog;
        _app = app.Value;
    }

    /// <summary>
    /// One normalisation rule for the whole site: absolute, lowercase, no
    /// trailing slash, no query string. Every canonical goes through here, so
    /// duplicates can't creep in page by page.
    /// </summary>
    public string Canonical(string path)
    {
        var clean = (path ?? "/").Split('?')[0].Split('#')[0].Trim().ToLowerInvariant();

        if (!clean.StartsWith('/')) clean = "/" + clean;
        if (clean.Length > 1) clean = clean.TrimEnd('/');

        return _app.BaseUrl.TrimEnd('/') + clean;
    }

    public string Slugify(string value)
    {
        var lower = (value ?? "").Trim().ToLowerInvariant();
        var hyphenated = NonSlugCharacters().Replace(lower, "-");

        return CollapseHyphens().Replace(hyphenated, "-").Trim('-');
    }

    public async Task<IReadOnlyList<SitemapEntry>> GetSitemapEntriesAsync(CancellationToken ct = default)
    {
        var entries = new List<SitemapEntry>
        {
            new(Canonical("/"), null, "weekly", "1.0"),
            new(Canonical("/services"), null, "monthly", "0.8"),
            new(Canonical("/locations"), null, "weekly", "0.7"),
            new(Canonical("/routes"), null, "weekly", "0.7"),
            new(Canonical("/quote"), null, "monthly", "0.9"),
            new(Canonical("/join-as-partner"), null, "monthly", "0.5")
        };

        foreach (var service in _catalog.GetAll())
        {
            entries.Add(new SitemapEntry(
                Canonical($"/services/{service.Slug}"), null, "monthly", "0.8"));
        }

        // Published only. Draft pages and every private area stay out.
        var locations = await _db.Locations
            .AsNoTracking()
            .Where(l => l.IsPublished)
            .Select(l => new { l.Slug, l.UpdatedAt, l.CreatedAt })
            .ToListAsync(ct);

        entries.AddRange(locations.Select(l => new SitemapEntry(
            Canonical($"/locations/{l.Slug}"), l.UpdatedAt ?? l.CreatedAt, "monthly", "0.7")));

        var routes = await _db.Routes
            .AsNoTracking()
            .Where(r => r.IsPublished && r.FromLocation!.IsPublished && r.ToLocation!.IsPublished)
            .Select(r => new { r.Slug, r.UpdatedAt, r.CreatedAt })
            .ToListAsync(ct);

        entries.AddRange(routes.Select(r => new SitemapEntry(
            Canonical($"/{r.Slug}"), r.UpdatedAt ?? r.CreatedAt, "monthly", "0.7")));

        return entries;
    }

    public string RenderSitemapXml(IReadOnlyList<SitemapEntry> entries)
    {
        var xml = new StringBuilder();

        xml.AppendLine("""<?xml version="1.0" encoding="UTF-8"?>""");
        xml.AppendLine("""<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">""");

        foreach (var entry in entries)
        {
            xml.AppendLine("  <url>");
            xml.AppendLine($"    <loc>{System.Security.SecurityElement.Escape(entry.Url)}</loc>");

            if (entry.LastModified.HasValue)
            {
                xml.AppendLine($"    <lastmod>{entry.LastModified.Value:yyyy-MM-dd}</lastmod>");
            }

            xml.AppendLine($"    <changefreq>{entry.ChangeFrequency}</changefreq>");
            xml.AppendLine($"    <priority>{entry.Priority}</priority>");
            xml.AppendLine("  </url>");
        }

        xml.AppendLine("</urlset>");
        return xml.ToString();
    }

    /// <summary>
    /// Private areas are disallowed; CSS, JS and images are deliberately left
    /// crawlable, because blocking them stops Google rendering public pages.
    /// </summary>
    public string RenderRobotsTxt() =>
        $"""
        User-agent: *
        Allow: /

        Disallow: /admin
        Disallow: /partner
        Disallow: /my-request
        Disallow: /quote/success

        Sitemap: {Canonical("/sitemap.xml")}
        """;

    public string BreadcrumbJsonLd(IReadOnlyList<BreadcrumbViewModel> crumbs)
    {
        var items = crumbs.Select((crumb, index) => new Dictionary<string, object>
        {
            ["@type"] = "ListItem",
            ["position"] = index + 1,
            ["name"] = crumb.Label,
            ["item"] = crumb.Url ?? ""
        }).ToList();

        return Serialise(new Dictionary<string, object>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "BreadcrumbList",
            ["itemListElement"] = items
        });
    }

    public string? FaqJsonLd(IReadOnlyList<FaqViewModel> faqs)
    {
        // No visible FAQs means no FAQ schema. Marking up questions that
        // aren't on the page is exactly what search engines penalise.
        if (faqs.Count == 0) return null;

        var entities = faqs.Select(faq => new Dictionary<string, object>
        {
            ["@type"] = "Question",
            ["name"] = faq.Question,
            ["acceptedAnswer"] = new Dictionary<string, object>
            {
                ["@type"] = "Answer",
                ["text"] = faq.Answer
            }
        }).ToList();

        return Serialise(new Dictionary<string, object>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "FAQPage",
            ["mainEntity"] = entities
        });
    }

    /// <summary>
    /// Serialised by System.Text.Json, then "&lt;" escaped - the one sequence
    /// that could close the script tag early from inside a JSON string.
    /// </summary>
    private static string Serialise(object value) =>
        JsonSerializer.Serialize(value, JsonLdOptions).Replace("<", "\\u003c");

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NonSlugCharacters();

    [GeneratedRegex(@"-{2,}")]
    private static partial Regex CollapseHyphens();
}