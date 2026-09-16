namespace ShiftingGuru.Models;

/// <summary>
/// A moving/logistics business applying to receive leads.
/// Holds no password - authentication lives entirely in ASP.NET Core Identity,
/// linked through IdentityUserId.
/// </summary>
public class Vendor
{
    public int Id { get; set; }

    /// <summary>Public reference, e.g. SG-V-20260911-00001. Unique.</summary>
    public string VendorNumber { get; set; } = "";

    /// <summary>The Identity user that signs in for this business.</summary>
    public string IdentityUserId { get; set; } = "";

    public string BusinessName { get; set; } = "";
    public string ContactPerson { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string City { get; set; } = "";
    public string Address { get; set; } = "";
    public string? GstNumber { get; set; }
    public int YearsOfExperience { get; set; }
    public string? OperatingLocations { get; set; }
    public string? AdditionalInformation { get; set; }

    /// <summary>Server-controlled. Never bound from a form.</summary>
    public VendorStatus Status { get; set; } = VendorStatus.Pending;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<VendorService> Services { get; set; } = new List<VendorService>();

    /// <summary>Leads passed to this vendor.</summary>
    public ICollection<LeadAssignment> Assignments { get; set; } = new List<LeadAssignment>();

    /// <summary>Quotes this vendor has submitted.</summary>
    public ICollection<Quote> Quotes { get; set; } = new List<Quote>();

    /// <summary>Customer reviews. Only Approved ones count publicly.</summary>
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
}

public enum VendorStatus
{
    Pending,
    Approved,
    Rejected,
    Suspended
}