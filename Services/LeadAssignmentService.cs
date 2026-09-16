using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services.Notifications;
using ShiftingGuru.ViewModels.Admin;

namespace ShiftingGuru.Services;

public class LeadAssignmentService : ILeadAssignmentService
{
    /// <summary>Cap on candidate vendors pulled back for ranking.</summary>
    private const int CandidateLimit = 100;

    private readonly ApplicationDbContext _db;
    private readonly INotificationService _notifications;
    private readonly ILogger<LeadAssignmentService> _logger;

    public LeadAssignmentService(
        ApplicationDbContext db,
        INotificationService notifications,
        ILogger<LeadAssignmentService> logger)
    {
        _db = db;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<AdminLeadAssignmentViewModel> BuildAssignmentPanelAsync(
        Lead lead, CancellationToken ct = default)
    {
        var existing = await _db.LeadAssignments
            .AsNoTracking()
            .Include(a => a.Vendor)
            .Where(a => a.LeadId == lead.Id)
            .OrderByDescending(a => a.AssignedAt)
            .ToListAsync(ct);

        var activeVendorIds = existing
            .Where(a => a.Status != AssignmentStatus.Cancelled)
            .Select(a => a.VendorId)
            .ToHashSet();

        var approvedCount = await _db.Vendors
            .AsNoTracking()
            .CountAsync(v => v.Status == VendorStatus.Approved, ct);

        // Service match is a straightforward EXISTS, so PostgreSQL decides it.
        // Only the small projected shape comes back, not whole vendor rows.
        var candidates = await _db.Vendors
            .AsNoTracking()
            .Where(v => v.Status == VendorStatus.Approved)
            .Select(v => new
            {
                v.Id,
                v.VendorNumber,
                v.BusinessName,
                v.City,
                v.OperatingLocations,
                ServiceMatch = v.Services.Any(s => s.ServiceSlug == lead.ServiceSlug)
            })
            .OrderByDescending(v => v.ServiceMatch)
            .ThenBy(v => v.BusinessName)
            .Take(CandidateLimit)
            .ToListAsync(ct);

        // Location matching is substring work across two free-text fields, so
        // it happens here rather than in SQL where it couldn't use an index.
        var places = new[] { lead.MovingFrom, lead.MovingTo, lead.StorageLocation }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!.Trim())
            .ToList();

        var items = candidates
            .Select(v => new AdminVendorAssignmentItemViewModel
            {
                VendorId = v.Id,
                VendorNumber = v.VendorNumber,
                BusinessName = v.BusinessName,
                City = v.City,
                OperatingLocations = v.OperatingLocations,
                ServiceMatch = v.ServiceMatch ? MatchLevel.Match : MatchLevel.None,
                LocationMatch = ScoreLocation(v.City, v.OperatingLocations, places),
                AlreadyAssigned = activeVendorIds.Contains(v.Id)
            })
            .OrderByDescending(v => v.ServiceMatch)
            .ThenByDescending(v => v.LocationMatch)
            .ThenBy(v => v.BusinessName)
            .ToList();

        return new AdminLeadAssignmentViewModel
        {
            Existing = existing,
            Candidates = items,
            ApprovedVendorCount = approvedCount,

            // Never filter the admin down to nothing: if no vendor matches on
            // service, every approved vendor is still listed, clearly marked.
            ShowingAllApproved = items.Count > 0 && items.All(i => i.ServiceMatch == MatchLevel.None)
        };
    }

    private static MatchLevel ScoreLocation(string city, string? operating, IReadOnlyList<string> places)
    {
        if (places.Count == 0) return MatchLevel.None;

        foreach (var place in places)
        {
            if (string.Equals(city, place, StringComparison.OrdinalIgnoreCase))
            {
                return MatchLevel.Match;
            }
        }

        if (!string.IsNullOrWhiteSpace(operating))
        {
            foreach (var place in places)
            {
                if (operating.Contains(place, StringComparison.OrdinalIgnoreCase))
                {
                    return MatchLevel.Partial;
                }
            }
        }

        return MatchLevel.None;
    }

    public async Task<AssignmentOutcome> AssignAsync(
        int leadId, IEnumerable<int> vendorIds, CancellationToken ct = default)
    {
        var requested = vendorIds.Distinct().ToList();
        if (requested.Count == 0) return new AssignmentOutcome(0, 0, 0, "Select at least one vendor.");

        if (!await _db.Leads.AnyAsync(l => l.Id == leadId, ct))
        {
            return new AssignmentOutcome(0, 0, 0, "That lead no longer exists.");
        }

        // Only ids that are real AND approved survive. A tampered checkbox
        // value naming a suspended vendor is simply dropped.
        var approved = await _db.Vendors
            .AsNoTracking()
            .Where(v => requested.Contains(v.Id) && v.Status == VendorStatus.Approved)
            .Select(v => v.Id)
            .ToListAsync(ct);

        if (approved.Count == 0)
        {
            return new AssignmentOutcome(0, 0, requested.Count,
                "None of those vendors can be assigned. They may no longer be approved.");
        }

        var existing = await _db.LeadAssignments
            .Where(a => a.LeadId == leadId && approved.Contains(a.VendorId))
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var created = 0;
        var reactivated = 0;
        var skipped = requested.Count - approved.Count;

        foreach (var vendorId in approved)
        {
            var row = existing.FirstOrDefault(a => a.VendorId == vendorId);

            if (row is null)
            {
                _db.LeadAssignments.Add(new LeadAssignment
                {
                    LeadId = leadId,
                    VendorId = vendorId,
                    Status = AssignmentStatus.Assigned,
                    AssignedAt = now
                });
                created++;
            }
            else if (row.Status == AssignmentStatus.Cancelled)
            {
                // The unique index means we can't insert a second row, so a
                // withdrawn assignment is brought back to life instead.
                row.Status = AssignmentStatus.Assigned;
                row.AssignedAt = now;
                row.ViewedAt = null;
                row.UpdatedAt = now;
                reactivated++;
            }
            else
            {
                skipped++;   // already actively assigned
            }
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to assign lead {LeadId}", leadId);
            return new AssignmentOutcome(0, 0, 0, "Couldn't save those assignments. Please try again.");
        }

        // After the save, so nobody is emailed about an assignment that rolled back.
        await NotifyAssignedVendorsAsync(leadId, approved, ct);

        return new AssignmentOutcome(created, reactivated, skipped, null);
    }

    private async Task NotifyAssignedVendorsAsync(
        int leadId, IReadOnlyList<int> vendorIds, CancellationToken ct)
    {
        var lead = await _db.Leads.AsNoTracking().FirstOrDefaultAsync(l => l.Id == leadId, ct);
        if (lead is null) return;

        var vendors = await _db.Vendors
            .AsNoTracking()
            .Where(v => vendorIds.Contains(v.Id))
            .ToListAsync(ct);

        foreach (var vendor in vendors)
        {
            await _notifications.LeadAssignedAsync(lead, vendor, ct);
        }
    }

    public async Task<bool> CancelAsync(int leadId, int assignmentId, CancellationToken ct = default)
    {
        // leadId is part of the lookup so an assignment id from another lead
        // can't be cancelled through this lead's page.
        var assignment = await _db.LeadAssignments
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.LeadId == leadId, ct);

        if (assignment is null) return false;

        assignment.Status = AssignmentStatus.Cancelled;
        assignment.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel assignment {AssignmentId}", assignmentId);
            return false;
        }
    }
}