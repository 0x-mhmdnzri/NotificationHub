using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Campaigns;

public interface IBroadcastService
{
    Task<BroadcastResult> SendAsync(BroadcastRequest request, CancellationToken ct = default);
}
