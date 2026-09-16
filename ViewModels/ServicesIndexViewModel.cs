using ShiftingGuru.Models;

namespace ShiftingGuru.ViewModels;

public class ServicesIndexViewModel
{
    public IReadOnlyList<Service> Services { get; set; } = Array.Empty<Service>();
}