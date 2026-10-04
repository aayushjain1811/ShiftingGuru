using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ShiftingGuru.Services.Auth;

/// <summary>
/// NEW (mobile API): makes the tokens the mobile apps send with every request.
///
/// A token can be READ by anyone who has it - it is signed so it can't be
/// changed, not encrypted. That's why only the user id, email and role go in.
/// </summary>
public class JwtTokenService : ITokenService
{
    private readonly JwtOptions _options;
    private readonly SigningCredentials _credentials;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        _credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public IssuedToken CreateAccessToken(IdentityUser user, string role)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new Dictionary<string, object>
        {
            [ApiClaims.UserId] = user.Id,
            [ApiClaims.Role] = role,

            // A unique id per token, useful later for refresh tokens and logout.
            [ApiClaims.TokenId] = Guid.NewGuid().ToString("N")
        };

        if (!string.IsNullOrEmpty(user.Email))
        {
            claims[ApiClaims.Email] = user.Email;
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Claims = claims,
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = _credentials
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);

        return new IssuedToken(token, expires);
    }
}