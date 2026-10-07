using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services.Email;
using ShiftingGuru.Services.Notifications;
using ShiftingGuru.Services.Push;

namespace ShiftingGuru.Services.Jobs;

/// <summary>Settings for the scheduled checks. The key is a secret (Secret Manager on Cloud Run).</summary>
public class JobsOptions
{
    public const string SectionName = "Jobs";

    /// <summary>Cloud Scheduler sends this in the X-Job-Key header. Empty = the endpoint is switched off.</summary>
    public string Key { get; set; } = "";
}

public record JobResult(bool Succeeded, string? Error = null)
{
    public static JobResult Ok() => new(true);
    public static JobResult Fail(string error) => new(false, error);
}

/// <summary>
/// Where a booking's completion stands - for the partner's and customer's screens.
///
/// State:
///   notMarked         the partner hasn't marked it yet
///   awaitingCustomer  marked; waiting for the customer (auto-confirms at AutoConfirmAt)
///   problem           the customer reported a problem; the team is dealing with it
///   resolved          the team dealt with a problem and kept the job open
///   completed         done
///   closed            the booking was cancelled or closed
/// </summary>
public record JobCompletionView(
    string State,
    DateTime? PartnerMarkedAt,
    string? PartnerNote,
    DateTime? AutoConfirmAt,
    bool CanMark,
    string? CannotMarkReason,
    bool CanAnswer,
    string? ProblemText);

public interface IJobCompletionService
{
    Task<JobCompletionView?> ForPartnerAsync(int vendorId, int leadId, CancellationToken ct = default);
    Task<JobResult> MarkCompletedAsync(int vendorId, int leadId, string? note, CancellationToken ct = default);

    Task<JobCompletionView?> ForCustomerAsync(int customerId, int leadId, CancellationToken ct = default);
    Task<JobResult> ConfirmAsync(int customerId, int leadId, CancellationToken ct = default);
    Task<JobResult> ReportProblemAsync(int customerId, int leadId, string text, CancellationToken ct = default);

    /// <summary>Completes jobs the customer didn't answer within 3 days. Returns how many.</summary>
    Task<int> AutoConfirmDueAsync(CancellationToken ct = default);

    /// <summary>The morning email to the admins: problems, overdue jobs. Once a day, after 9 am India time.</summary>
    Task<bool> SendDailyDigestAsync(CancellationToken ct = default);

    /// <summary>An admin resolves a reported problem - completing the job, or keeping it open.</summary>
    Task<JobResult> ResolveAsync(int leadId, bool completeJob, string? note, string adminEmail, CancellationToken ct = default);

    /// <summary>Converted bookings past their move date that the partner never marked complete.</summary>
    IQueryable<Lead> OverdueQuery();
}

/// <summary>
/// NEW: the whole "is this job done?" flow, in one place.
///
/// Completing a job here does exactly what the admin's Complete button does
/// (status Completed, and NotificationService.LeadCompletedAsync, which emails
/// and pushes the review invitation) - so the result is the same whichever
/// way a job gets completed.
/// </summary>
public class JobCompletionService : IJobCompletionService
{
    public static readonly TimeSpan AutoConfirmAfter = TimeSpan.FromDays(3);
    public const int OverdueAfterDays = 2;           // move date passed 2+ days ago, not marked
    public const int FlexibleOverdueAfterDays = 14;  // no move date: chosen 14+ days ago, not marked
    private const int DigestHourIndia = 9;
    private const int DigestListLimit = 25;

    private static readonly TimeSpan IndiaOffset = TimeSpan.FromHours(5.5);
    private static readonly CultureInfo India = new("en-IN");

    private readonly ApplicationDbContext _db;
    private readonly INotificationService _notifications;
    private readonly IPushSender _push;
    private readonly NotificationQueue _queue;
    private readonly EmailTemplate _template;
    private readonly EmailOptions _email;
    private readonly AppOptions _app;
    private readonly ILogger<JobCompletionService> _logger;

    public JobCompletionService(
        ApplicationDbContext db,
        INotificationService notifications,
        IPushSender push,
        NotificationQueue queue,
        EmailTemplate template,
        IOptions<EmailOptions> email,
        IOptions<AppOptions> app,
        ILogger<JobCompletionService> logger)
    {
        _db = db;
        _notifications = notifications;
        _push = push;
        _queue = queue;
        _template = template;
        _email = email.Value;
        _app = app.Value;
        _logger = logger;
    }

    private static DateOnly TodayIndia => DateOnly.FromDateTime(DateTime.UtcNow + IndiaOffset);

