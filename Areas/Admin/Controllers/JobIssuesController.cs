using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services;
using ShiftingGuru.Services.Jobs;

namespace ShiftingGuru.Areas.Admin.Controllers;

/// <summary>One booking row on the Job issues page.</summary>
public class JobIssueRow
{
    public int LeadId { get; init; }
    public string LeadNumber { get; init; } = "";
    public string ServiceName { get; init; } = "";
    public string Route { get; init; } = "";
    public DateOnly? MovingDate { get; init; }
    public string CustomerName { get; init; } = "";
    public string CustomerPhone { get; init; } = "";
    public string VendorName { get; init; } = "";
    public string VendorPhone { get; init; } = "";
    public DateTime? PartnerMarkedAt { get; init; }
    public string? PartnerNote { get; init; }
    public DateTime? AnsweredAt { get; init; }
    public string? ProblemText { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public string? ResolvedBy { get; init; }
    public string? ResolutionNote { get; init; }
}

public class JobIssuesViewModel
{
    public IReadOnlyList<JobIssueRow> Problems { get; init; } = Array.Empty<JobIssueRow>();
    public IReadOnlyList<JobIssueRow> Overdue { get; init; } = Array.Empty<JobIssueRow>();
    public IReadOnlyList<JobIssueRow> Waiting { get; init; } = Array.Empty<JobIssueRow>();
    public IReadOnlyList<JobIssueRow> RecentlyResolved { get; init; } = Array.Empty<JobIssueRow>();
}

/// <summary>
/// NEW: /admin/job-issues - everything about finished jobs that needs a person:
///   1. Problems customers reported (with what they wrote) - resolve them here
///   2. Bookings past their move date that the partner never marked complete
///   3. Jobs waiting for the customer to confirm (no action needed)
///   4. Problems resolved in the last 30 days
/// </summary>
[Area("Admin")]
[Route("admin/job-issues")]
[Authorize(Roles = AdminSeeder.AdminRole)]
public class JobIssuesController : Controller
{
    private const int ListLimit = 100;

    private readonly ApplicationDbContext _db;
    private readonly IJobCompletionService _jobs;
    private readonly IAuditService _audit;

    public JobIssuesController(ApplicationDbContext db, IJobCompletionService jobs, IAuditService audit)
    {
        _db = db;
        _jobs = jobs;
        _audit = audit;
    }

    // GET /admin/job-issues
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var withJob = _db.JobCompletions.AsNoTracking().Where(j => j.Lead!.SelectedVendorId != null);

        var problems = await withJob
            .Where(j => j.CustomerAnswer == JobCustomerAnswer.Problem && j.ResolvedAt == null)
            .OrderBy(j => j.CustomerAnsweredAt)
            .Take(ListLimit)
            .Select(RowFromJob())
            .ToListAsync(ct);

        var waiting = await withJob
            .Where(j => j.CustomerAnswer == JobCustomerAnswer.Waiting && j.Lead!.Status == LeadStatus.Converted)
            .OrderBy(j => j.PartnerMarkedAt)
            .Take(ListLimit)
            .Select(RowFromJob())
            .ToListAsync(ct);

        var since = DateTime.UtcNow.AddDays(-30);
        var resolved = await withJob
            .Where(j => j.CustomerAnswer == JobCustomerAnswer.Problem && j.ResolvedAt != null && j.ResolvedAt >= since)
            .OrderByDescending(j => j.ResolvedAt)
            .Take(ListLimit)
            .Select(RowFromJob())
            .ToListAsync(ct);

        var overdue = await _jobs.OverdueQuery()
            .OrderBy(l => l.MovingDate)
            .Take(ListLimit)
            .Select(l => new JobIssueRow
            {
                LeadId = l.Id,
                LeadNumber = l.LeadNumber,
                ServiceName = l.ServiceName,
                Route = l.MovingFrom == null ? (l.StorageLocation ?? "-") : l.MovingFrom + " to " + l.MovingTo,
                MovingDate = l.MovingDate,
                CustomerName = l.CustomerName,
                CustomerPhone = l.Phone,
                VendorName = l.SelectedVendor!.BusinessName,
                VendorPhone = l.SelectedVendor.Phone
            })
            .ToListAsync(ct);

        return View(new JobIssuesViewModel
        {
            Problems = problems,
            Overdue = overdue,
            Waiting = waiting,
            RecentlyResolved = resolved
        });
    }

    // POST /admin/job-issues/42/resolve   (completeJob=true|false, note)
    [HttpPost("{leadId:int}/resolve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve(int leadId, bool completeJob, string? note, CancellationToken ct)
    {
        var admin = User.Identity?.Name ?? "admin";
        var result = await _jobs.ResolveAsync(leadId, completeJob, note, admin, ct);

        if (result.Succeeded)
        {
            var number = await _db.Leads.Where(l => l.Id == leadId).Select(l => l.LeadNumber).FirstOrDefaultAsync(ct);
            await _audit.RecordAsync(AuditAction.StatusChanged, nameof(Lead), leadId,
                completeJob
                    ? $"Problem report on {number} resolved; job marked complete"
                    : $"Problem report on {number} resolved; job kept open", ct);

            TempData["AdminMessage"] = completeJob
                ? "Resolved. The job is complete and the customer has been asked for a review."
                : "Resolved. The job stays open - the partner can mark it complete again.";
        }
        else
        {
            TempData["AdminError"] = result.Error;
        }

        return RedirectToAction(nameof(Index));
    }

    // POST /admin/job-issues/run-checks - the hourly check, on demand.
    [HttpPost("run-checks")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunChecks(CancellationToken ct)
    {
        var done = await _jobs.AutoConfirmDueAsync(ct);
        TempData["AdminMessage"] = done == 0
            ? "Checked. No jobs were due for automatic completion."
            : $"Checked. {done} job(s) completed automatically (customer didn't answer within 3 days).";

        return RedirectToAction(nameof(Index));
    }

    private static System.Linq.Expressions.Expression<Func<JobCompletion, JobIssueRow>> RowFromJob() =>
        j => new JobIssueRow
        {
            LeadId = j.LeadId,
            LeadNumber = j.Lead!.LeadNumber,
            ServiceName = j.Lead.ServiceName,
            Route = j.Lead.MovingFrom == null ? (j.Lead.StorageLocation ?? "-") : j.Lead.MovingFrom + " to " + j.Lead.MovingTo,
            MovingDate = j.Lead.MovingDate,
            CustomerName = j.Lead.CustomerName,
            CustomerPhone = j.Lead.Phone,
            VendorName = j.Lead.SelectedVendor!.BusinessName,
            VendorPhone = j.Lead.SelectedVendor.Phone,
            PartnerMarkedAt = j.PartnerMarkedAt,
            PartnerNote = j.PartnerNote,
            AnsweredAt = j.CustomerAnsweredAt,
            ProblemText = j.ProblemText,
            ResolvedAt = j.ResolvedAt,
            ResolvedBy = j.ResolvedBy,
            ResolutionNote = j.ResolutionNote
        };
}