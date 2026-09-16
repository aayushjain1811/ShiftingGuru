using ShiftingGuru.Models;

namespace ShiftingGuru.ViewModels.Admin;

public class AdminNotificationListViewModel
{
    public IReadOnlyList<NotificationLog> Notifications { get; set; } = Array.Empty<NotificationLog>();

    public NotificationStatus? Status { get; set; }
    public NotificationType? Type { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 30;
    public int TotalCount { get; set; }

    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;

    public int PendingCount { get; set; }
    public int FailedCount { get; set; }

    public IDictionary<string, string?> RouteValues(int page) => new Dictionary<string, string?>
    {
        ["status"] = Status?.ToString(),
        ["type"] = Type?.ToString(),
        ["from"] = FromDate?.ToString("yyyy-MM-dd"),
        ["to"] = ToDate?.ToString("yyyy-MM-dd"),
        ["page"] = page.ToString()
    };
}