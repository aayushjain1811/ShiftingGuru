using ShiftingGuru.Models;

namespace ShiftingGuru.Services;

public interface IAuditService
{
    /// <summary>
    /// Records an admin action. Never throws - an audit failure must not undo
    /// the thing it was recording.
    /// </summary>
    Task RecordAsync(
        AuditAction action, string entityType, int entityId, string description,
        CancellationToken ct = default);
}