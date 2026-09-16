using ShiftingGuru.ViewModels.Seo;

namespace ShiftingGuru.Services.Seo;

/// <summary>One entry in the sitemap.</summary>
public record SitemapEntry(string Url, DateTime? LastModified, string ChangeFrequency, string Priority);

public interface ISeoService
{
    /// <summary>Absolute canonical URL for an app-relative path.</summary>
    string Canonical(string path);

    /// <summary>Turns free text into a URL-safe lowercase slug.</summary>
    string Slugify(string value);

    /// <summary>Every published public URL, for the sitemap.</summary>
    Task<IReadOnlyList<SitemapEntry>> GetSitemapEntriesAsync(CancellationToken ct = default);

    string RenderSitemapXml(IReadOnlyList<SitemapEntry> entries);

    string RenderRobotsTxt();

    /// <summary>BreadcrumbList JSON-LD for a visible breadcrumb trail.</summary>
    string BreadcrumbJsonLd(IReadOnlyList<BreadcrumbViewModel> crumbs);

    /// <summary>FAQPage JSON-LD. Returns null when there are no visible FAQs.</summary>
    string? FaqJsonLd(IReadOnlyList<FaqViewModel> faqs);
}