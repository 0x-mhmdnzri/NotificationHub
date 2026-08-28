using Microsoft.Extensions.DependencyInjection;
using NotificationHub.Application.DependencyInjection;
using NotificationHub.Domain.Common;
using NotificationHub.Domain.Broadcast;
using NotificationHub.Domain.Delivery;
using NotificationHub.Domain.Events;
using NotificationHub.Domain.Preferences;
using NotificationHub.Domain.Templates;
using NotificationHub.Infrastructure.Messaging;
using NotificationHub.Infrastructure.Persistence;

namespace NotificationHub.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureCqrs(this IServiceCollection services)
    {
        services.AddApplication();
        services.AddScoped<INotificationRepository, EfNotificationRepository>();
        services.AddScoped<IBroadcastCampaignRepository, EfBroadcastCampaignRepository>();
        services.AddScoped<IUserPreferenceRepository, EfUserPreferenceRepository>();
        services.AddScoped<INotificationTemplateRepository, EfNotificationTemplateRepository>();
        services.AddScoped<IDomainEventDispatcher, OutboxDomainEventDispatcher>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        return services;
    }
}
