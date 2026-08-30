using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NotificationHub.Abstractions.Channels;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.PluginHost;

namespace NotificationHub.Core.Routing;

/// <summary>
/// Orders providers by preferred config, then health. Open for alternative strategies via IProviderRouter (OCP).
/// </summary>
public sealed class HealthAwareProviderRouter : IProviderRouter
{
    private readonly PluginLoader _pluginLoader;
    private readonly IProviderHealthTracker _health;
    private readonly ProviderOptions _providerOptions;
    private readonly ProviderHealthOptions _healthOptions;
    private readonly ILogger<HealthAwareProviderRouter> _logger;

    public HealthAwareProviderRouter(
        PluginLoader pluginLoader,
        IProviderHealthTracker health,
        IOptions<ProviderOptions> providerOptions,
        IOptions<ProviderHealthOptions> healthOptions,
        ILogger<HealthAwareProviderRouter> logger)
    {
        _pluginLoader = pluginLoader;
        _health = health;
        _providerOptions = providerOptions.Value;
        _healthOptions = healthOptions.Value;
        _logger = logger;
    }

    public IReadOnlyList<IChannelPlugin> Resolve(string channel, string? preferredProvider, bool allowFallback)
    {
        var all = _pluginLoader.LoadedPlugins.OfType<IChannelPlugin>()
            .Where(p => p.Channel.Equals(channel, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (all.Count == 0)
            return all;

        var configuredOrder = channel.Equals("email", StringComparison.OrdinalIgnoreCase)
            ? _providerOptions.EmailFallbackOrder
            : channel.Equals("sms", StringComparison.OrdinalIgnoreCase)
                ? _providerOptions.SmsFallbackOrder
                : all.Select(p => p.Id).ToArray();

        if (!string.IsNullOrWhiteSpace(preferredProvider))
            configuredOrder = new[] { preferredProvider }.Concat(configuredOrder.Where(x => !x.Equals(preferredProvider, StringComparison.OrdinalIgnoreCase))).ToArray();
        else if (channel.Equals("email", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(_providerOptions.PreferredEmailProvider))
            configuredOrder = new[] { _providerOptions.PreferredEmailProvider! }
                .Concat(configuredOrder.Where(x => x != _providerOptions.PreferredEmailProvider)).ToArray();
        else if (channel.Equals("sms", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(_providerOptions.PreferredSmsProvider))
            configuredOrder = new[] { _providerOptions.PreferredSmsProvider! }
                .Concat(configuredOrder.Where(x => x != _providerOptions.PreferredSmsProvider)).ToArray();

        var ordered = configuredOrder
            .Select(id => all.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
            .Where(p => p is not null)
            .Cast<IChannelPlugin>()
            .ToList();

        foreach (var p in all.Where(p => ordered.All(o => o.Id != p.Id)))
            ordered.Add(p);

        if (_healthOptions.DeprioritizeUnhealthy && ordered.Count > 1)
        {
            var healthy = new List<IChannelPlugin>();
            var unhealthy = new List<IChannelPlugin>();
            foreach (var plugin in ordered)
            {
                var snap = _health.GetHealth(plugin.Id, channel);
                if (snap.IsHealthy)
                    healthy.Add(plugin);
                else
                {
                    unhealthy.Add(plugin);
                    _logger.LogWarning(
                        "Provider {ProviderId} deprioritized (successRate={Rate}, samples={Samples})",
                        plugin.Id, snap.SuccessRate, snap.TotalSamples);
                }
            }
            ordered = healthy.Concat(unhealthy).ToList();
        }

        if (!allowFallback && ordered.Count > 1)
            return ordered.Take(1).ToList();

        return ordered;
    }
}
