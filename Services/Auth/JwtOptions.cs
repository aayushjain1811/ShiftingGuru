namespace ShiftingGuru.Services.Auth;

/// <summary>
/// NEW (mobile API): settings for the tokens the mobile apps sign in with.
/// Issuer, Audience and the lifetimes live in appsettings.json.
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

    /// <summary>How long an access token works. Short, because the app refreshes it quietly.</summary>
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>
    /// NEW: how long a refresh token works. Each refresh hands out a new one,
    /// so a partner who opens the app at least once in this window stays signed in.
    /// </summary>
    public int RefreshTokenDays { get; set; } = 30;
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