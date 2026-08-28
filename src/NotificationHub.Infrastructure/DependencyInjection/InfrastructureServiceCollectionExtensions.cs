using Microsoft.Extensions.DependencyInjection;
using NotificationHub.Application.DependencyInjection;
using NotificationHub.Domain.Broadcast;
using NotificationHub.Domain.Delivery;
using NotificationHub.Infrastructure.DomainAdapters;

namespace NotificationHub.Infrastructure.DependencyInjection;

/// <summary>
/// Infrastructure composition: wires Application (CQRS) on top of Core services.
/// Host should call AddInfrastructure after configuring DbContext / Rabbit / etc.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureCqrs(this IServiceCollection services)
    {
        services.AddApplication();
        // Domain repository ports (transitional in-memory; swap for EF adapters when aggregate mapping lands)
        services.AddSingleton<INotificationRepository, InMemoryNotificationRepository>();
        services.AddSingleton<IBroadcastCampaignRepository, InMemoryBroadcastCampaignRepository>();
        return services;
    }
}
