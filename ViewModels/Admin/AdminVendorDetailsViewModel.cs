using ShiftingGuru.Models;

namespace ShiftingGuru.ViewModels.Admin;

public class AdminVendorDetailsViewModel
{
    public Vendor Vendor { get; set; } = new();

    /// <summary>Only the transitions valid from the current status.</summary>
    public IReadOnlyList<VendorStatus> AllowedTransitions { get; set; } = Array.Empty<VendorStatus>();
}