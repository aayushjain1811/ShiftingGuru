using ShiftingGuru.Models;
using ShiftingGuru.ViewModels.Seo;

namespace ShiftingGuru.ViewModels;

/// <summary>
/// Everything Details.cshtml needs. Wrapping Service rather than passing it
/// directly leaves room for page-level extras (related services, breadcrumbs,
/// later on a pre-filled quote form) without changing the domain model.
/// </summary>
public class ServiceDetailsViewModel
{
    public Service Service { get; set; } = new();

    /// <summary>Other services shown at the foot of the page.</summary>
    public IReadOnlyList<Service> RelatedServices { get; set; } = Array.Empty<Service>();

    /// <summary>Service-specific FAQs, if any have been published.</summary>
    public IReadOnlyList<FaqViewModel> Faqs { get; set; } = Array.Empty<FaqViewModel>();

    /// <summary>Published city pages, for internal linking.</summary>
    public IReadOnlyList<LocationLinkViewModel> Locations { get; set; }
        = Array.Empty<LocationLinkViewModel>();

    public string CanonicalUrl => $"https://www.shiftingguru.com/services/{Service.Slug}";
}