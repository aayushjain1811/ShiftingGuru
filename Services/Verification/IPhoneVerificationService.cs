namespace ShiftingGuru.Services.Verification;

public interface IPhoneVerificationService
{
    /// <summary>
    /// True only if the token was really issued by Firebase for this project,
    /// hasn't expired, and belongs to +91 followed by these 10 digits.
    /// </summary>
    Task<bool> IsVerifiedAsync(string? idToken, string mobileDigits, CancellationToken ct = default);
}