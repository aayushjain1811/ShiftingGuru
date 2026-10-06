using System.Globalization;
using Microsoft.AspNetCore.Identity;
using ShiftingGuru.Data;
using ShiftingGuru.Services.Email;

namespace ShiftingGuru.Services.Auth;

/// <summary>The result of step 2. ErrorCode is invalidCode, lockedOut or weakPassword.</summary>
public record PasswordResetResult(bool Succeeded, string? ErrorCode, string? Error)
{
    public static PasswordResetResult Ok() => new(true, null, null);
    public static PasswordResetResult Fail(string code, string error) => new(false, code, error);
}

public interface IPartnerPasswordResetService
{
    /// <summary>
    /// Step 1: emails a 6-digit code if the address belongs to a partner.
    /// Says nothing either way - callers must show the same message whatever happens.
    /// </summary>
    Task SendCodeAsync(string email, CancellationToken ct = default);

    /// <summary>Step 2: checks the code and sets the new password.</summary>
    Task<PasswordResetResult> ResetAsync(string email, string code, string newPassword, CancellationToken ct = default);
}

/// <summary>
/// NEW: partner "forgot password", used by BOTH the website pages and the
/// mobile API - one set of rules, written once.
///
/// Built entirely on ASP.NET Identity - no new table, no migration:
/// - The code comes from Identity's email token provider. It's tied to the
///   account's security stamp, so it stops working the moment the password
///   changes (single use), and expires on its own after a few minutes.
/// - Wrong codes count as failed sign-in attempts: 5 wrong guesses lock the
///   account for 10 minutes, the same rule as the login form.
/// - Changing the password ends every phone's session (refresh tokens check
///   the security stamp) and the website's cookie session soon after.
/// </summary>
public class PartnerPasswordResetService : IPartnerPasswordResetService
{
    private const string CodePurpose = "PartnerPasswordReset";

    // Where "when did we last send a code" is kept - Identity's own token table.
    private const string StoreProvider = "ShiftingGuruPasswordReset";
    private const string LastSentName = "code-sent-at";

    // One code per minute per account, so this can't be used to flood an inbox.
    private static readonly TimeSpan ResendGap = TimeSpan.FromSeconds(60);

    private readonly UserManager<IdentityUser> _users;
    private readonly IEmailService _email;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<PartnerPasswordResetService> _logger;

    public PartnerPasswordResetService(
        UserManager<IdentityUser> users,
        IEmailService email,
        IWebHostEnvironment environment,
        ILogger<PartnerPasswordResetService> logger)
    {
        _users = users;
        _email = email;
        _environment = environment;
        _logger = logger;
    }

    public async Task SendCodeAsync(string email, CancellationToken ct = default)
    {
        var user = await FindPartnerAsync(email);
        if (user is null || string.IsNullOrEmpty(user.Email)) return;

        if (await _users.IsLockedOutAsync(user)) return;
        if (await SentRecentlyAsync(user)) return;

        var code = await _users.GenerateUserTokenAsync(user, TokenOptions.DefaultEmailProvider, CodePurpose);

        await _users.SetAuthenticationTokenAsync(user, StoreProvider, LastSentName,
            DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));

        try
        {
            await _email.SendAsync(user.Email, "Your ShiftingGuru password reset code",
                CodeHtml(code), CodeText(code), ct);
        }
        catch (Exception ex)
        {
            // Never the code or the email address in the log.
            _logger.LogError(ex, "Couldn't send a partner password reset code.");
        }

        // Development only, clearly labelled: lets you test without a real inbox.
        if (_environment.IsDevelopment())
        {
            _logger.LogInformation("DEVELOPMENT ONLY - partner password reset code: {Code}", code);
        }
    }

    public async Task<PasswordResetResult> ResetAsync(
        string email, string code, string newPassword, CancellationToken ct = default)
    {
        const string invalidMessage = "That code isn't right or has expired. Check it, or request a new one.";

        var user = await FindPartnerAsync(email);
        if (user is null) return PasswordResetResult.Fail("invalidCode", invalidMessage);

        if (await _users.IsLockedOutAsync(user))
        {
            return PasswordResetResult.Fail("lockedOut", "Too many attempts. Try again in a few minutes.");
        }

        var valid = await _users.VerifyUserTokenAsync(
            user, TokenOptions.DefaultEmailProvider, CodePurpose, code.Trim());

        if (!valid)
        {
            // Counts towards the same 5-attempt lockout as the login form.
            await _users.AccessFailedAsync(user);
            return PasswordResetResult.Fail("invalidCode", invalidMessage);
        }

        // The code proved who they are. Identity's own reset token does the change.
        var resetToken = await _users.GeneratePasswordResetTokenAsync(user);
        var result = await _users.ResetPasswordAsync(user, resetToken, newPassword);

        if (!result.Succeeded)
        {
            // Password rules (8+ characters with a number). The code still
            // works, so they can simply try a stronger password.
            return PasswordResetResult.Fail("weakPassword",
                string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        await _users.ResetAccessFailedCountAsync(user);
        await _users.RemoveAuthenticationTokenAsync(user, StoreProvider, LastSentName);

        _logger.LogInformation("A partner reset their password.");
        return PasswordResetResult.Ok();
    }

    // -----------------------------------------------------------------

    private async Task<IdentityUser?> FindPartnerAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;

        var user = await _users.FindByEmailAsync(email.Trim());
        return user is not null && await _users.IsInRoleAsync(user, AdminSeeder.VendorRole) ? user : null;
    }

    private async Task<bool> SentRecentlyAsync(IdentityUser user)
    {
        var saved = await _users.GetAuthenticationTokenAsync(user, StoreProvider, LastSentName);

        return long.TryParse(saved, NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
            && DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc) < ResendGap;
    }

    private static string CodeHtml(string code) => $"""
        <div style="font-family:Arial,Helvetica,sans-serif;max-width:520px;margin:0 auto;padding:32px 24px;color:#182238">
          <p style="margin:0 0 12px;font-size:15px">Hi,</p>
          <p style="margin:0 0 20px;font-size:15px">Use this code to set a new password for your ShiftingGuru partner account:</p>
          <p style="margin:0 0 20px;font-size:32px;font-weight:bold;letter-spacing:8px;color:#3156C6">{code}</p>
          <p style="margin:0 0 12px;font-size:14px">The code works for the next 5 minutes, on the website or in the Partner app.</p>
          <p style="margin:0;font-size:13px;color:#667085">If you didn't ask to reset your password, you can ignore this email. Your password won't change.</p>
        </div>
        """;

    private static string CodeText(string code) =>
        $"Use this code to set a new password for your ShiftingGuru partner account: {code}\n\n" +
        "The code works for the next 5 minutes, on the website or in the Partner app.\n\n" +
        "If you didn't ask to reset your password, you can ignore this email.";
}