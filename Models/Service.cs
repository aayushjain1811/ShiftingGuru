namespace ShiftingGuru.Models;

/// <summary>
/// A service ShiftingGuru brokers leads for.
/// Deliberately a plain class with no data-access concerns, so the same shape
/// can later be mapped straight onto an EF Core entity without touching views.
/// </summary>
public class Service
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    /// <summary>URL segment, e.g. "home-shifting" for /services/home-shifting.</summary>
    public string Slug { get; set; } = "";

    /// <summary>One line, used on cards.</summary>
    public string ShortDescription { get; set; } = "";

    /// <summary>SVG path data for the line icon. Stored as data so the icon
    /// travels with the service instead of living in a switch statement.</summary>
    public string IconPath { get; set; } = "";

    public string HeroTitle { get; set; } = "";

    public string HeroDescription { get; set; } = "";

    public string MetaTitle { get; set; } = "";

    public string MetaDescription { get; set; } = "";

    /// <summary>Overview paragraphs, rendered in order.</summary>
    public IReadOnlyList<string> Overview { get; set; } = Array.Empty<string>();

    /// <summary>"What this service includes".</summary>
    public IReadOnlyList<ServiceItem> Features { get; set; } = Array.Empty<ServiceItem>();

    /// <summary>Benefits specific to this service.</summary>
    public IReadOnlyList<ServiceItem> Benefits { get; set; } = Array.Empty<ServiceItem>();

    public IReadOnlyList<FaqItem> Faqs { get; set; } = Array.Empty<FaqItem>();

    /// <summary>Inactive services stay out of listings and return 404.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Given a double-width tile in the homepage grid.</summary>
    public bool IsFeatured { get; set; }
}

public class ServiceItem
{
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
}

public class FaqItem
{
    public string Question { get; set; } = "";
    public string Answer { get; set; } = "";
}