    // =================================================================
    // Partner
    // =================================================================

    public async Task<JobCompletionView?> ForPartnerAsync(int vendorId, int leadId, CancellationToken ct = default)
    {
        var lead = await _db.Leads.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == leadId && l.SelectedVendorId == vendorId, ct);
        if (lead is null) return null;

        var job = await _db.JobCompletions.AsNoTracking().FirstOrDefaultAsync(j => j.LeadId == leadId, ct);
        return ViewOf(lead, job, forPartner: true);
    }

    public async Task<JobResult> MarkCompletedAsync(int vendorId, int leadId, string? note, CancellationToken ct = default)
    {
        var lead = await _db.Leads
            .Include(l => l.SelectedVendor)
            .FirstOrDefaultAsync(l => l.Id == leadId && l.SelectedVendorId == vendorId, ct);

        if (lead is null) return JobResult.Fail("That booking couldn't be found.");

        var job = await _db.JobCompletions.FirstOrDefaultAsync(j => j.LeadId == leadId, ct);
        var view = ViewOf(lead, job, forPartner: true);
        if (!view.CanMark) return JobResult.Fail(view.CannotMarkReason ?? "This job can't be marked complete right now.");

        var now = DateTime.UtcNow;
        var cleanNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim()[..Math.Min(note.Trim().Length, 500)];

        if (job is null)
        {
            _db.JobCompletions.Add(new JobCompletion
            {
                LeadId = lead.Id,
                VendorId = vendorId,
                PartnerMarkedAt = now,
                PartnerNote = cleanNote,
                CustomerAnswer = JobCustomerAnswer.Waiting
            });
        }
        else
        {
            // Marked again after the team resolved a problem and kept the job open.
            job.PartnerMarkedAt = now;
            job.PartnerNote = cleanNote;
            job.CustomerAnswer = JobCustomerAnswer.Waiting;
            job.CustomerAnsweredAt = null;
        }

        await _db.SaveChangesAsync(ct);

        var partnerName = lead.SelectedVendor?.BusinessName ?? "Your partner";

        // App customers: a notification that opens the booking with Yes / There's a problem.
        await PushToCustomerAsync(lead, new PushMessage(
            "Did your move happen?",
            $"{partnerName} marked your move complete. Tap to confirm, or tell us if something's wrong.",
            $"/booking/{lead.Id}",
            PushCategory.Bookings), ct);

        // Everyone with an email (including website customers without the app).
        if (!string.IsNullOrWhiteSpace(lead.Email))
        {
            await QueueAsync(NotificationType.CustomerJobMarkedComplete, lead.Email!,
                $"Was your move with {partnerName} completed?", nameof(Lead), lead.Id,
                new EmailContent
                {
                    Heading = "Did your move happen?",
                    Intro = $"{partnerName} has marked your {lead.ServiceName.ToLowerInvariant()} as complete.",
                    Rows = new[]
                    {
                        new EmailRow("Reference", lead.LeadNumber),
                        new EmailRow("Route", Route(lead)),
                        new EmailRow("Partner", partnerName)
                    },
                    Closing = "If everything went well, there's nothing you need to do. If something isn't right, "
                            + "open the ShiftingGuru app and tap \"There's a problem\", or reply to this email "
                            + $"within {AutoConfirmAfter.Days} days - after that, the job is marked complete."
                }, ct);
        }

        return JobResult.Ok();
    }

    // =================================================================
    // Customer
    // =================================================================

    public async Task<JobCompletionView?> ForCustomerAsync(int customerId, int leadId, CancellationToken ct = default)
    {
        var lead = await _db.Leads.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == leadId && l.CustomerId == customerId && l.SelectedVendorId != null, ct);
        if (lead is null) return null;

        var job = await _db.JobCompletions.AsNoTracking().FirstOrDefaultAsync(j => j.LeadId == leadId, ct);
        return ViewOf(lead, job, forPartner: false);
    }

    public async Task<JobResult> ConfirmAsync(int customerId, int leadId, CancellationToken ct = default)
    {
        var (lead, job) = await LoadForCustomerAsync(customerId, leadId, ct);
        if (lead is null) return JobResult.Fail("That booking couldn't be found.");

        if (job is null || job.CustomerAnswer != JobCustomerAnswer.Waiting || lead.Status != LeadStatus.Converted)
        {
            return JobResult.Fail("There's nothing to confirm on this booking right now.");
        }

        job.CustomerAnswer = JobCustomerAnswer.Confirmed;
        job.CustomerAnsweredAt = DateTime.UtcNow;

        await CompleteLeadAsync(lead, ct);
        return JobResult.Ok();
    }

    public async Task<JobResult> ReportProblemAsync(int customerId, int leadId, string text, CancellationToken ct = default)
    {
        var problem = (text ?? "").Trim();
        if (problem.Length < 10) return JobResult.Fail("Please tell us a little more - at least 10 characters.");
        if (problem.Length > 1000) problem = problem[..1000];

        var (lead, job) = await LoadForCustomerAsync(customerId, leadId, ct);
        if (lead is null) return JobResult.Fail("That booking couldn't be found.");

        if (job is null || job.CustomerAnswer != JobCustomerAnswer.Waiting || lead.Status != LeadStatus.Converted)
        {
            return JobResult.Fail("This booking can't take a problem report right now. Please contact support.");
        }

        job.CustomerAnswer = JobCustomerAnswer.Problem;
        job.CustomerAnsweredAt = DateTime.UtcNow;
        job.ProblemText = problem;
        job.ResolvedAt = null;
        job.ResolvedBy = null;
        job.ResolutionNote = null;

        await _db.SaveChangesAsync(ct);

        var vendor = lead.SelectedVendor;

        // 1. Straight to the admins, with what the customer wrote.
        await QueueAdminAsync(NotificationType.AdminJobProblem,
            $"Problem reported on {lead.LeadNumber}", nameof(Lead), lead.Id,
            new EmailContent
            {
                Heading = "A customer reported a problem",
                Intro = $"{lead.CustomerName} says there's a problem with their {lead.ServiceName.ToLowerInvariant()}, "
                      + "after the partner marked it complete. The job stays open until you resolve it.",
                Rows = new[]
                {
                    new EmailRow("What they wrote", problem),
                    new EmailRow("Reference", lead.LeadNumber),
                    new EmailRow("Route", Route(lead)),
                    new EmailRow("Customer", $"{lead.CustomerName} · {lead.Phone}"),
                    new EmailRow("Partner", vendor is null ? "-" : $"{vendor.BusinessName} · {vendor.Phone}"),
                    new EmailRow("Partner's note", job.PartnerNote ?? "None")
                },
                CtaLabel = "Open problem reports",
                CtaUrl = _app.Url("/admin/job-issues")
            }, ct);

        // 2. Tell the partner, so they aren't surprised by a call.
        if (vendor is not null)
        {
            await _push.SendAsync(new[] { vendor.IdentityUserId }, new PushMessage(
                "Customer reported a problem",
                $"{lead.LeadNumber}: the ShiftingGuru team will contact you about it.",
                $"/lead/{lead.Id}",
                PushCategory.Bookings), ct);
        }

        return JobResult.Ok();
    }

    // =================================================================
    // Scheduled checks
    // =================================================================

    public async Task<int> AutoConfirmDueAsync(CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow - AutoConfirmAfter;

        var due = await _db.JobCompletions
            .Include(j => j.Lead!).ThenInclude(l => l.SelectedVendor)
            .Where(j => j.CustomerAnswer == JobCustomerAnswer.Waiting
                     && j.PartnerMarkedAt <= cutoff
                     && j.Lead!.Status == LeadStatus.Converted)
            .Take(200)
            .ToListAsync(ct);

        var done = 0;
        foreach (var job in due)
        {
            try
            {
                job.CustomerAnswer = JobCustomerAnswer.AutoConfirmed;
                job.CustomerAnsweredAt = DateTime.UtcNow;
                await CompleteLeadAsync(job.Lead!, ct);
                done++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Auto-confirm failed for lead {LeadId}", job.LeadId);
            }
        }

        if (done > 0) _logger.LogInformation("Auto-confirmed {Count} completed job(s).", done);
        return done;
    }

    public async Task<bool> SendDailyDigestAsync(CancellationToken ct = default)
    {
        var nowIndia = DateTime.UtcNow + IndiaOffset;
        if (nowIndia.Hour < DigestHourIndia) return false;

        var problems = await _db.JobCompletions.AsNoTracking()
            .Where(j => j.CustomerAnswer == JobCustomerAnswer.Problem
                     && j.ResolvedAt == null
                     && j.Lead!.Status == LeadStatus.Converted)
            .OrderBy(j => j.CustomerAnsweredAt)
            .Select(j => new { j.Lead!.LeadNumber, j.ProblemText, Vendor = j.Lead.SelectedVendor!.BusinessName })
            .Take(DigestListLimit)
            .ToListAsync(ct);

        var overdue = await OverdueQuery()
            .OrderBy(l => l.MovingDate)
            .Select(l => new { l.LeadNumber, l.MovingDate, Vendor = l.SelectedVendor!.BusinessName })
            .Take(DigestListLimit)
            .ToListAsync(ct);

        var waiting = await _db.JobCompletions.AsNoTracking()
            .CountAsync(j => j.CustomerAnswer == JobCustomerAnswer.Waiting && j.Lead!.Status == LeadStatus.Converted, ct);

        if (problems.Count == 0 && overdue.Count == 0) return false;   // nothing needs you today

        var rows = new List<EmailRow>();
        rows.AddRange(problems.Select(p => new EmailRow(
            $"Problem · {p.LeadNumber}", $"{p.Vendor}: \"{Shorten(p.ProblemText, 140)}\"")));
        rows.AddRange(overdue.Select(o => new EmailRow(
            $"Not marked · {o.LeadNumber}",
            $"{o.Vendor} · {(o.MovingDate is { } d ? "moved " + d.ToString("d MMM", India) : "no move date")}")));

        // One digest per day: the date is the "entity", so a second run the same day is ignored.
        var dayKey = int.Parse(nowIndia.ToString("yyyyMMdd", CultureInfo.InvariantCulture));

        await QueueAdminAsync(NotificationType.AdminJobsDigest,
            $"ShiftingGuru jobs needing attention - {nowIndia.ToString("d MMM", India)}", "JobsDigest", dayKey,
            new EmailContent
            {
                Heading = "Jobs needing your attention",
                Intro = $"{problems.Count} problem report(s) waiting for you, and {overdue.Count} booking(s) whose "
                      + $"move date has passed but the partner hasn't marked them complete. "
                      + $"({waiting} job(s) are waiting for the customer to confirm - nothing to do for those.)",
                Rows = rows.ToArray(),
                CtaLabel = "Open job issues",
                CtaUrl = _app.Url("/admin/job-issues")
            }, ct);

        return true;
    }

    // =================================================================
    // Admin
    // =================================================================

    public async Task<JobResult> ResolveAsync(int leadId, bool completeJob, string? note, string adminEmail, CancellationToken ct = default)
    {
        var job = await _db.JobCompletions
            .Include(j => j.Lead!).ThenInclude(l => l.SelectedVendor)
            .FirstOrDefaultAsync(j => j.LeadId == leadId, ct);

        if (job?.Lead is null || job.CustomerAnswer != JobCustomerAnswer.Problem || job.ResolvedAt is not null)
        {
            return JobResult.Fail("There's no open problem report on this booking.");
        }

        job.ResolvedAt = DateTime.UtcNow;
        job.ResolvedBy = adminEmail;
        job.ResolutionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim()[..Math.Min(note.Trim().Length, 500)];

        if (completeJob && job.Lead.Status == LeadStatus.Converted)
        {
            await CompleteLeadAsync(job.Lead, ct);
        }
        else
        {
            // Kept open: the partner can mark it complete again once it's sorted.
            await _db.SaveChangesAsync(ct);
        }

        return JobResult.Ok();
    }

    /// <summary>Converted bookings whose move date passed 2+ days ago (or flexible ones chosen 14+ days ago), never marked.</summary>
    public IQueryable<Lead> OverdueQuery()
    {
        var dateCutoff = TodayIndia.AddDays(-OverdueAfterDays);
        var flexibleCutoff = DateTime.UtcNow.AddDays(-FlexibleOverdueAfterDays);

        return _db.Leads.AsNoTracking()
            .Where(l => l.Status == LeadStatus.Converted
                     && l.SelectedVendorId != null
                     && !_db.JobCompletions.Any(j => j.LeadId == l.Id)
                     && ((l.MovingDate != null && l.MovingDate <= dateCutoff)
                         || (l.MovingDate == null && l.ConvertedAt != null && l.ConvertedAt <= flexibleCutoff)));
    }

    // =================================================================
    // Shared
    // =================================================================

    private JobCompletionView ViewOf(Lead lead, JobCompletion? job, bool forPartner)
    {
        var autoAt = job?.CustomerAnswer == JobCustomerAnswer.Waiting ? job.PartnerMarkedAt + AutoConfirmAfter : (DateTime?)null;
        // The customer sees their own words back; the partner doesn't see them in the app.
        var problemText = forPartner ? null : job?.ProblemText;

        if (lead.Status == LeadStatus.Completed)
            return new("completed", job?.PartnerMarkedAt, job?.PartnerNote, null, false, null, false, problemText);

        if (lead.Status != LeadStatus.Converted)
            return new("closed", job?.PartnerMarkedAt, job?.PartnerNote, null, false, null, false, problemText);

        if (job is null || (job.CustomerAnswer == JobCustomerAnswer.Problem && job.ResolvedAt is not null))
        {
            var state = job is null ? "notMarked" : "resolved";

            // Not before the move day: a job can't be done before it happens.
            if (lead.MovingDate is { } day && day > TodayIndia)
            {
                return new(state, job?.PartnerMarkedAt, job?.PartnerNote, null, false,
                    $"You can mark it complete from the move day ({day.ToString("d MMM", India)}).", false, problemText);
            }

            return new(state, job?.PartnerMarkedAt, job?.PartnerNote, null, forPartner, null, false, problemText);
        }

        return job.CustomerAnswer switch
        {
            JobCustomerAnswer.Waiting =>
                new("awaitingCustomer", job.PartnerMarkedAt, job.PartnerNote, autoAt, false, null, !forPartner, problemText),
            JobCustomerAnswer.Problem =>
                new("problem", job.PartnerMarkedAt, job.PartnerNote, null, false, null, false, problemText),
            _ =>
                new("completed", job.PartnerMarkedAt, job.PartnerNote, null, false, null, false, problemText)
        };
    }

    private async Task<(Lead? Lead, JobCompletion? Job)> LoadForCustomerAsync(int customerId, int leadId, CancellationToken ct)
    {
        var lead = await _db.Leads
            .Include(l => l.SelectedVendor)
            .FirstOrDefaultAsync(l => l.Id == leadId && l.CustomerId == customerId && l.SelectedVendorId != null, ct);

        var job = lead is null ? null : await _db.JobCompletions.FirstOrDefaultAsync(j => j.LeadId == leadId, ct);
        return (lead, job);
    }

    /// <summary>Exactly what the admin's Complete button does. Saves any pending changes too.</summary>
    private async Task CompleteLeadAsync(Lead lead, CancellationToken ct)
    {
        lead.Status = LeadStatus.Completed;
        lead.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        if (lead.SelectedVendor is not null)
        {
            // Emails, the "How was your move?" push and the review invitation.
            await _notifications.LeadCompletedAsync(lead, lead.SelectedVendor, ct);
        }
    }

    private async Task PushToCustomerAsync(Lead lead, PushMessage message, CancellationToken ct)
    {
        if (lead.CustomerId is null) return;

        var userId = await _db.Customers.AsNoTracking()
            .Where(c => c.Id == lead.CustomerId)
            .Select(c => c.IdentityUserId)
            .FirstOrDefaultAsync(ct);

        if (userId is not null) await _push.SendAsync(new[] { userId }, message, ct);
    }

    private static string Route(Lead lead) =>
        lead.MovingFrom is null ? lead.StorageLocation ?? "-" : $"{lead.MovingFrom} to {lead.MovingTo}";

    private static string Shorten(string? text, int max) =>
        string.IsNullOrEmpty(text) ? "" : text.Length <= max ? text : text[..max] + "…";

    // ---- Email: the same queue and template as NotificationService ----

    private Task QueueAdminAsync(NotificationType type, string subject, string entityType, int entityId,
        EmailContent content, CancellationToken ct)
    {
        var recipients = _email.AdminRecipientList;
        if (recipients.Count == 0)
        {
            _logger.LogWarning("No admin notification recipients configured; skipping {Type}.", type);
            return Task.CompletedTask;
        }

        return Task.WhenAll(recipients.Select(r => QueueAsync(type, r, subject, entityType, entityId, content, ct)));
    }

    /// <summary>Never throws - an email problem must not undo the job update.</summary>
    private async Task QueueAsync(NotificationType type, string recipient, string subject,
        string entityType, int entityId, EmailContent content, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(recipient)) return;

        try
        {
            var exists = await _db.NotificationLogs.AnyAsync(n =>
                n.Type == type && n.EntityType == entityType && n.EntityId == entityId && n.Recipient == recipient, ct);
            if (exists) return;

            var log = new NotificationLog
            {
                Type = type,
                Recipient = recipient,
                Subject = subject,
                HtmlBody = _template.RenderHtml(content),
                TextBody = _template.RenderText(content),
                EntityType = entityType,
                EntityId = entityId,
                Status = NotificationStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _db.NotificationLogs.Add(log);
            await _db.SaveChangesAsync(ct);
            _queue.Enqueue(log.Id);
        }
        catch (DbUpdateException)
        {
            _logger.LogInformation("Duplicate {Type} notification suppressed.", type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Couldn't queue a {Type} notification.", type);
        }
    }
}