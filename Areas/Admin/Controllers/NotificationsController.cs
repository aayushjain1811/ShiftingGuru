using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Models;
using ShiftingGuru.Services.Notifications;
using ShiftingGuru.ViewModels.Admin;

namespace ShiftingGuru.Areas.Admin.Controllers;

[Area("Admin")]
[Route("admin/notifications")]
[Authorize(Roles = AdminSeeder.AdminRole)]
public class NotificationsController : Controller
{
    private const int PageSize = 30;

    private readonly ApplicationDbContext _db;
    private readonly NotificationQueue _queue;

    public NotificationsController(ApplicationDbContext db, NotificationQueue queue)
    {
        _db = db;
        _queue = queue;
    }

    // GET /admin/notifications
    [HttpGet("")]
    public async Task<IActionResult> Index(
        NotificationStatus? status, NotificationType? type,
        DateOnly? from, DateOnly? to, int page = 1, CancellationToken ct = default)
    {
        ViewData["Title"] = "Notifications";
        if (page < 1) page = 1;

        var query = _db.NotificationLogs.AsNoTracking().AsQueryable();

        if (status.HasValue && Enum.IsDefined(status.Value))
        {
            query = query.Where(n => n.Status == status.Value);
        }

        if (type.HasValue && Enum.IsDefined(type.Value))
        {
            query = query.Where(n => n.Type == type.Value);
        }

        if (from.HasValue)
        {
            var fromUtc = from.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(n => n.CreatedAt >= fromUtc);
        }

        if (to.HasValue)
        {
            var toUtc = to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(n => n.CreatedAt < toUtc);
        }

        var total = await query.CountAsync(ct);

        var totalPages = total == 0 ? 1 : (int)Math.Ceiling(total / (double)PageSize);
        if (page > totalPages) page = totalPages;

        var notifications = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(ct);

        // One grouped query for the two headline counts.
        var counts = await _db.NotificationLogs
            .AsNoTracking()
            .GroupBy(n => n.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return View(new AdminNotificationListViewModel
        {
            Notifications = notifications,
            Status = status,
            Type = type,
            FromDate = from,
            ToDate = to,
            Page = page,
            PageSize = PageSize,
            TotalCount = total,
            PendingCount = counts.FirstOrDefault(c => c.Status == NotificationStatus.Pending)?.Count ?? 0,
            FailedCount = counts.FirstOrDefault(c => c.Status == NotificationStatus.Failed)?.Count ?? 0
        });
    }

    // POST /admin/notifications/42/retry
    [HttpPost("{id:int}/retry")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Retry(int id, CancellationToken ct)
    {
        var notification = await _db.NotificationLogs.FirstOrDefaultAsync(n => n.Id == id, ct);

        if (notification is null)
        {
            TempData["AdminError"] = "That notification no longer exists.";
            return RedirectToAction(nameof(Index));
        }

        // Already-sent notifications are never re-sent from here. Resending a
        // successful email is a business decision, not a maintenance action.
        if (notification.Status == NotificationStatus.Sent)
        {
            TempData["AdminError"] = "That notification was already sent.";
            return RedirectToAction(nameof(Index));
        }

        notification.Status = NotificationStatus.Pending;
        notification.ErrorMessage = null;
        await _db.SaveChangesAsync(ct);

        _queue.Enqueue(notification.Id);

        TempData["AdminMessage"] = "Notification queued for another attempt.";
        return RedirectToAction(nameof(Index));
    }
}