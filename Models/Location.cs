namespace ShiftingGuru.Models;

/// <summary>
/// A city with its own SEO landing page. Content is editable rather than
/// generated, so each page can say something genuinely different.
/// </summary>
public class Location
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    /// <summary>Lowercase URL segment, e.g. "gurgaon". Unique.</summary>
    public string Slug { get; set; } = "";

    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string Country { get; set; } = "India";

    public string ShortDescription { get; set; } = "";

    /// <summary>Body copy. Blank lines separate paragraphs.</summary>
    public string Content { get; set; } = "";

    public string H1 { get; set; } = "";
    public string MetaTitle { get; set; } = "";
    public string MetaDescription { get; set; } = "";

    public string? OgTitle { get; set; }
    public string? OgDescription { get; set; }
    public string? OgImage { get; set; }

    /// <summary>
    /// NEW: the city photo behind the hero, e.g. "media/locations/agra-3f2a....webp".
    /// Served at "/" + this path by MediaController. Null means the plain hero.
    /// </summary>
    public string? HeroImagePath { get; set; }

    /// <summary>Unpublished locations 404 publicly and stay out of the sitemap.</summary>
    public bool IsPublished { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<MovingRoute> RoutesFrom { get; set; } = new List<MovingRoute>();
    public ICollection<MovingRoute> RoutesTo { get; set; } = new List<MovingRoute>();
    public ICollection<Faq> Faqs { get; set; } = new List<Faq>();

    /// <summary>"Gurgaon, Haryana"</summary>
    public string Display => string.IsNullOrWhiteSpace(State) ? City : $"{City}, {State}";

    /// <summary>NEW: the public URL of the hero photo, or null.</summary>
    public string? HeroImageUrl => string.IsNullOrEmpty(HeroImagePath) ? null : "/" + HeroImagePath;
}