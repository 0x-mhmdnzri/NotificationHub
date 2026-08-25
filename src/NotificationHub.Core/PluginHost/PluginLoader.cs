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

    public void Register(IPlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        if (_loadedPlugins.Any(p => p.Id == plugin.Id))
            return;
        _loadedPlugins.Add(plugin);
        _logger.LogInformation("Registered plugin {PluginId} v{Version} ({Name})", plugin.Id, plugin.Version, plugin.Name);
    }

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
                await using var stream = File.OpenRead(dll);
                var assembly = loadContext.LoadFromStream(stream);
                await RegisterFromAssemblyAsync(assembly, context, ct);
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
            await RegisterFromAssemblyAsync(assembly, context, ct);
    }

    private async Task RegisterFromAssemblyAsync(Assembly assembly, IPluginContext context, CancellationToken ct)
    {
        var pluginTypes = assembly.GetTypes()
            .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var type in pluginTypes)
        {
            if (Activator.CreateInstance(type) is not IPlugin plugin)
                continue;

            await plugin.InitializeAsync(context, ct);
            await plugin.StartAsync(ct);
            Register(plugin);
        }
    }
}
