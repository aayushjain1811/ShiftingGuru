using ShiftingGuru.Models;

namespace ShiftingGuru.ViewModels.Admin;

public class AdminQuoteDetailsViewModel
{
    public Quote Quote { get; set; } = new();
    public Lead Lead { get; set; } = new();
    public Vendor Vendor { get; set; } = new();

    public IReadOnlyList<(string Label, string Value)> ServiceDetails { get; set; }
        = Array.Empty<(string, string)>();

    /// <summary>Only the status moves that are legal from here.</summary>
    public IReadOnlyList<QuoteStatus> AllowedTransitions { get; set; } = Array.Empty<QuoteStatus>();
}