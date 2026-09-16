using ShiftingGuru.Models;

namespace ShiftingGuru.ViewModels.Admin;

public class AdminVendorListViewModel
{
    public IReadOnlyList<Vendor> Vendors { get; set; } = Array.Empty<Vendor>();

    public string? Search { get; set; }
    public VendorStatus? Status { get; set; }
    public string? ServiceSlug { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public int TotalCount { get; set; }

    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;

    public IReadOnlyList<Service> Services { get; set; } = Array.Empty<Service>();

    public IDictionary<string, string?> RouteValues(int page) => new Dictionary<string, string?>
    {
        ["search"] = Search,
        ["status"] = Status?.ToString(),
        ["service"] = ServiceSlug,
        ["page"] = page.ToString()
    };
}