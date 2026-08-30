using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using NotificationHub.Core.Environments;
using NotificationHub.Core.Expressions;
using NotificationHub.Core.I18n;
using NotificationHub.Core.Messaging;
using NotificationHub.Core.Observability;
using NotificationHub.Core.Preferences;
using NotificationHub.Core.Queue;
using NotificationHub.Core.Routing;
using NotificationHub.Core.Security;
using NotificationHub.Core.Sync;
using NotificationHub.Core.Templates;
using NotificationHub.Core.Webhooks;
using NotificationHub.Core.Workflow;

namespace NotificationHub.Core.DependencyInjection;

/// <summary>
/// Platform service registration via Scrutor — narrow filters, explicit lifetimes.
/// Conditional infrastructure (Redis vs InMemory, Hangfire vs Null) stays in Host.
/// </summary>
public static class CoreServiceCollectionExtensions
{
    public static IServiceCollection AddCorePlatform(this IServiceCollection services)
    {
        // --- Matching FooService → IFooService (Scoped) ---
        // Exclude PreferenceService (decorated), Metrics (Singleton), Health (separate)
        services.Scan(scan => scan
            .FromAssemblyOf<CoreAssemblyMarker>()
            .AddClasses(c => c.Where(t =>
                t.Name.EndsWith("Service", StringComparison.Ordinal) &&
                t is { IsAbstract: false, IsInterface: false } &&
                t != typeof(PreferenceService) &&
                t != typeof(CachingPreferenceService) &&
                !t.Name.Contains("Metrics", StringComparison.Ordinal) &&
                !t.Name.Contains("Health", StringComparison.Ordinal) &&
                t.Namespace is not null &&
                !t.Namespace.Contains("RateLimiting")))
            .AsMatchingInterface()
            .WithScopedLifetime());

        // Messaging health
        services.Scan(scan => scan
            .FromAssemblyOf<CoreAssemblyMarker>()
            .AddClasses(c => c.AssignableTo<IMessagingHealthService>())
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        // Webhooks (Dispatcher naming, not *Service)
        services.Scan(scan => scan
            .FromAssemblyOf<CoreAssemblyMarker>()
            .AddClasses(c => c.AssignableTo<IWebhookDispatcher>())
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        // Workflow step handlers — multiple intentional (Append default)
        services.Scan(scan => scan
            .FromAssemblyOf<CoreAssemblyMarker>()
            .AddClasses(c => c.AssignableTo<IWorkflowStepHandler>())
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        services.Scan(scan => scan
            .FromAssemblyOf<CoreAssemblyMarker>()
            .AddClasses(c => c.AssignableTo<IWorkflowEngine>())
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        services.Scan(scan => scan
            .FromAssemblyOf<CoreAssemblyMarker>()
            .AddClasses(c => c.AssignableTo<IWorkflowRunRepository>())
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        services.Scan(scan => scan
            .FromAssemblyOf<CoreAssemblyMarker>()
            .AddClasses(c => c.AssignableTo<IWorkflowTimeline>())
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        // Expression evaluator — Singleton (stateless)
        services.Scan(scan => scan
            .FromAssemblyOf<CoreAssemblyMarker>()
            .AddClasses(c => c.AssignableTo<IExpressionEvaluator>())
            .AsImplementedInterfaces()
            .WithSingletonLifetime());

        // Security
        services.Scan(scan => scan
            .FromAssemblyOf<CoreAssemblyMarker>()
            .AddClasses(c => c.AssignableTo<IApiKeyStore>())
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        services.Scan(scan => scan
            .FromAssemblyOf<CoreAssemblyMarker>()
            .AddClasses(c => c.AssignableTo<IApiKeyValidator>())
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        services.Scan(scan => scan
            .FromAssemblyOf<CoreAssemblyMarker>()
            .AddClasses(c => c.Where(t => t == typeof(ApiKeyBootstrapper)))
            .AsSelf()
            .WithScopedLifetime());

        // Templates
        services.Scan(scan => scan
            .FromAssemblyOf<CoreAssemblyMarker>()
            .AddClasses(c => c.AssignableTo<ITemplateStore>())
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        services.Scan(scan => scan
            .FromAssemblyOf<CoreAssemblyMarker>()
            .AddClasses(c => c.AssignableTo<ITemplateRenderer>())
            .AsImplementedInterfaces()
            .WithSingletonLifetime());

        services.Scan(scan => scan
            .FromAssemblyOf<CoreAssemblyMarker>()
            .AddClasses(c => c.AssignableTo<ITemplateEngine>())
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        services.Scan(scan => scan
            .FromAssemblyOf<CoreAssemblyMarker>()
            .AddClasses(c => c.Where(t => t.Name == "TemplateSeeder"))
            .AsSelf()
            .WithScopedLifetime());

        // Provider routing (Singleton)
        services.Scan(scan => scan
            .FromAssemblyOf<CoreAssemblyMarker>()
            .AddClasses(c => c.AssignableTo<IProviderHealthTracker>())
            .AsImplementedInterfaces()
            .WithSingletonLifetime());

        services.Scan(scan => scan
            .FromAssemblyOf<CoreAssemblyMarker>()
            .AddClasses(c => c.AssignableTo<IProviderRouter>())
            .AsImplementedInterfaces()
            .WithSingletonLifetime());

        // Outbox / Inbox / status store / notification queue
        services.Scan(scan => scan
            .FromAssemblyOf<CoreAssemblyMarker>()
            .AddClasses(c => c.Where(t =>
                t.Name is "EfOutbox" or "EfInbox" or "PostgresNotificationStatusStore" or "OutboxNotificationQueue"))
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        // Metrics + Environment (Singleton)
        services.Scan(scan => scan
            .FromAssemblyOf<CoreAssemblyMarker>()
            .AddClasses(c => c.AssignableTo<IMetricsService>())
            .AsImplementedInterfaces()
            .WithSingletonLifetime());

        services.Scan(scan => scan
            .FromAssemblyOf<CoreAssemblyMarker>()
            .AddClasses(c => c.AssignableTo<IEnvironmentContext>())
            .AsImplementedInterfaces()
            .WithSingletonLifetime());

        services.Scan(scan => scan
            .FromAssemblyOf<CoreAssemblyMarker>()
            .AddClasses(c => c.AssignableTo<ILocalizationCatalog>())
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        services.Scan(scan => scan
            .FromAssemblyOf<CoreAssemblyMarker>()
            .AddClasses(c => c.AssignableTo<ICrossChannelReadSync>())
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        // Preference decorator (explicit)
        services.AddScoped<PreferenceService>();
        services.AddScoped<IPreferenceService>(sp =>
            new CachingPreferenceService(
                sp.GetRequiredService<PreferenceService>(),
                sp.GetRequiredService<IMemoryCache>()));

        // Plugin loader + orchestrator
        services.Scan(scan => scan
            .FromAssemblyOf<CoreAssemblyMarker>()
            .AddClasses(c => c.Where(t => t.Name == "PluginLoader"))
            .AsSelf()
            .WithSingletonLifetime());

        services.Scan(scan => scan
            .FromAssemblyOf<CoreAssemblyMarker>()
            .AddClasses(c => c.Where(t => t.Name == "NotificationOrchestrator"))
            .AsSelf()
            .WithScopedLifetime());

        return services;
    }
}
