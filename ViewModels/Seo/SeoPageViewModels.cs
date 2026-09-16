using ShiftingGuru.Models;

namespace ShiftingGuru.ViewModels.Seo;

/// <summary>One route, flattened for display.</summary>
public class RouteLinkViewModel
{
    public string Slug { get; set; } = "";
    public string FromName { get; set; } = "";
    public string ToName { get; set; } = "";

    public string Label => $"{FromName} to {ToName}";
    public string Url => $"/{Slug}";
}

public class LocationLinkViewModel
{
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string State { get; set; } = "";

    public string Url => $"/locations/{Slug}";
}

public class LocationIndexViewModel
{
    public IReadOnlyList<LocationLinkViewModel> Locations { get; set; }
        = Array.Empty<LocationLinkViewModel>();
}

public class LocationDetailsViewModel
{
    public Location Location { get; set; } = new();

    /// <summary>Routes starting or ending here. Published only.</summary>
    public IReadOnlyList<RouteLinkViewModel> Routes { get; set; }
        = Array.Empty<RouteLinkViewModel>();

    public IReadOnlyList<LocationLinkViewModel> OtherLocations { get; set; }
        = Array.Empty<LocationLinkViewModel>();

    public IReadOnlyList<FaqViewModel> Faqs { get; set; } = Array.Empty<FaqViewModel>();

    public IReadOnlyList<Service> Services { get; set; } = Array.Empty<Service>();

    /// <summary>Content split into paragraphs on blank lines.</summary>
    public IReadOnlyList<string> Paragraphs =>
        Location.Content.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

public class RouteIndexViewModel
{
    public IReadOnlyList<RouteLinkViewModel> Routes { get; set; }
        = Array.Empty<RouteLinkViewModel>();
}

public class RouteDetailsViewModel
{
    public MovingRoute Route { get; set; } = new();

    public IReadOnlyList<RouteLinkViewModel> RelatedRoutes { get; set; }
        = Array.Empty<RouteLinkViewModel>();

    public IReadOnlyList<FaqViewModel> Faqs { get; set; } = Array.Empty<FaqViewModel>();

    public IReadOnlyList<Service> Services { get; set; } = Array.Empty<Service>();

    public IReadOnlyList<string> Paragraphs =>
        Route.Content.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}