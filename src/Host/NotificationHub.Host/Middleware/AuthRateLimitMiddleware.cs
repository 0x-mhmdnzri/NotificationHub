using NotificationHub.Core.RateLimiting;

namespace NotificationHub.Host.Middleware;

/// <summary>
/// Rate limits human auth-sensitive routes (invite, switch, logout, sessions).
/// Does not affect machine API Key traffic.
/// </summary>
public sealed class AuthRateLimitMiddleware(RequestDelegate next, ILogger<AuthRateLimitMiddleware> log)
{
    static readonly string[] SensitivePrefixes =
    [
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
        var limit = config.GetValue("RateLimiting:AuthSensitivePerMinute", 20);
        var key = $"auth-sensitive:ip:{ip}";

        if (!await rateLimiter.IsAllowedAsync(key, limit, context.RequestAborted))
        {
            log.LogWarning("Auth sensitive rate limit exceeded for {IP} on {Path}", ip, path);
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.Response.WriteAsJsonAsync(new { error = "Too many requests" });
            return;
        }

        await next(context);
    }
}
