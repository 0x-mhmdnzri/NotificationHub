using System.Text;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using NotificationHub.Core.Auth;
using NotificationHub.Core.Identity;
using NotificationHub.Host.Auth;
using NotificationHub.Host.Middleware;

namespace NotificationHub.Host.Security;

public static class JwtAuthExtensions
{
    public static IServiceCollection AddNotificationHubJwtBearer(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<JwtTokenOptions>(config.GetSection(JwtTokenOptions.SectionName));
        services.Configure<JwtBearerAuthOptions>(config.GetSection(JwtBearerAuthOptions.SectionName));
        services.Configure<SuperAdminSeedOptions>(config.GetSection(SuperAdminSeedOptions.SectionName));
        services.AddSingleton<JwtSecurityContextFactory>();
        services.AddScoped(sp => sp.GetRequiredService<JwtSecurityContextFactory>().Create());
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
                o.Events = AnonymousAuthJwtEvents();
            });
        }
        else
        {
            // MUST match AccountAuthService.IssueTokensAsync key derivation
            var keyBytes = Encoding.UTF8.GetBytes(local.SigningKey ?? string.Empty);
            if (keyBytes.Length < 32)
                keyBytes = SHA256.HashData(keyBytes.Length == 0
                    ? "NotificationHub.DevSigningKey"u8.ToArray()
                    : keyBytes);
            else if (keyBytes.Length > 64)
                keyBytes = keyBytes.AsSpan(0, 64).ToArray();

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
                o.Events = AnonymousAuthJwtEvents();
            });
        }

        services.AddAuthorization();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

        return services;
    }

    /// <summary>
    /// Never let a leftover/invalid Bearer token challenge anonymous auth endpoints.
    /// </summary>
    static JwtBearerEvents AnonymousAuthJwtEvents() => new()
    {
        OnMessageReceived = context =>
        {
            var path = context.Request.Path.Value ?? "";
            if (DualAuthPassThrough.IsAnonymousAuthPath(path))
                context.Token = null;
            return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
            var path = context.Request.Path.Value ?? "";
            if (DualAuthPassThrough.IsAnonymousAuthPath(path))
            {
                // Endpoint handles its own 401 body (invalid_credentials, etc.)
                context.HandleResponse();
            }
            return Task.CompletedTask;
        }
    };
}
