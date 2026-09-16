using ShiftingGuru.Models;

namespace ShiftingGuru.ViewModels.Partner;

public class PartnerQuoteDetailsViewModel
{
    public Quote Quote { get; set; } = new();
    public Lead Lead { get; set; } = new();
}