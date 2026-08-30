using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using NotificationHub.Core.Auth;
using NotificationHub.Core.Identity;
using NotificationHub.Host.Auth;

namespace NotificationHub.Host.Security;

public static class JwtAuthExtensions
{
    /// <summary>
    /// JWT Bearer for human clients. API Key middleware remains for machine clients.
    /// Supports local HS256 (Auth:Jwt) and optional external Authority (Auth:JwtBearer).
    /// </summary>
    public static IServiceCollection AddNotificationHubJwtBearer(
        this IServiceCollection services, IConfiguration config)
    {
        services.Configure<JwtBearerAuthOptions>(config.GetSection(JwtBearerAuthOptions.SectionName));
        services.Configure<JwtTokenOptions>(config.GetSection(JwtTokenOptions.SectionName));
        services.Configure<SuperAdminSeedOptions>(config.GetSection(SuperAdminSeedOptions.SectionName));

        services.AddScoped<ITenantContext, JwtTenantContext>();
        services.AddScoped<JwtSecurityContextFactory>();
        services.AddScoped<ISecurityContext>(sp => sp.GetRequiredService<JwtSecurityContextFactory>().Create());
        services.AddScoped<AccountAuthService>();

        var local = config.GetSection(JwtTokenOptions.SectionName).Get<JwtTokenOptions>() ?? new JwtTokenOptions();
        var remote = config.GetSection(JwtBearerAuthOptions.SectionName).Get<JwtBearerAuthOptions>()
                     ?? new JwtBearerAuthOptions();

        var auth = services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        });

        if (!string.IsNullOrWhiteSpace(remote.Authority) && remote.Enabled)
        {
            auth.AddJwtBearer(o =>
            {
                o.Authority = remote.Authority;
                o.Audience = remote.Audience;
                o.RequireHttpsMetadata = remote.RequireHttpsMetadata;
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
        }
        else
        {
            var keyBytes = Encoding.UTF8.GetBytes(local.SigningKey);
            if (keyBytes.Length < 32)
                keyBytes = System.Security.Cryptography.SHA256.HashData(keyBytes);

            auth.AddJwtBearer(o =>
            {
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = local.Issuer,
                    ValidateAudience = true,
                    ValidAudience = local.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                    NameClaimType = "sub",
                    RoleClaimType = "role",
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });
        }

        services.AddAuthorization();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

        return services;
    }
}
