using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services.Email;

namespace ShiftingGuru.Services.Notifications;

/// <summary>
/// Background worker. Sweeps any Pending rows left behind by a previous run,
/// then drains the queue for as long as the app is up.
/// </summary>
public class NotificationSender : BackgroundService
{
    private readonly NotificationQueue _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<NotificationSender> _logger;

    public NotificationSender(
        NotificationQueue queue, IServiceScopeFactory scopes, ILogger<NotificationSender> logger)
    {
        _queue = queue;
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await RecoverPendingAsync(ct);

        await foreach (var id in _queue.ReadAllAsync(ct))
        {
            await TrySendAsync(id, ct);
        }
    }

    /// <summary>
    /// Anything left Pending was queued before the process stopped. Because the
    /// row was written before the send was attempted, nothing is lost.
    /// </summary>
    private async Task RecoverPendingAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var pending = await db.NotificationLogs
                .AsNoTracking()
                .Where(n => n.Status == NotificationStatus.Pending)
                .OrderBy(n => n.CreatedAt)
                .Select(n => n.Id)
                .Take(500)
                .ToListAsync(ct);

            if (pending.Count == 0) return;

            _logger.LogInformation("Recovering {Count} pending notifications.", pending.Count);

            foreach (var id in pending) _queue.Enqueue(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Couldn't recover pending notifications at startup.");
        }
    }

    private async Task TrySendAsync(int id, CancellationToken ct)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var email = scope.ServiceProvider.GetRequiredService<IEmailService>();

            var notification = await db.NotificationLogs.FirstOrDefaultAsync(n => n.Id == id, ct);
            if (notification is null || notification.Status == NotificationStatus.Sent) return;

            notification.AttemptCount++;
            notification.LastAttemptAt = DateTime.UtcNow;

            var result = await email.SendAsync(
                notification.Recipient, notification.Subject,
                notification.HtmlBody, notification.TextBody, ct);

            if (result.Succeeded)
            {
                notification.Status = NotificationStatus.Sent;
                notification.SentAt = DateTime.UtcNow;
                notification.ErrorMessage = null;
            }
            else
            {
                notification.Status = NotificationStatus.Failed;
                notification.ErrorMessage = result.Error;
            }

            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // A failure here must never take the worker down - the row stays
            // Pending or Failed and can be retried from the admin page.
            _logger.LogError(ex, "Notification {NotificationId} could not be processed.", id);
        }
    }
}