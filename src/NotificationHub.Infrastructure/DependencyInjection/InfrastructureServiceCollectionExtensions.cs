using Microsoft.Extensions.DependencyInjection;
using NotificationHub.Application.DependencyInjection;

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
        return services;
    }
}
