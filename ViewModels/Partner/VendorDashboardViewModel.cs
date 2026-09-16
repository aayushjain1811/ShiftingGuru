using ShiftingGuru.Models;

namespace ShiftingGuru.ViewModels.Partner;

public class VendorDashboardViewModel
{
    public Vendor Vendor { get; set; } = new();

    /// <summary>Active assignments held by this vendor. From the database.</summary>
    public int AssignedLeads { get; set; }

    /// <summary>Assigned but not yet opened.</summary>
    public int NewLeads { get; set; }

    /// <summary>Opened at least once.</summary>
    public int ViewedLeads { get; set; }

    /// <summary>Quotes this vendor has submitted. From the database.</summary>
    public int QuotesSubmitted { get; set; }

    public int AcceptedQuotes { get; set; }

    /// <summary>Public rating, from approved reviews only. Null when none.</summary>
    public double? AverageRating { get; set; }
    public int ReviewCount { get; set; }

    /// <summary>Rough percentage of optional profile fields filled in.</summary>
    public int ProfileCompletion { get; set; }

    public IReadOnlyList<string> MissingProfileFields { get; set; } = Array.Empty<string>();
}