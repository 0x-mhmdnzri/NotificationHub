using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using NotificationHub.Core.Auth;
using NotificationHub.Core.Identity;

namespace NotificationHub.Host.Security;

public static class JwtAuthExtensions
{
    /// <summary>
    /// Registers JWT Bearer when Auth:JwtBearer:Enabled=true.
    /// Does not remove or alter API Key middleware.
    /// </summary>
    public static IServiceCollection AddNotificationHubJwtBearer(
        this IServiceCollection services, IConfiguration config)
    {
        services.Configure<JwtBearerAuthOptions>(config.GetSection(JwtBearerAuthOptions.SectionName));
        var opts = config.GetSection(JwtBearerAuthOptions.SectionName).Get<JwtBearerAuthOptions>()
                   ?? new JwtBearerAuthOptions();

        services.AddScoped<ITenantContext, JwtTenantContext>();
        services.AddScoped<JwtSecurityContextFactory>();
        services.AddScoped<ISecurityContext>(sp => sp.GetRequiredService<JwtSecurityContextFactory>().Create());

        if (!opts.Enabled || string.IsNullOrWhiteSpace(opts.Authority))
        {
            // Placeholders so DI resolves; API Key path remains primary until Identity host is up.
            services.AddAuthentication();
            return services;
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.Authority = opts.Authority;
                o.Audience = opts.Audience;
                o.RequireHttpsMetadata = opts.RequireHttpsMetadata;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    NameClaimType = "sub",
                    RoleClaimType = "role"
                };
            });

        return services;
    }
}
