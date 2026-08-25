using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Queue;

public interface INotificationQueue
{
    ValueTask EnqueueAsync(NotificationRequest request, CancellationToken ct = default);
    IAsyncEnumerable<NotificationRequest> DequeueAsync(CancellationToken ct = default);
}
