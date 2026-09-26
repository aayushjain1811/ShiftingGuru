using ShiftingGuru.Models;
using ShiftingGuru.ViewModels.Partner;

namespace ShiftingGuru.Services;

public record PartnerRegistrationResult(
    bool Succeeded,
    Vendor? Vendor,
    IReadOnlyList<string> Errors,
    bool ResetEmailVerification = false,
    bool ResetPhoneVerification = false,
    bool ResetPayment = false)
{
    public static PartnerRegistrationResult Ok(Vendor vendor) =>
        new(true, vendor, Array.Empty<string>());

    public static PartnerRegistrationResult Fail(params string[] errors) =>
        new(false, null, errors);

    /// <summary>NEW: a verification proof was rejected, so the form must ask for it again.</summary>
    public static PartnerRegistrationResult VerificationFailed(bool email, bool phone, string error) =>
        new(false, null, new[] { error }, email, phone);

    /// <summary>NEW: the registration fee couldn't be matched, so the form asks for it again.</summary>
    public static PartnerRegistrationResult PaymentFailed(string error) =>
        new(false, null, new[] { error }, ResetPayment: true);
}

public interface IPartnerService
{
    /// <summary>
    /// Checks the email and mobile proofs and the documents, then creates the
    /// Identity user, assigns the Vendor role, stores the documents and writes
    /// the Vendor + VendorServices + VendorDocuments rows. Status is always Pending.
    /// </summary>
    Task<PartnerRegistrationResult> RegisterAsync(
        PartnerRegistrationViewModel model, CancellationToken ct = default);

    /// <summary>Loads the vendor owned by an Identity user, or null.</summary>
    Task<Vendor?> GetByIdentityUserIdAsync(string identityUserId, bool tracked = false,
        CancellationToken ct = default);

    /// <summary>Status changes an admin may make from the current one.</summary>
    IReadOnlyList<VendorStatus> AllowedTransitionsFrom(VendorStatus current);
}