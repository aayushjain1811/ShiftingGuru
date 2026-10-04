namespace ShiftingGuru.Services.Auth;

/// <summary>
/// NEW (mobile API): settings for the tokens the mobile apps sign in with.
/// Issuer, Audience and AccessTokenMinutes live in appsettings.json.
/// SigningKey is a secret: user secrets locally, Secret Manager on Cloud Run.
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Who made the token, e.g. https://www.shiftingguru.com</summary>
    public string Issuer { get; set; } = "";

    /// <summary>Who the token is for, e.g. shiftingguru-mobile</summary>
    public string Audience { get; set; } = "";

    /// <summary>The secret used to sign tokens. At least 32 characters.</summary>
    public string SigningKey { get; set; } = "";

    /// <summary>How long a token works before the app must sign in again.</summary>
    public int AccessTokenMinutes { get; set; } = 60;
}

/// <summary>
/// The claim names written inside mobile tokens. Short, standard names, used
/// in one place so the token maker and the controllers can never disagree.
/// </summary>
public static class ApiClaims
{
    public const string UserId = "sub";
    public const string Role = "role";
    public const string Email = "email";
    public const string TokenId = "jti";
}