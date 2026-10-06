namespace ShiftingGuru.Models;

/// <summary>
/// NEW (mobile apps): which kinds of push notification a partner or customer
/// wants. One row per person, created the first time they change a switch;
/// with no row, everything is on.
///
/// Account notices (approved, paused) are always sent and can't be turned off.
/// Emails are not affected by these switches.
/// </summary>
public class NotificationPreference
{
    /// <summary>The Identity user these switches belong to. Also the key.</summary>
    public string IdentityUserId { get; set; } = "";

    /// <summary>Partners: a new lead was assigned to them.</summary>
    public bool Leads { get; set; } = true;

    /// <summary>Customers: a new quote arrived. Partners: their quote was chosen or not.</summary>
    public bool Quotes { get; set; } = true;

    /// <summary>Request received, booking confirmed, job complete.</summary>
    public bool Bookings { get; set; } = true;

    /// <summary>Customers: rate your move. Partners: a new review went live.</summary>
    public bool Reviews { get; set; } = true;

    public DateTime UpdatedAt { get; set; }
}

/// <summary>The kinds of push notification, matching the switches above.</summary>
public static class PushCategory
{
    public const string Leads = "leads";
    public const string Quotes = "quotes";
    public const string Bookings = "bookings";
    public const string Reviews = "reviews";
}