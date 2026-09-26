namespace ShiftingGuru.Services.Payments;

/// <summary>
/// The "Razorpay" settings.
///   KeyId          - public, also sent to the browser. Fine in appsettings.json.
///   KeySecret      - SECRET. User secrets locally, Secret Manager on Cloud Run.
///   WebhookSecret  - SECRET. The value you type when creating the webhook in Razorpay.
/// </summary>
public class RazorpayOptions
{
    public const string SectionName = "Razorpay";

    public string? KeyId { get; set; }
    public string? KeySecret { get; set; }
    public string? WebhookSecret { get; set; }

    /// <summary>The partner registration fee, in rupees.</summary>
    public decimal RegistrationFeeRupees { get; set; } = 229m;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(KeyId) && !string.IsNullOrWhiteSpace(KeySecret);
}