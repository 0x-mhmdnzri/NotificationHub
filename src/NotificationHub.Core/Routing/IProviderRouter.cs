using NotificationHub.Abstractions.Channels;

namespace NotificationHub.Core.Routing;

/// <summary>Selects ordered providers for a channel based on preference, fallback, and health (SRP).</summary>
public interface IProviderRouter
{
    IReadOnlyList<IChannelPlugin> Resolve(string channel, string? preferredProvider, bool allowFallback);
}
