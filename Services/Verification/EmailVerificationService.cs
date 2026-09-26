using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services.Email;

namespace ShiftingGuru.Services.Verification;

/// <summary>
/// Email one-time codes for the partner sign-up form, sent through Resend.
///
/// Limits, all checked against the database so they hold across Cloud Run instances:
///   - a code works for 10 minutes and allows 5 wrong guesses
///   - one email can ask for a new code every 30 seconds, and 5 times an hour
///   - the whole site sends at most 200 codes an hour, so a bot can't burn
///     the Resend quota by cycling through made-up addresses
/// </summary>
public class EmailVerificationService : IEmailVerificationService
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ResendGap = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(2);
    private const int MaxAttempts = 5;
    private const int MaxPerEmailPerHour = 5;
    private const int MaxSiteWidePerHour = 200;

    private static readonly EmailAddressAttribute EmailCheck = new();

    private readonly ApplicationDbContext _db;
    private readonly IEmailService _email;
    private readonly IHostEnvironment _env;
    private readonly ILogger<EmailVerificationService> _logger;

    public EmailVerificationService(
        ApplicationDbContext db,
        IEmailService email,
        IHostEnvironment env,
        ILogger<EmailVerificationService> logger)
    {
        _db = db;
        _email = email;
        _env = env;
        _logger = logger;
    }

    public async Task<VerificationResult> SendCodeAsync(string email, CancellationToken ct = default)
    {
        var address = Normalize(email);
        if (address is null) return VerificationResult.Fail("Enter a valid email address.");

        // Caught here rather than after the whole form is filled in. Identity
        // stores emails upper-cased in NormalizedEmail.
        var upper = address.ToUpperInvariant();
        if (await _db.Users.AnyAsync(u => u.NormalizedEmail == upper, ct))
        {
            return VerificationResult.Fail("This email already has a partner account. Sign in instead.");
        }

        var now = DateTime.UtcNow;
        var hourAgo = now.AddHours(-1);

        var recent = await _db.EmailVerifications
            .Where(e => e.Email == address && e.CreatedAt > hourAgo)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => e.CreatedAt)
            .ToListAsync(ct);

        if (recent.Count > 0 && now - recent[0] < ResendGap)
        {
            return VerificationResult.Fail("Wait a few seconds before asking for another code.");
        }

        if (recent.Count >= MaxPerEmailPerHour)
        {
            return VerificationResult.Fail("Too many codes for this email. Try again in an hour.");
        }

        var siteWide = await _db.EmailVerifications.CountAsync(e => e.CreatedAt > hourAgo, ct);
        if (siteWide >= MaxSiteWidePerHour)
        {
            _logger.LogWarning("Site-wide email code limit reached ({Count} in the last hour).", siteWide);
            return VerificationResult.Fail("We can't send codes right now. Try again later.");
        }

        // RandomNumberGenerator, not Random: Random is predictable.
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

        _db.EmailVerifications.Add(new EmailVerification
        {
            Email = address,
            CodeHash = Hash($"{address}:{code}"),
            CreatedAt = now,
            ExpiresAt = now + CodeLifetime
        });
        await _db.SaveChangesAsync(ct);

        // Locally Email:Enabled is false, so nothing is actually sent.
        // This line lets you still test the flow. Never runs in production.
        if (_env.IsDevelopment())
        {
            _logger.LogInformation("Development only: code for {Email} is {Code}.", address, code);
        }

        var sent = await _email.SendAsync(
            address,
            $"{code} is your ShiftingGuru verification code",
            BuildHtml(code),
            BuildText(code),
            ct);

        return sent.Succeeded
            ? VerificationResult.Ok()
            : VerificationResult.Fail("We couldn't send the email. Check the address and try again.");
    }

    public async Task<VerificationResult> VerifyCodeAsync(string email, string code, CancellationToken ct = default)
    {
        var address = Normalize(email);
        if (address is null) return VerificationResult.Fail("Enter a valid email address.");

        code = code?.Trim() ?? "";
        if (code.Length != 6 || !code.All(char.IsAsciiDigit))
        {
            return VerificationResult.Fail("Enter the 6-digit code from your email.");
        }

        var now = DateTime.UtcNow;

        // Only the newest code counts. Asking for a new one retires the old.
        var row = await _db.EmailVerifications
            .Where(e => e.Email == address)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (row is null) return VerificationResult.Fail("Send a code first.");
        if (row.VerifiedAt is not null) return VerificationResult.Fail("This code was already used. Send a new one.");
        if (row.ExpiresAt <= now) return VerificationResult.Fail("This code has expired. Send a new one.");
        if (row.Attempts >= MaxAttempts) return VerificationResult.Fail("Too many wrong tries. Send a new code.");

        if (!SameHash(row.CodeHash, Hash($"{address}:{code}")))
        {
            row.Attempts++;
            await _db.SaveChangesAsync(ct);

            var left = MaxAttempts - row.Attempts;
            return VerificationResult.Fail(left > 0
                ? $"That code isn't right. {left} {(left == 1 ? "try" : "tries")} left."
                : "Too many wrong tries. Send a new code.");
        }

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

        row.VerifiedAt = now;
        row.TokenHash = Hash(token);
        await _db.SaveChangesAsync(ct);

        return VerificationResult.Ok(token);
    }

    public async Task<bool> ConsumeTokenAsync(string email, string token, CancellationToken ct = default)
    {
        var row = await FindUsableTokenAsync(email, token, ct);
        if (row is null) return false;

        row.UsedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> IsTokenValidAsync(string email, string token, CancellationToken ct = default) =>
        await FindUsableTokenAsync(email, token, ct) is not null;

    private async Task<EmailVerification?> FindUsableTokenAsync(string email, string token, CancellationToken ct)
    {
        var address = Normalize(email);
        if (address is null || string.IsNullOrWhiteSpace(token)) return null;

        var hash = Hash(token.Trim());
        var row = await _db.EmailVerifications.FirstOrDefaultAsync(e => e.TokenHash == hash, ct);
        var now = DateTime.UtcNow;

        if (row is null ||
            row.Email != address ||              // verified a different email, then changed it
            row.UsedAt is not null ||            // already spent on an application
            row.VerifiedAt is null ||
            row.VerifiedAt < now - TokenLifetime)
        {
            return null;
        }

        return row;
    }

    // -----------------------------------------------------------------

    private static string? Normalize(string? email)
    {
        var value = email?.Trim().ToLowerInvariant();
        return !string.IsNullOrEmpty(value) && value.Length <= 120 && EmailCheck.IsValid(value)
            ? value
            : null;
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    // Compares in constant time, so response timing reveals nothing about the code.
    private static bool SameHash(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(a), Encoding.ASCII.GetBytes(b));

    private static string BuildText(string code) =>
        $"Your ShiftingGuru verification code is {code}.\n\n" +
        "It works for 10 minutes. Don't share it with anyone.\n\n" +
        "If you didn't try to register as a ShiftingGuru partner, you can ignore this email.";

    private static string BuildHtml(string code)
    {
        var safe = WebUtility.HtmlEncode(code);
        return $"""
            <div style="font-family:Arial,Helvetica,sans-serif;max-width:480px;margin:0 auto;padding:32px 24px;color:#182238">
              <p style="margin:0 0 8px;font-size:15px">Your ShiftingGuru verification code is</p>
              <p style="margin:0 0 20px;font-size:34px;font-weight:700;letter-spacing:8px;color:#3156C6">{safe}</p>
              <p style="margin:0 0 16px;font-size:14px;color:#667085">It works for 10 minutes. Don't share it with anyone.</p>
              <p style="margin:0;font-size:13px;color:#667085">If you didn't try to register as a ShiftingGuru partner, you can ignore this email.</p>
            </div>
            """;
    }
}