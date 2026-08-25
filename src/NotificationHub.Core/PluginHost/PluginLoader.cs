using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging;
using NotificationHub.Abstractions.Plugins;

namespace NotificationHub.Core.PluginHost;

public sealed class PluginLoader
{
    private readonly ILogger<PluginLoader> _logger;
    private readonly List<IPlugin> _loadedPlugins = new();

    public PluginLoader(ILogger<PluginLoader> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<IPlugin> LoadedPlugins => _loadedPlugins.AsReadOnly();

    public async Task LoadFromDirectoryAsync(string directory, IPluginContext context, CancellationToken ct = default)
    {
        if (!Directory.Exists(directory))
        {
            _logger.LogWarning("Plugin directory not found: {Directory}", directory);
            return;
        }

        foreach (var dll in Directory.GetFiles(directory, "*.dll", SearchOption.AllDirectories))
        {
            try
            {
                var loadContext = new AssemblyLoadContext($"Plugin_{Path.GetFileNameWithoutExtension(dll)}", isCollectible: true);
                using var stream = File.OpenRead(dll);
                var assembly = loadContext.LoadFromStream(stream);

                var pluginTypes = assembly.GetTypes()
                    .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                foreach (var type in pluginTypes)
                {
                    if (Activator.CreateInstance(type) is IPlugin plugin)
                    {
                        await plugin.InitializeAsync(context, ct);
                        await plugin.StartAsync(ct);
                        _loadedPlugins.Add(plugin);
                        _logger.LogInformation("Loaded plugin {PluginId} v{Version} ({Name})", plugin.Id, plugin.Version, plugin.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load plugin from {Dll}", dll);
            }
        }
    }

    public async Task LoadFromAssembliesAsync(IEnumerable<Assembly> assemblies, IPluginContext context, CancellationToken ct = default)
    {
        foreach (var assembly in assemblies)
        {
            var pluginTypes = assembly.GetTypes()
                .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            foreach (var type in pluginTypes)
            {
                if (Activator.CreateInstance(type) is IPlugin plugin)
                {
                    await plugin.InitializeAsync(context, ct);
                    await plugin.StartAsync(ct);
                    _loadedPlugins.Add(plugin);
                    _logger.LogInformation("Loaded plugin {PluginId} v{Version} ({Name})", plugin.Id, plugin.Version, plugin.Name);
                }
            }
        }
    }
}
