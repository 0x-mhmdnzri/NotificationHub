using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using NotificationHub.Application.Common.Behaviors;

namespace NotificationHub.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);

            // Order matters: validation → logging → side-specific
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(CommandOnlyBehavior<,>));
            cfg.AddOpenBehavior(typeof(QueryOnlyBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);
        return services;
    }
}
