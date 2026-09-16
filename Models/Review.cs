namespace ShiftingGuru.Models;

/// <summary>
/// A customer's review of the vendor they selected. Created only for a
/// completed lead, and never published without admin approval.
/// </summary>
public class Review
{
    public int Id { get; set; }

    public int LeadId { get; set; }
    public Lead? Lead { get; set; }

    /// <summary>Always the lead's selected vendor. Never taken from a form.</summary>
    public int VendorId { get; set; }
    public Vendor? Vendor { get; set; }

    /// <summary>Whole stars, 1 to 5. Validated server-side.</summary>
    public int Rating { get; set; }

    public string? Title { get; set; }

    public string Comment { get; set; } = "";

    /// <summary>
    /// Display name captured at submission, e.g. "Aayush J." - public reviews
    /// shouldn't re-read the lead's full name, and the byline shouldn't change
    /// if the lead is later edited.
    /// </summary>
    public string CustomerNameSnapshot { get; set; } = "";

    public ReviewStatus Status { get; set; } = ReviewStatus.Pending;

    /// <summary>Admin-only. Never rendered to customers or vendors.</summary>
    public string? ModerationNote { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }

    /// <summary>The customer may still change it; no one has seen it publicly.</summary>
    public bool IsEditableByCustomer => Status == ReviewStatus.Pending;

    /// <summary>Only these count towards a vendor's public rating.</summary>
    public static readonly ReviewStatus[] PublicStatuses = { ReviewStatus.Approved };

    /// <summary>Turns "Aayush Jain" into "Aayush J." for public display.</summary>
    public static string ToDisplayName(string fullName)
    {
        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0) return "Customer";
        if (parts.Length == 1) return parts[0];

        return $"{parts[0]} {char.ToUpperInvariant(parts[^1][0])}.";
    }
}

public enum ReviewStatus
{
    Pending,
    Approved,
    Rejected,

    /// <summary>Was published, since withdrawn. Kept for history.</summary>
    Hidden
}