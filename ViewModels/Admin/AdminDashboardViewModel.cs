using ShiftingGuru.Models;

namespace ShiftingGuru.ViewModels.Admin;

public class AdminDashboardViewModel
{
    public int TotalLeads { get; set; }

    /// <summary>Count per status, straight from a grouped database query.</summary>
    public IReadOnlyDictionary<LeadStatus, int> CountsByStatus { get; set; }
        = new Dictionary<LeadStatus, int>();

    public IReadOnlyList<Lead> RecentLeads { get; set; } = Array.Empty<Lead>();

    /// <summary>Leads with at least one active vendor assignment.</summary>
    public int AssignedLeads { get; set; }

    public int UnassignedLeads => Math.Max(0, TotalLeads - AssignedLeads);

    /// <summary>Quote counts by status, from one grouped query.</summary>
    public IReadOnlyDictionary<QuoteStatus, int> QuoteCounts { get; set; } = new Dictionary<QuoteStatus, int>();

    public int TotalQuotes => QuoteCounts.Values.Sum();
    public int QuotesFor(QuoteStatus status) => QuoteCounts.TryGetValue(status, out var c) ? c : 0;

    public int CountFor(LeadStatus status) =>
        CountsByStatus.TryGetValue(status, out var count) ? count : 0;
}