using System.Threading.Channels;

namespace ShiftingGuru.Services.Notifications;

/// <summary>
/// In-process handoff between a web request and the sending worker. It carries
/// only NotificationLog ids - the content is already safely in the database, so
/// a dropped queue item costs a delay, not a lost notification.
/// </summary>
public class NotificationQueue
{
    private readonly Channel<int> _channel =
        Channel.CreateUnbounded<int>(new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(int notificationId) => _channel.Writer.TryWrite(notificationId);

    public IAsyncEnumerable<int> ReadAllAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);
}