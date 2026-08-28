using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace NotificationHub.Abstractions.Plugins;

public interface IPlugin
{
    string Id { get; }
    Version Version { get; }
    string Name { get; }
    PluginCapability[] Capabilities { get; }

    Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default);
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task<PluginHealth> HealthCheckAsync(CancellationToken cancellationToken = default);
}

public sealed record PluginCapability(string Name, string? Value = null);

public sealed record PluginHealth(bool IsHealthy, string? Message = null);

public interface IPluginContext
{
    IServiceProvider Services { get; }
    IConfiguration Configuration { get; }
    ILogger Logger { get; }
}
