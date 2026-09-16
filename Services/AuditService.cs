using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using ShiftingGuru.Data;
using ShiftingGuru.Models;

namespace ShiftingGuru.Services;

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpContextAccessor _http;
    private readonly UserManager<IdentityUser> _users;
    private readonly ILogger<AuditService> _logger;

    public AuditService(
        ApplicationDbContext db,
        IHttpContextAccessor http,
        UserManager<IdentityUser> users,
        ILogger<AuditService> logger)
    {
        _db = db;
        _http = http;
        _users = users;
        _logger = logger;
    }

    public async Task RecordAsync(
        AuditAction action, string entityType, int entityId, string description,
        CancellationToken ct = default)
    {
        try
        {
            var principal = _http.HttpContext?.User;

            // Identity comes from the authenticated context, never a parameter,
            // so an action can't be attributed to someone else.
            var userId = principal is null ? null : _users.GetUserId(principal);
            if (string.IsNullOrEmpty(userId)) return;

            _db.AdminAuditLogs.Add(new AdminAuditLog
            {
                AdminUserId = userId,
                AdminEmail = principal?.FindFirstValue(ClaimTypes.Name) ?? "",
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Description = description,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Logged and swallowed on purpose: losing an audit line is bad,
            // rolling back an approval because of it is worse.
            _logger.LogError(ex, "Couldn't write an audit entry for {EntityType} {EntityId}",
                entityType, entityId);
        }
    }
}