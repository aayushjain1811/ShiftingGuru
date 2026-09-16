using ShiftingGuru.Models;

namespace ShiftingGuru.ViewModels.Partner;

public class PartnerLeadDetailsViewModel
{
    public Lead Lead { get; set; } = new();

    public LeadAssignment Assignment { get; set; } = new();

    /// <summary>Only the requirement fields that apply to this lead's service.</summary>
    public IReadOnlyList<(string Label, string Value)> ServiceDetails { get; set; }
        = Array.Empty<(string, string)>();

    /// <summary>This vendor's most recent quote on this lead, if any.</summary>
    public Quote? ExistingQuote { get; set; }

    public bool HasActiveQuote =>
        ExistingQuote is not null && Quote.ActiveStatuses.Contains(ExistingQuote.Status);
}