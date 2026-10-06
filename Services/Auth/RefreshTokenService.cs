using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace ShiftingGuru.Services.Auth;

/// <summary>A refresh token as handed to the app, and when it stops working (UTC).</summary>
public record IssuedRefreshToken(string Token, DateTime ExpiresAt);

/// <summary>A refresh token that checked out: whose it is, and its stored name.</summary>
public record RefreshTokenMatch(IdentityUser User, string Name);

public interface IRefreshTokenService
{
    /// <summary>Creates and stores a new refresh token for the user.</summary>
    Task<IssuedRefreshToken> IssueAsync(IdentityUser user);

    /// <summary>Checks a refresh token. Null if it's unknown, expired, tampered with, or the password changed.</summary>
    Task<RefreshTokenMatch?> FindAsync(string? rawToken);

    /// <summary>Spends a valid refresh token and hands out a new one. Each token works exactly once.</summary>
    Task<IssuedRefreshToken> RotateAsync(RefreshTokenMatch match);

    /// <summary>Deletes a refresh token (sign-out). Does nothing if it's already gone.</summary>
    Task RevokeAsync(string? rawToken);
}

/// <summary>
/// NEW (mobile API): long-lived "stay signed in" tokens for the apps.
///
/// Stored in Identity's existing AspNetUserTokens table - no new table and no
/// migration. One row per signed-in phone, so a partner can use two phones.
///
/// What the app holds:  {userId}.{tokenId}.{secret}
/// What the database holds, per token: SHA-256 of the secret | expiry | security stamp
///
/// - Only a hash is stored, so a database leak doesn't hand anyone a working token.
/// - The security stamp changes when the password changes, which ends every
///   phone's session at once - exactly what someone resetting a stolen
///   password needs.
/// </summary>
public class RefreshTokenService : IRefreshTokenService
{
    private const string Provider = "ShiftingGuruMobile";
    private const string NamePrefix = "refresh:";
    private const int SecretBytes = 32;   // 256 bits

    private readonly UserManager<IdentityUser> _users;
    private readonly JwtOptions _options;
    private readonly ILogger<RefreshTokenService> _logger;

    public RefreshTokenService(
        UserManager<IdentityUser> users,
        IOptions<JwtOptions> options,
        ILogger<RefreshTokenService> logger)
    {
        _users = users;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IssuedRefreshToken> IssueAsync(IdentityUser user)
    {
        var tokenId = Guid.NewGuid().ToString("N");                      // 32 lowercase hex characters
        var secret = Base64Url(RandomNumberGenerator.GetBytes(SecretBytes));
        var expiresAt = DateTime.UtcNow.AddDays(_options.RefreshTokenDays);
        var stamp = await _users.GetSecurityStampAsync(user) ?? "";

        var stored = string.Join('|',
            Hash(secret),
            expiresAt.Ticks.ToString(CultureInfo.InvariantCulture),
            stamp);

        var result = await _users.SetAuthenticationTokenAsync(user, Provider, NamePrefix + tokenId, stored);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Couldn't save the refresh token: " +
                string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        return new IssuedRefreshToken($"{user.Id}.{tokenId}.{secret}", expiresAt);
    }

    public async Task<RefreshTokenMatch?> FindAsync(string? rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return null;

        var parts = rawToken.Trim().Split('.');
        if (parts.Length != 3) return null;

        var (userId, tokenId, secret) = (parts[0], parts[1], parts[2]);

        // Only names we made ourselves - nothing odd reaches the database lookup.
        if (tokenId.Length != 32 || !tokenId.All(char.IsAsciiHexDigitLower)) return null;

        var user = await _users.FindByIdAsync(userId);
        if (user is null) return null;

        var name = NamePrefix + tokenId;
        var stored = await _users.GetAuthenticationTokenAsync(user, Provider, name);
        if (stored is null) return null;   // already used, signed out, or never existed

        var pieces = stored.Split('|');
        if (pieces.Length != 3 ||
            !long.TryParse(pieces[1], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks))
        {
            await _users.RemoveAuthenticationTokenAsync(user, Provider, name);
            return null;
        }

        // Fixed-time comparison, so the response time can't hint at how close a guess was.
        var matches = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(pieces[0]),
            Encoding.UTF8.GetBytes(Hash(secret)));

        if (!matches) return null;

        var expired = new DateTime(ticks, DateTimeKind.Utc) <= DateTime.UtcNow;
        var passwordChanged = pieces[2] != (await _users.GetSecurityStampAsync(user) ?? "");

        if (expired || passwordChanged)
        {
            await _users.RemoveAuthenticationTokenAsync(user, Provider, name);
            return null;
        }

        return new RefreshTokenMatch(user, name);
    }

    public async Task<IssuedRefreshToken> RotateAsync(RefreshTokenMatch match)
    {
        // The old token is deleted first, so it can never be used twice.
        await _users.RemoveAuthenticationTokenAsync(match.User, Provider, match.Name);
        return await IssueAsync(match.User);
    }

    public async Task RevokeAsync(string? rawToken)
    {
        var match = await FindAsync(rawToken);
        if (match is null) return;

        await _users.RemoveAuthenticationTokenAsync(match.User, Provider, match.Name);
        _logger.LogInformation("A mobile session was signed out.");
    }

    private static string Hash(string secret) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}