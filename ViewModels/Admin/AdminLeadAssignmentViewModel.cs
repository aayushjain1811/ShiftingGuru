using ShiftingGuru.Models;

namespace ShiftingGuru.ViewModels.Admin;

/// <summary>How well a vendor fits a particular lead.</summary>
public enum MatchLevel
{
    None,
    Partial,
    Match
}

/// <summary>One candidate vendor row in the assignment table.</summary>
public class AdminVendorAssignmentItemViewModel
{
    public int VendorId { get; set; }
    public string VendorNumber { get; set; } = "";
    public string BusinessName { get; set; } = "";
    public string City { get; set; } = "";
    public string? OperatingLocations { get; set; }

    public MatchLevel ServiceMatch { get; set; }
    public MatchLevel LocationMatch { get; set; }

    /// <summary>True when an active assignment already exists for this lead.</summary>
    public bool AlreadyAssigned { get; set; }
}

/// <summary>The assignment panel on the admin lead details page.</summary>
public class AdminLeadAssignmentViewModel
{
    public IReadOnlyList<LeadAssignment> Existing { get; set; } = Array.Empty<LeadAssignment>();

    public IReadOnlyList<AdminVendorAssignmentItemViewModel> Candidates { get; set; }
        = Array.Empty<AdminVendorAssignmentItemViewModel>();

    /// <summary>True when nothing matched and we're showing every approved vendor.</summary>
    public bool ShowingAllApproved { get; set; }

    public int ApprovedVendorCount { get; set; }
}