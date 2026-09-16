using ShiftingGuru.Models;

namespace ShiftingGuru.ViewModels.Admin;

public class AdminLeadDetailsViewModel
{
    public Lead Lead { get; set; } = new();

    /// <summary>
    /// Label/value pairs for the fields that actually apply to this lead's
    /// service. Empty fields are filtered out in the controller, so the view
    /// contains no business logic about which service uses which field.
    /// </summary>
    public IReadOnlyList<(string Label, string Value)> ServiceDetails { get; set; }
        = Array.Empty<(string, string)>();

    public IReadOnlyList<LeadStatus> AvailableStatuses { get; set; } = Array.Empty<LeadStatus>();

    /// <summary>Vendor assignment panel for this lead.</summary>
    public AdminLeadAssignmentViewModel Assignment { get; set; } = new();

    /// <summary>Vendor quotes against this lead, cheapest first.</summary>
    public IReadOnlyList<Quote> Quotes { get; set; } = Array.Empty<Quote>();
}