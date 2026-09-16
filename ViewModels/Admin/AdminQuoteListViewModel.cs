using ShiftingGuru.Models;

namespace ShiftingGuru.ViewModels.Admin;

public class AdminQuoteRow
{
    public int QuoteId { get; set; }
    public string QuoteNumber { get; set; } = "";
    public string LeadNumber { get; set; } = "";
    public string VendorName { get; set; } = "";
    public string ServiceName { get; set; } = "";
    public string? MovingFrom { get; set; }
    public string? MovingTo { get; set; }
    public string? StorageLocation { get; set; }
    public decimal TotalAmount { get; set; }
    public QuoteStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AdminQuoteListViewModel
{
    public IReadOnlyList<AdminQuoteRow> Quotes { get; set; } = Array.Empty<AdminQuoteRow>();

    public string? Search { get; set; }
    public QuoteStatus? Status { get; set; }
    public string? ServiceSlug { get; set; }
    public int? VendorId { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public int TotalCount { get; set; }

    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;

    public IReadOnlyList<Service> Services { get; set; } = Array.Empty<Service>();
    public IReadOnlyList<(int Id, string Name)> Vendors { get; set; } = Array.Empty<(int, string)>();

    public IDictionary<string, string?> RouteValues(int page) => new Dictionary<string, string?>
    {
        ["search"] = Search,
        ["status"] = Status?.ToString(),
        ["service"] = ServiceSlug,
        ["vendorId"] = VendorId?.ToString(),
        ["from"] = FromDate?.ToString("yyyy-MM-dd"),
        ["to"] = ToDate?.ToString("yyyy-MM-dd"),
        ["page"] = page.ToString()
    };
}