using NotificationHub.Core.RateLimiting;

namespace NotificationHub.Host.Middleware;

/// <summary>
/// Rate limits human auth-sensitive routes (login, register, refresh, invite, switch, logout, sessions).
/// Does not affect machine API Key traffic.
/// </summary>
public sealed class AuthRateLimitMiddleware(RequestDelegate next, ILogger<AuthRateLimitMiddleware> log)
{
    static readonly string[] SensitivePrefixes =
    [
        "/api/v1/auth/login",
        "/api/v1/auth/register",
        "/api/v1/auth/refresh",
        "/api/v1/auth/invitations",
        "/api/v1/auth/organizations/switch",
        "/api/v1/auth/logout",
        "/api/v1/auth/sessions"
    ];

    public async Task InvokeAsync(HttpContext context, IRateLimiter rateLimiter, IConfiguration config)
    {
        var path = context.Request.Path.Value ?? "";
        if (!SensitivePrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await next(context);
            return;
        }

        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var isCredentialEndpoint =
            path.StartsWith("/api/v1/auth/login", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/v1/auth/register", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/v1/auth/refresh", StringComparison.OrdinalIgnoreCase);

        var limit = isCredentialEndpoint
            ? config.GetValue("RateLimiting:AuthLoginPerMinute", 10)
            : config.GetValue("RateLimiting:AuthSensitivePerMinute", 20);
        var key = isCredentialEndpoint ? $"auth-login:ip:{ip}" : $"auth-sensitive:ip:{ip}";

        if (!await rateLimiter.IsAllowedAsync(key, limit, context.RequestAborted))
        {
            log.LogWarning("Auth rate limit exceeded for {IP} on {Path}", ip, path);
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.RetryAfter = "60";
            await context.Response.WriteAsJsonAsync(new { error = "Too many requests" });
            return;
        }

        await next(context);
    }
}
