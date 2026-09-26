namespace ShiftingGuru.ViewModels.Partner;

/// <summary>NEW: the "Complete your registration" page shown after the form is submitted.</summary>
public class PartnerPaymentViewModel
{
    /// <summary>The protected link token. Identifies the application without a login.</summary>
    public string Token { get; set; } = "";

    public string VendorNumber { get; set; } = "";
    public string BusinessName { get; set; } = "";
    public string ContactName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";

    public decimal FeeRupees { get; set; }
    public int TrialDays { get; set; }

    /// <summary>True when the link is missing, tampered with, or older than 7 days.</summary>
    public bool LinkExpired { get; set; }
}