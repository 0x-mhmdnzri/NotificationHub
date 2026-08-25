using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging;
using NotificationHub.Abstractions.Plugins;

namespace NotificationHub.Core.PluginHost;

/// <summary>Microkernel plugin host — register, directory load, unload (F20).</summary>
public sealed class PluginLoader
{
    private readonly ILogger<PluginLoader> _logger;
    private readonly List<IPlugin> _loadedPlugins = new();
    private readonly Dictionary<string, AssemblyLoadContext> _contexts = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();

    public PluginLoader(ILogger<PluginLoader> logger) => _logger = logger;

    public IReadOnlyList<IPlugin> LoadedPlugins
    {
        get { lock (_sync) return _loadedPlugins.ToList().AsReadOnly(); }
    }

    public void Register(IPlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        lock (_sync)
        {
            if (_loadedPlugins.Any(p => p.Id == plugin.Id))
                return;
            _loadedPlugins.Add(plugin);
        }
        _logger.LogInformation("Registered plugin {PluginId} v{Version} ({Name})", plugin.Id, plugin.Version, plugin.Name);
    }

    public async Task UnregisterAsync(string pluginId, CancellationToken ct = default)
    {
        IPlugin? plugin;
        lock (_sync)
        {
            plugin = _loadedPlugins.FirstOrDefault(p => p.Id == pluginId);
            if (plugin is null) return;
            _loadedPlugins.Remove(plugin);
        }
        try { await plugin.StopAsync(ct); } catch (Exception ex) { _logger.LogWarning(ex, "Stop failed for {Id}", pluginId); }
        if (_contexts.Remove(pluginId, out var alc))
        {
            alc.Unload();
            _logger.LogInformation("Unloaded ALC for plugin {Id}", pluginId);
        }
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
            // skip host/shared deps
            var name = Path.GetFileName(dll);
            if (name.StartsWith("NotificationHub.Core", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("NotificationHub.Abstractions", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("System.", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                var loadContext = new AssemblyLoadContext($"Plugin_{Path.GetFileNameWithoutExtension(dll)}_{Guid.NewGuid():N}", isCollectible: true);
                await using var stream = File.OpenRead(dll);
                var assembly = loadContext.LoadFromStream(stream);
                await RegisterFromAssemblyAsync(assembly, context, loadContext, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load plugin from {Dll}", dll);
            }
        }
    }

    public async Task ReloadDirectoryAsync(string directory, IPluginContext context, CancellationToken ct = default)
    {
        _logger.LogInformation("Hot-reload plugins from {Dir}", directory);
        await LoadFromDirectoryAsync(directory, context, ct);
    }

    public async Task LoadFromAssembliesAsync(IEnumerable<Assembly> assemblies, IPluginContext context, CancellationToken ct = default)
    {
        foreach (var assembly in assemblies)
            await RegisterFromAssemblyAsync(assembly, context, null, ct);
    }

    private async Task RegisterFromAssemblyAsync(Assembly assembly, IPluginContext context, AssemblyLoadContext? alc, CancellationToken ct)
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
            if (alc is not null)
                _contexts[plugin.Id] = alc;
        }
    }
}
