namespace ShiftingGuru.Models;

/// <summary>
/// One 6-digit code emailed from the partner sign-up form.
///
/// Neither the code nor the proof token is stored in plain text - only their
/// SHA-256 hashes - so a database leak doesn't hand anyone a working code.
/// Rows live in the database rather than in memory because Cloud Run can run
/// more than one copy of the app, and each copy has its own memory.
/// </summary>
public class EmailVerification
{
    public long Id { get; set; }

    /// <summary>Trimmed and lower-case.</summary>
    public string Email { get; set; } = "";

    public string CodeHash { get; set; } = "";

    /// <summary>Wrong guesses against this code. Capped by the service.</summary>
    public int Attempts { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }

    /// <summary>Set when the right code is entered.</summary>
    public DateTime? VerifiedAt { get; set; }

    /// <summary>Hash of the token the browser gets back after a correct code.</summary>
    public string? TokenHash { get; set; }

    /// <summary>Set when the token is spent on a submitted application. Single use.</summary>
    public DateTime? UsedAt { get; set; }
}