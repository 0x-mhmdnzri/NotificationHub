using Microsoft.AspNetCore.Authorization;
using NotificationHub.Core.Identity;

namespace NotificationHub.Host.Security;

public static class RbacServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationHubRbac(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddAuthorization();
        return services;
    }
}
