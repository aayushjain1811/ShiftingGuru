namespace ShiftingGuru.Models;

/// <summary>
/// NEW (mobile apps): one phone that can receive push notifications for a
/// signed-in partner or customer. A person with two phones has two rows.
///
/// The token is the Expo push token the app gets from the phone, e.g.
/// "ExponentPushToken[xxxxxxxx]". It's removed when the person logs out, or
/// when Expo reports the app was uninstalled.
/// </summary>
public class DeviceToken
{
    public int Id { get; set; }

    /// <summary>The Identity user signed in on this phone (partner or customer).</summary>
    public string IdentityUserId { get; set; } = "";

    /// <summary>Unique - one phone belongs to whoever signed in on it last.</summary>
    public string Token { get; set; } = "";

    /// <summary>"android" or "ios".</summary>
    public string Platform { get; set; } = "";

    public DateTime CreatedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
}