namespace ShiftingGuru.Models;

/// <summary>
/// A moving/logistics business applying to receive leads.
/// Holds no password - authentication lives entirely in ASP.NET Core Identity,
/// linked through IdentityUserId.
/// </summary>
public class Vendor
{
    /// <summary>NEW: length of the free trial, which starts when an admin first approves the partner.</summary>
    public const int FreeTrialDays = 7;

    public int Id { get; set; }

    /// <summary>Public reference, e.g. SG-V-20260911-00001. Unique.</summary>
    public string VendorNumber { get; set; } = "";

    /// <summary>The Identity user that signs in for this business.</summary>
    public string IdentityUserId { get; set; } = "";

    public string BusinessName { get; set; } = "";

    /// <summary>Shown as "Full name" on the sign-up form.</summary>
    public string ContactPerson { get; set; } = "";

    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string City { get; set; } = "";
    public string Address { get; set; } = "";

    /// <summary>Required for new applications. Older partners may not have one.</summary>
    public string? GstNumber { get; set; }

    public int YearsOfExperience { get; set; }
    public string? OperatingLocations { get; set; }
    public string? AdditionalInformation { get; set; }

    /// <summary>Server-controlled. Never bound from a form.</summary>
    public VendorStatus Status { get; set; } = VendorStatus.Pending;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>NEW: when the email code was confirmed. Null for partners from before this feature.</summary>
    public DateTime? EmailVerifiedAt { get; set; }

    /// <summary>NEW: when the mobile code was confirmed. Null for partners from before this feature.</summary>
    public DateTime? PhoneVerifiedAt { get; set; }

    /// <summary>NEW: when the registration fee was paid. Null = not paid (or joined before the fee existed).</summary>
    public DateTime? RegistrationFeePaidAt { get; set; }

    /// <summary>NEW: the free trial, set when an admin first approves a partner who has paid.</summary>
    public DateTime? TrialStartedAt { get; set; }
    public DateTime? TrialEndsAt { get; set; }

    public ICollection<VendorService> Services { get; set; } = new List<VendorService>();

    /// <summary>NEW: GST certificate, PAN, Aadhaar front/back and office photo.</summary>
    public ICollection<VendorDocument> Documents { get; set; } = new List<VendorDocument>();

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
    Suspended,

    /// <summary>
    /// NEW: form submitted but the registration fee isn't paid yet. Hidden from
    /// the normal admin list, can't be approved, can't use the partner area.
    /// Paying moves it to Pending automatically.
    /// </summary>
    AwaitingPayment
}