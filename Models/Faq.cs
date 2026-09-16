namespace ShiftingGuru.Models;

/// <summary>
/// A question attached to exactly one of: a location, a route, or a service.
/// All three foreign keys nullable, with a check that exactly one is set -
/// simpler than three near-identical tables.
/// </summary>
public class Faq
{
    public int Id { get; set; }

    public string Question { get; set; } = "";
    public string Answer { get; set; } = "";

    public int? LocationId { get; set; }
    public Location? Location { get; set; }

    public int? RouteId { get; set; }
    public MovingRoute? Route { get; set; }

    /// <summary>References the in-memory service catalog, not a table.</summary>
    public string? ServiceSlug { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsPublished { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Exactly one owner. Enforced in the database too.</summary>
    public bool HasSingleOwner
    {
        get
        {
            var owners = 0;
            if (LocationId.HasValue) owners++;
            if (RouteId.HasValue) owners++;
            if (!string.IsNullOrWhiteSpace(ServiceSlug)) owners++;
            return owners == 1;
        }
    }
}