using ShiftingGuru.Models;

namespace ShiftingGuru.ViewModels.Partner;

/// <summary>
/// A projected row - only the columns the list needs, so the query doesn't
/// pull full Lead rows including customer contact details.
/// </summary>
public class PartnerLeadRow
{
    public int LeadId { get; set; }
    public string LeadNumber { get; set; } = "";
    public string ServiceName { get; set; } = "";
    public string? MovingFrom { get; set; }
    public string? MovingTo { get; set; }
    public string? StorageLocation { get; set; }
    public DateOnly? MovingDate { get; set; }
    public AssignmentStatus Status { get; set; }
    public DateTime AssignedAt { get; set; }
}

public class PartnerLeadListViewModel
{
    public IReadOnlyList<PartnerLeadRow> Leads { get; set; } = Array.Empty<PartnerLeadRow>();

    public AssignmentStatus? Status { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalCount { get; set; }

    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}