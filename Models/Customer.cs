namespace ShiftingGuru.Models;

/// <summary>
/// NEW (mobile app): a customer who registered in the ShiftingGuru app.
///
/// Website customers still don't need an account - they use the quote form
/// and the magic link as before. This is only for the app.
///
/// Holds no password - like Vendor, sign-in lives entirely in ASP.NET Core
/// Identity, linked through IdentityUserId. The Identity user's UserName is
/// the 10-digit mobile number, which is what customers sign in with.
/// </summary>
public class Customer
{
    public int Id { get; set; }

    /// <summary>The Identity user that signs in for this customer.</summary>
    public string IdentityUserId { get; set; } = "";

    public string FullName { get; set; } = "";

    /// <summary>Plain 10 digits, verified by an SMS code. Unique.</summary>
    public string Phone { get; set; } = "";

    public string Email { get; set; } = "";
    public string City { get; set; } = "";

    /// <summary>When the mobile number was confirmed with the SMS code.</summary>
    public DateTime PhoneVerifiedAt { get; set; }

    /// <summary>When the customer accepted the Terms and Privacy Policy.</summary>
    public DateTime TermsAcceptedAt { get; set; }

    /// <summary>Lets the team switch an account off later without deleting it.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// This customer's requests: made in the app, plus earlier website
    /// requests with the same verified mobile number.
    /// </summary>
    public ICollection<Lead> Leads { get; set; } = new List<Lead>();
}