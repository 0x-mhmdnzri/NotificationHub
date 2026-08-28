using Microsoft.Extensions.DependencyInjection;
using NotificationHub.Application.DependencyInjection;
using NotificationHub.Domain.Broadcast;
using NotificationHub.Domain.Common;
using NotificationHub.Domain.Delivery;
using NotificationHub.Domain.Events;
using NotificationHub.Domain.Preferences;
using NotificationHub.Domain.Templates;
using NotificationHub.Infrastructure.HangfireJobs;
using NotificationHub.Infrastructure.Messaging;
using NotificationHub.Infrastructure.Messaging.Integration;
using NotificationHub.Infrastructure.Persistence;
using Scrutor;

namespace NotificationHub.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// CQRS application + domain ports (repositories, UoW, domain→integration dispatcher).
    /// MediatR remains the registration authority for handlers (not Scrutor).
    /// </summary>
    public static IServiceCollection AddInfrastructureCqrs(this IServiceCollection services)
    {
        services.AddApplication();

        // Domain repository ports — explicit contracts, Scoped (DbContext-bound)
        services.Scan(scan => scan
            .FromAssemblyOf<InfrastructureAssemblyMarker>()
            .AddClasses(c => c.AssignableTo<INotificationRepository>())
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        services.Scan(scan => scan
            .FromAssemblyOf<InfrastructureAssemblyMarker>()
            .AddClasses(c => c.AssignableTo<IBroadcastCampaignRepository>())
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        services.Scan(scan => scan
            .FromAssemblyOf<InfrastructureAssemblyMarker>()
            .AddClasses(c => c.AssignableTo<IUserPreferenceRepository>())
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        services.Scan(scan => scan
            .FromAssemblyOf<InfrastructureAssemblyMarker>()
            .AddClasses(c => c.AssignableTo<INotificationTemplateRepository>())
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        services.Scan(scan => scan
            .FromAssemblyOf<InfrastructureAssemblyMarker>()
            .AddClasses(c => c.AssignableTo<IDomainEventDispatcher>())
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        services.Scan(scan => scan
            .FromAssemblyOf<InfrastructureAssemblyMarker>()
            .AddClasses(c => c.AssignableTo<IUnitOfWork>())
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        return services;
    }

    /// <summary>
    /// Hangfire job types (scoped) — only concrete Hangfire job classes.
    /// Scheduler remains explicit (Hangfire vs Null based on config).
    /// </summary>
    public static IServiceCollection AddHangfireJobs(this IServiceCollection services)
    {
        services.Scan(scan => scan
            .FromAssemblyOf<InfrastructureAssemblyMarker>()
            .AddClasses(c => c.Where(t =>
                t.Namespace is not null &&
                t.Namespace.Contains("HangfireJobs") &&
                t is { IsAbstract: false, IsInterface: false } &&
                t != typeof(HangfireOutboxDispatchScheduler) &&
                t.Name.EndsWith("Job", StringComparison.Ordinal)))
            .AsSelf()
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        return services;
    }

    public static IServiceCollection AddIntegrationMessaging(this IServiceCollection services)
    {
        services.Scan(scan => scan
            .FromAssemblyOf<InfrastructureAssemblyMarker>()
            .AddClasses(c => c.InNamespaces(
                "NotificationHub.Infrastructure.Messaging.Integration")
                .Where(t => t.Name != "DomainEventToIntegrationMapper" &&
                            !t.Name.EndsWith("Mapper", StringComparison.Ordinal)))
            .AsSelf()
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        return services;
    }
}
