namespace ShiftingGuru.Models;

/// <summary>
/// Links one customer Lead to one Vendor. A lead can be assigned to several
/// vendors, and a vendor can hold many leads, so this is the join between them
/// with its own lifecycle.
/// </summary>
public class LeadAssignment
{
    public int Id { get; set; }

    public int LeadId { get; set; }
    public Lead? Lead { get; set; }

    public int VendorId { get; set; }
    public Vendor? Vendor { get; set; }

    public AssignmentStatus Status { get; set; } = AssignmentStatus.Assigned;

    public DateTime AssignedAt { get; set; }

    /// <summary>Set the first time the vendor opens the lead details page.</summary>
    public DateTime? ViewedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    /// <summary>Cancelled assignments stay in the table as history.</summary>
    public bool IsActive => Status is not (AssignmentStatus.Cancelled or AssignmentStatus.Expired);
}

public enum AssignmentStatus
{
    Assigned,
    Viewed,
    Declined,
    Expired,
    Completed,

    /// <summary>Withdrawn by an admin. Kept rather than deleted.</summary>
    Cancelled
}