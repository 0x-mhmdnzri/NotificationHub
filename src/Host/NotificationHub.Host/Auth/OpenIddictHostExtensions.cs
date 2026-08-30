using NotificationHub.Core.Persistence;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace NotificationHub.Host.Auth;

/// <summary>
/// Registers OpenIddict on the API host.
/// Login/register issue API JWTs; OpenIddict exposes the token endpoint surface.
/// </summary>
public static class OpenIddictHostExtensions
{
    public static IServiceCollection AddNotificationHubOpenIddict(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddOpenIddict()
            .AddCore(o => o.UseEntityFrameworkCore().UseDbContext<NotificationDbContext>())
            .AddServer(o =>
            {
                o.SetTokenEndpointUris("/connect/token");

                o.AllowPasswordFlow()
                    .AllowRefreshTokenFlow()
                    .AllowClientCredentialsFlow();

                o.RegisterScopes(Scopes.OpenId, Scopes.Profile, Scopes.Email, "notificationhub.admin");

                o.AddDevelopmentEncryptionCertificate()
                    .AddDevelopmentSigningCertificate();

                o.UseAspNetCore()
                    .EnableTokenEndpointPassthrough();
            })
            .AddValidation(o =>
            {
                o.UseLocalServer();
                o.UseAspNetCore();
            });

        return services;
    }

    public static async Task SeedOpenIddictClientsAsync(IServiceProvider sp, CancellationToken ct = default)
    {
        var manager = sp.GetRequiredService<IOpenIddictApplicationManager>();
        if (await manager.FindByClientIdAsync("admin-api", ct) is null)
        {
            await manager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = "admin-api",
                DisplayName = "NotificationHub Admin API (password login)",
                ClientType = ClientTypes.Public,
                Permissions =
                {
                    Permissions.Endpoints.Token,
                    Permissions.GrantTypes.Password,
                    Permissions.GrantTypes.RefreshToken,
                    Permissions.Scopes.Email,
                    Permissions.Scopes.Profile,
                    Permissions.Prefixes.Scope + "notificationhub.admin"
                }
            }, ct);
        }
    }
}
