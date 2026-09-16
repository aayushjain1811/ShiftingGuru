namespace ShiftingGuru.ViewModels.Seo;

/// <summary>
/// Everything the layout needs for the head of one page. Built in a controller
/// or the SEO service, never assembled inside a Razor view.
/// </summary>
public class SeoViewModel
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";

    /// <summary>Absolute, lowercase, no trailing slash, no query string.</summary>
    public string Canonical { get; set; } = "";

    /// <summary>"index, follow" for public pages; "noindex, nofollow" otherwise.</summary>
    public string Robots { get; set; } = "index, follow";

    public string? OgTitle { get; set; }
    public string? OgDescription { get; set; }
    public string? OgImage { get; set; }
    public string OgType { get; set; } = "website";

    public IReadOnlyList<BreadcrumbViewModel> Breadcrumbs { get; set; }
        = Array.Empty<BreadcrumbViewModel>();

    /// <summary>Extra JSON-LD blocks, already serialised.</summary>
    public IReadOnlyList<string> JsonLdBlocks { get; set; } = Array.Empty<string>();

    // Falls back to the page title and description when OG values aren't set,
    // so a share never renders blank.
    public string EffectiveOgTitle => OgTitle ?? Title;
    public string EffectiveOgDescription => OgDescription ?? Description;

    public static SeoViewModel NoIndex(string title) =>
        new() { Title = title, Robots = "noindex, nofollow" };
}

public class BreadcrumbViewModel
{
    public string Label { get; set; } = "";

    /// <summary>Absolute URL. Null for the current page, which isn't a link.</summary>
    public string? Url { get; set; }

    public BreadcrumbViewModel() { }

    public BreadcrumbViewModel(string label, string? url = null)
    {
        Label = label;
        Url = url;
    }
}

public class FaqViewModel
{
    public string Question { get; set; } = "";
    public string Answer { get; set; } = "";
}