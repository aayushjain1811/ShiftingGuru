namespace ShiftingGuru.Models;

/// <summary>
/// A city-to-city corridor with its own landing page.
///
/// Named MovingRoute rather than Route because Microsoft.AspNetCore.Routing.Route
/// is in scope in every controller and view - a model called Route would make
/// every [Route("...")] attribute ambiguous. The database table is still
/// "Routes" and the URLs are unaffected.
/// </summary>
public class MovingRoute
{
    public int Id { get; set; }

    public int FromLocationId { get; set; }
    public Location? FromLocation { get; set; }

    public int ToLocationId { get; set; }
    public Location? ToLocation { get; set; }

    /// <summary>e.g. "delhi-to-bangalore". Unique, lowercase.</summary>
    public string Slug { get; set; } = "";

    public string H1 { get; set; } = "";
    public string ShortDescription { get; set; } = "";
    public string Content { get; set; } = "";

    public string MetaTitle { get; set; } = "";
    public string MetaDescription { get; set; } = "";

    public string? OgTitle { get; set; }
    public string? OgDescription { get; set; }
    public string? OgImage { get; set; }

    public bool IsPublished { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<Faq> Faqs { get; set; } = new List<Faq>();

    /// <summary>Builds the canonical slug from two location slugs.</summary>
    public static string BuildSlug(string fromSlug, string toSlug) =>
        $"{fromSlug.ToLowerInvariant()}-to-{toSlug.ToLowerInvariant()}";
}