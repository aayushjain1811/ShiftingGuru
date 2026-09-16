namespace ShiftingGuru.Models;

/// <summary>
/// A record of one significant admin action. Deliberately not a page-view log -
/// only state changes that someone might later need to account for.
///
/// Never stores passwords, tokens, or customer contact details.
/// </summary>
public class AdminAuditLog
{
    public int Id { get; set; }

    /// <summary>Identity user id of the admin who acted.</summary>
    public string AdminUserId { get; set; } = "";

    /// <summary>Email, captured at the time so the log stays readable.</summary>
    public string AdminEmail { get; set; } = "";

    public AuditAction Action { get; set; }

    /// <summary>"Lead", "Vendor", "Location"...</summary>
    public string EntityType { get; set; } = "";
    public int EntityId { get; set; }

    /// <summary>Human-readable summary, e.g. "Published location Gurgaon".</summary>
    public string Description { get; set; } = "";

    public DateTime CreatedAt { get; set; }
}

public enum AuditAction
{
    Created,
    Updated,
    Published,
    Unpublished,
    Approved,
    Rejected,
    Suspended,
    Assigned,
    Unassigned,
    StatusChanged,
    Deleted
}