using ShiftingGuru.Models;

namespace ShiftingGuru.ViewModels.Partner;

/// <summary>Projected row - no customer contact details in the list query.</summary>
public class PartnerQuoteRow
{
    public int QuoteId { get; set; }
    public string QuoteNumber { get; set; } = "";
    public string LeadNumber { get; set; } = "";
    public string ServiceName { get; set; } = "";
    public string? MovingFrom { get; set; }
    public string? MovingTo { get; set; }
    public string? StorageLocation { get; set; }
    public decimal TotalAmount { get; set; }
    public QuoteStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PartnerQuoteListViewModel
{
    public IReadOnlyList<PartnerQuoteRow> Quotes { get; set; } = Array.Empty<PartnerQuoteRow>();

    public QuoteStatus? Status { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalCount { get; set; }

    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}