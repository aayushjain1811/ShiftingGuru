using System.ComponentModel.DataAnnotations;

namespace ShiftingGuru.Models;

/// <summary>
/// A customer's submitted requirement, persisted to PostgreSQL.
/// This is NOT bound from the form - QuoteRequestViewModel is, and the server
/// maps across explicitly. That keeps Id, Status, LeadNumber and the
/// timestamps out of reach of anything the browser sends.
/// </summary>
public class Lead
{
    /// <summary>Internal primary key. Never shown to customers.</summary>
    public int Id { get; set; }

    /// <summary>Public reference, e.g. SG-20260902-00042. Unique.</summary>
    public string LeadNumber { get; set; } = "";

    // ---------- Service ----------
    public string ServiceSlug { get; set; } = "";
    public string ServiceName { get; set; } = "";

    // ---------- Location ----------
    // Nullable because storage requests have no origin/destination.
    public string? MovingFrom { get; set; }
    public string? MovingTo { get; set; }
    public string? StorageLocation { get; set; }

    /// <summary>A calendar date, not an instant - so DateOnly, not DateTime.
    /// Avoids any timezone ambiguity about which day the customer meant.</summary>
    public DateOnly? MovingDate { get; set; }

    // ---------- Service-specific requirements ----------
    // Only the fields belonging to the chosen service are populated.
    public string? PropertyType { get; set; }
    public string? MoveSize { get; set; }
    public string? OfficeSize { get; set; }
    public string? DeskCount { get; set; }
    public string? VehicleType { get; set; }
    public string? VehicleModel { get; set; }
    public string? VehicleCondition { get; set; }
    public string? GoodsType { get; set; }
    public string? LoadDetails { get; set; }
    public string? VehicleRequirement { get; set; }
    public string? StorageType { get; set; }
    public string? StorageSize { get; set; }
    public string? StorageDuration { get; set; }

    // ---------- Contact ----------
    public string CustomerName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string? Email { get; set; }
    public string? PreferredContactMethod { get; set; }
    public string? AdditionalRequirements { get; set; }

    // ---------- Server-controlled ----------
    public LeadStatus Status { get; set; } = LeadStatus.New;

    /// <summary>Always UTC. Set by the server, never accepted from the form.</summary>
    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    // ---------- Conversion ----------
    // Set only by QuoteSelectionService, inside a transaction.

    public int? SelectedVendorId { get; set; }
    public Vendor? SelectedVendor { get; set; }

    public int? SelectedQuoteId { get; set; }
    public Quote? SelectedQuote { get; set; }

    public DateTime? ConvertedAt { get; set; }

    public bool IsConverted => Status == LeadStatus.Converted;

    /// <summary>Vendors this lead has been passed to.</summary>
    public ICollection<LeadAssignment> Assignments { get; set; } = new List<LeadAssignment>();

    /// <summary>Vendor price offers against this lead.</summary>
    public ICollection<Quote> Quotes { get; set; } = new List<Quote>();

    /// <summary>Passwordless access grants for this customer.</summary>
    public ICollection<CustomerAccessToken> AccessTokens { get; set; } = new List<CustomerAccessToken>();

    /// <summary>At most one - enforced by a unique index on LeadId.</summary>
    public Review? Review { get; set; }

    public bool IsCompleted => Status == LeadStatus.Completed;
}

/// <summary>
/// Lead lifecycle. Stored as text in PostgreSQL rather than an integer, so
/// the table stays readable and adding a value later can't silently shift
/// the meaning of existing rows.
/// </summary>
public enum LeadStatus
{
    New,
    Contacted,
    Assigned,
    InProgress,
    Quoted,
    Converted,

    /// <summary>The move itself has happened. Set by an admin, and the point
    /// at which the customer may review the vendor they chose.</summary>
    Completed,

    Closed,
    Cancelled
}