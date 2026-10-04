using Microsoft.AspNetCore.Identity;

namespace ShiftingGuru.Services.Auth;

/// <summary>A freshly made token and the moment it stops working (UTC).</summary>
public record IssuedToken(string AccessToken, DateTime ExpiresAt);

public interface ITokenService
{
    /// <summary>
    /// Makes a signed access token for an Identity user in the given role.
    /// Only the user id, email and role go inside - never anything private.
    /// </summary>
    IssuedToken CreateAccessToken(IdentityUser user, string role);
}