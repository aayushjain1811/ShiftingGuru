using ShiftingGuru.Models;

namespace ShiftingGuru.ViewModels.Admin;

/// <summary>
/// Holds the current page of leads plus the filter state, so the form can be
/// re-rendered with the user's choices still selected.
/// </summary>
public class AdminLeadListViewModel
{
    public IReadOnlyList<Lead> Leads { get; set; } = Array.Empty<Lead>();

    // ---- filter state (echoed back into the form) ----
    public string? Search { get; set; }
    public string? ServiceSlug { get; set; }
    public LeadStatus? Status { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }

    /// <summary>null = all, true = assigned, false = unassigned.</summary>
    public bool? Assigned { get; set; }

    /// <summary>Active assignment count per lead id, from one grouped query.</summary>
    public IReadOnlyDictionary<int, int> AssignmentCounts { get; set; } = new Dictionary<int, int>();

    public int CountFor(int leadId) =>
        AssignmentCounts.TryGetValue(leadId, out var count) ? count : 0;

    // ---- paging ----
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public int TotalCount { get; set; }

    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;

    public IReadOnlyList<Service> Services { get; set; } = Array.Empty<Service>();

    /// <summary>Rebuilds the query string for paging links, keeping filters.</summary>
    public IDictionary<string, string?> RouteValues(int page) => new Dictionary<string, string?>
    {
        ["search"] = Search,
        ["service"] = ServiceSlug,
        ["status"] = Status?.ToString(),
        ["assigned"] = Assigned?.ToString().ToLowerInvariant(),
        ["from"] = FromDate?.ToString("yyyy-MM-dd"),
        ["to"] = ToDate?.ToString("yyyy-MM-dd"),
        ["page"] = page.ToString()
    };
}