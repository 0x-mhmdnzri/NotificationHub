using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using NotificationHub.Abstractions.Plugins;

namespace NotificationHub.Host.Composition;

public static class PluginServiceCollectionExtensions
{
    /// <summary>
    /// Registers all IPlugin implementations from referenced plugin assemblies (Singleton, Append).
    /// Discovery boundary = assemblies that already reference this Host (compile-time plugins).
    /// </summary>
    public static IServiceCollection AddChannelPlugins(this IServiceCollection services)
    {
        var pluginAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a =>
            {
                var name = a.GetName().Name ?? "";
                return name.StartsWith("NotificationHub.Plugins.", StringComparison.Ordinal);
            })
            .ToArray();

        // Ensure plugin assemblies are loaded (ProjectReference may not load until type touch)
        pluginAssemblies = EnsurePluginAssembliesLoaded(pluginAssemblies);

        services.Scan(scan => scan
            .FromAssemblies(pluginAssemblies)
            .AddClasses(c => c.AssignableTo<IPlugin>())
            .AsImplementedInterfaces()
            .WithSingletonLifetime());

        return services;
    }

    private static Assembly[] EnsurePluginAssembliesLoaded(Assembly[] already)
    {
        var known = new[]
        {
            "NotificationHub.Plugins.Email.SendGrid",
            "NotificationHub.Plugins.Email.Smtp",
            "NotificationHub.Plugins.Email.Resend",
            "NotificationHub.Plugins.Email.Ses",
            "NotificationHub.Plugins.Sms.Kavenegar",
            "NotificationHub.Plugins.Sms.SmsIr",
            "NotificationHub.Plugins.Sms.Twilio",
            "NotificationHub.Plugins.InApp",
            "NotificationHub.Plugins.Chat.Slack",
            "NotificationHub.Plugins.Chat.WhatsApp",
            "NotificationHub.Plugins.Chat.Telegram",
            "NotificationHub.Plugins.Chat.Discord",
            "NotificationHub.Plugins.Chat.Teams",
            "NotificationHub.Plugins.Push.Fcm",
            "NotificationHub.Plugins.Push.Expo"
        };

        var list = already.ToList();
        foreach (var name in known)
        {
            if (list.Any(a => a.GetName().Name == name))
                continue;
            try
            {
                list.Add(Assembly.Load(name));
            }
            catch
            {
                // optional plugin not referenced
            }
        }

        return list.ToArray();
    }
}
