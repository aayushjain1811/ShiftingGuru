namespace ShiftingGuru.Models;

/// <summary>
/// A passwordless access grant for one customer's lead.
///
/// The raw token exists only in the link handed to the customer. What's stored
/// here is a SHA-256 hash, so a database leak doesn't hand anyone working
/// access links.
/// </summary>
public class CustomerAccessToken
{
    public int Id { get; set; }

    public int LeadId { get; set; }
    public Lead? Lead { get; set; }

    /// <summary>Base64 SHA-256 of the raw token. Never the token itself.</summary>
    public string TokenHash { get; set; } = "";

    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    public bool IsUsable(DateTime utcNow) => RevokedAt is null && ExpiresAt > utcNow;
}