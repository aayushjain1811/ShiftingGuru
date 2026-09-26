using ShiftingGuru.Models;
using ShiftingGuru.ViewModels.Partner;

namespace ShiftingGuru.Services;

public record PartnerRegistrationResult(
    bool Succeeded,
    Vendor? Vendor,
    IReadOnlyList<string> Errors,
    bool IsResume = false)
{
    public static PartnerRegistrationResult Ok(Vendor vendor) =>
        new(true, vendor, Array.Empty<string>());

    /// <summary>
    /// NEW: this email + mobile already has an unpaid application. Nothing new
    /// is created; the partner is sent to pay for the one they already started.
    /// </summary>
    public static PartnerRegistrationResult Resume(Vendor vendor) =>
        new(true, vendor, Array.Empty<string>(), IsResume: true);

    public static PartnerRegistrationResult Fail(params string[] errors) =>
        new(false, null, errors);
}

public interface IPartnerService
{
    /// <summary>
    /// Creates the Identity user, assigns the Vendor role, stores any documents
    /// and writes the Vendor rows with status AwaitingPayment. The application
    /// only becomes Pending (visible for review) once the fee is paid.
    /// </summary>
    Task<PartnerRegistrationResult> RegisterAsync(
        PartnerRegistrationViewModel model, CancellationToken ct = default);

    /// <summary>Loads the vendor owned by an Identity user, or null.</summary>
    Task<Vendor?> GetByIdentityUserIdAsync(string identityUserId, bool tracked = false,
        CancellationToken ct = default);

    /// <summary>Status changes an admin may make from the current one.</summary>
    IReadOnlyList<VendorStatus> AllowedTransitionsFrom(VendorStatus current);
}