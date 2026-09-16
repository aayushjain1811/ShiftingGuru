using ShiftingGuru.Models;
using ShiftingGuru.ViewModels.Admin;

namespace ShiftingGuru.Services;

public record AssignmentOutcome(int Created, int Reactivated, int Skipped, string? Error)
{
    public bool Succeeded => Error is null;
    public int Total => Created + Reactivated;
}

public interface ILeadAssignmentService
{
    /// <summary>Approved vendors ranked by how well they fit this lead.</summary>
    Task<AdminLeadAssignmentViewModel> BuildAssignmentPanelAsync(Lead lead, CancellationToken ct = default);

    /// <summary>
    /// Assigns a lead to the given vendors. Silently skips vendors that are
    /// not approved or are already assigned; reactivates cancelled ones.
    /// </summary>
    Task<AssignmentOutcome> AssignAsync(int leadId, IEnumerable<int> vendorIds, CancellationToken ct = default);

    /// <summary>Withdraws an assignment by marking it Cancelled.</summary>
    Task<bool> CancelAsync(int leadId, int assignmentId, CancellationToken ct = default);
}