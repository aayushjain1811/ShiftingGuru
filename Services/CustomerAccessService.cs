using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Models;

namespace ShiftingGuru.Services;

public class CustomerAccessService : ICustomerAccessService
{
    private const int TokenBytes = 32;          // 256 bits of entropy
    private const int DefaultLifetimeDays = 30;

    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<CustomerAccessService> _logger;

    public CustomerAccessService(
        ApplicationDbContext db, IConfiguration config, ILogger<CustomerAccessService> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
    }

    private int LifetimeDays =>
        _config.GetValue("CustomerPortal:AccessTokenLifetimeDays", DefaultLifetimeDays);

    public async Task<AccessLink> IssueAsync(int leadId, CancellationToken ct = default)
    {
        // RandomNumberGenerator, not Random: this value is a credential.
        var bytes = RandomNumberGenerator.GetBytes(TokenBytes);
        var rawToken = Base64UrlEncode(bytes);

        var expiresAt = DateTime.UtcNow.AddDays(LifetimeDays);

        _db.CustomerAccessTokens.Add(new CustomerAccessToken
        {
            LeadId = leadId,
            TokenHash = Hash(rawToken),      // only the hash is stored
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt
        });

        await _db.SaveChangesAsync(ct);

        // Lead id is logged; the token never is.
        _logger.LogInformation("Issued a customer access link for lead {LeadId}", leadId);

        return new AccessLink(rawToken, $"/my-request/access/{rawToken}", expiresAt);
    }

    public async Task<int?> ResolveLeadIdAsync(string rawToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return null;

        var hash = Hash(rawToken);

        var token = await _db.CustomerAccessTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (token is null || !token.IsUsable(DateTime.UtcNow)) return null;

        token.LastUsedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return token.LeadId;
    }

    public async Task<AccessLink?> IssueForContactAsync(
        string? email, string? phone, CancellationToken ct = default)
    {
        var normalisedEmail = email?.Trim();
        var normalisedPhone = phone?.Trim();

        if (string.IsNullOrEmpty(normalisedEmail) && string.IsNullOrEmpty(normalisedPhone))
        {
            return null;
        }

        // Most recent matching lead. The caller shows the same message either
        // way, so no existence information escapes.
        var leadId = await _db.Leads
            .AsNoTracking()
            .Where(l =>
                (normalisedEmail != null && l.Email != null && l.Email.ToLower() == normalisedEmail.ToLower()) ||
                (normalisedPhone != null && l.Phone == normalisedPhone))
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => (int?)l.Id)
            .FirstOrDefaultAsync(ct);

        if (leadId is null) return null;

        return await IssueAsync(leadId.Value, ct);
    }

    private static string Hash(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToBase64String(bytes);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}