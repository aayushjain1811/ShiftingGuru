using ShiftingGuru.Models;

namespace ShiftingGuru.Services;

/// <summary>The raw token exists only here, in memory, once.</summary>
public record AccessLink(string RawToken, string Url, DateTime ExpiresAt);

public interface ICustomerAccessService
{
    /// <summary>Issues a new access link for a lead. The raw token is returned
    /// exactly once and never persisted.</summary>
    Task<AccessLink> IssueAsync(int leadId, CancellationToken ct = default);

    /// <summary>Validates a raw token and returns the lead id it grants, or null.</summary>
    Task<int?> ResolveLeadIdAsync(string rawToken, CancellationToken ct = default);

    /// <summary>Issues a link for whoever matches the contact details, if anyone.
    /// Returns null without saying why - callers must not leak existence.</summary>
    Task<AccessLink?> IssueForContactAsync(string? email, string? phone, CancellationToken ct = default);
}