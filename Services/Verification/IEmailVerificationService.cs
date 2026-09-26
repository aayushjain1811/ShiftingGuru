namespace ShiftingGuru.Services.Verification;

public record VerificationResult(bool Succeeded, string? Error, string? Token = null)
{
    public static VerificationResult Ok(string? token = null) => new(true, null, token);
    public static VerificationResult Fail(string error) => new(false, error);
}

public interface IEmailVerificationService
{
    /// <summary>Emails a fresh 6-digit code. Older codes for that email stop working.</summary>
    Task<VerificationResult> SendCodeAsync(string email, CancellationToken ct = default);

    /// <summary>Checks a code. On success, Token is the proof the form sends back on submit.</summary>
    Task<VerificationResult> VerifyCodeAsync(string email, string code, CancellationToken ct = default);

    /// <summary>
    /// Used when the application is submitted: true only if the token is genuine,
    /// belongs to this email, is recent, and hasn't been used before. Marks it used.
    /// </summary>
    Task<bool> ConsumeTokenAsync(string email, string token, CancellationToken ct = default);

    /// <summary>
    /// NEW: the same checks as ConsumeTokenAsync, but without using the token up.
    /// Used before taking the registration fee: only verified emails can pay.
    /// </summary>
    Task<bool> IsTokenValidAsync(string email, string token, CancellationToken ct = default);
}