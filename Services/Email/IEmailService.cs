namespace ShiftingGuru.Services.Email;

public record EmailResult(bool Succeeded, string? Error)
{
    public static EmailResult Ok() => new(true, null);
    public static EmailResult Fail(string error) => new(false, error);
}

public interface IEmailService
{
    /// <summary>
    /// Attempts delivery. Never throws - failures come back as a result so a
    /// business transaction is never rolled back because SMTP was down.
    /// </summary>
    Task<EmailResult> SendAsync(
        string recipient, string subject, string htmlBody, string? textBody = null,
        CancellationToken ct = default);
}