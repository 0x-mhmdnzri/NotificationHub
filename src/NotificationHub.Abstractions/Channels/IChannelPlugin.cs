using NotificationHub.Abstractions.Models;
using NotificationHub.Abstractions.Plugins;

namespace NotificationHub.Abstractions.Channels;

public interface IChannelPlugin : IPlugin
{
    string Channel { get; }

    Task<DeliveryResult> SendAsync(RenderedNotification notification, CancellationToken cancellationToken = default);
}
