using ShiftingGuru.Models;
using ShiftingGuru.ViewModels.Partner;

namespace ShiftingGuru.Services;

public record PartnerRegistrationResult(bool Succeeded, Vendor? Vendor, IReadOnlyList<string> Errors)
{
    public static PartnerRegistrationResult Ok(Vendor vendor) =>
        new(true, vendor, Array.Empty<string>());

    public static PartnerRegistrationResult Fail(params string[] errors) =>
        new(false, null, errors);
}

public interface IPartnerService
{
    /// <summary>
    /// Creates the Identity user, assigns the Vendor role and writes the
    /// Vendor + VendorServices rows. Status is always Pending.
    /// </summary>
    Task<PartnerRegistrationResult> RegisterAsync(
        PartnerRegistrationViewModel model, CancellationToken ct = default);

    /// <summary>Loads the vendor owned by an Identity user, or null.</summary>
    Task<Vendor?> GetByIdentityUserIdAsync(string identityUserId, bool tracked = false,
        CancellationToken ct = default);

    /// <summary>Status changes an admin may make from the current one.</summary>
    IReadOnlyList<VendorStatus> AllowedTransitionsFrom(VendorStatus current);
}