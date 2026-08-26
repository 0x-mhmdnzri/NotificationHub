using NotificationHub.Abstractions.Plugins;

namespace NotificationHub.Host;

internal sealed class SimplePluginContext : IPluginContext
{
    public SimplePluginContext(IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        Services = services;
        Configuration = configuration;
        Logger = logger;
    }

    public IServiceProvider Services { get; }
    public IConfiguration Configuration { get; }
    public ILogger Logger { get; }
}
