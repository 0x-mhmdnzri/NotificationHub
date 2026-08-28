using NotificationHub.Abstractions.Models;
using NotificationHub.Core.RateLimiting;
using NotificationHub.Core.Security;
using NotificationHub.Infrastructure.HangfireJobs;

namespace NotificationHub.Host.Hangfire;

/// <summary>
/// Rate-limits /hangfire* separately from the public API so dashboard polling
/// cannot exhaust the global API budget (or be used as a DoS vector).
/// </summary>
public sealed class HangfireDashboardRateLimitMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        IRateLimiter rateLimiter,
        IConfiguration config)
    {
        var path = context.Request.Path.Value ?? "";
        if (!path.StartsWith("/hangfire", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var limit = config.GetValue("HangfireMessaging:DashboardRateLimitPerMinute", 30);
        if (limit <= 0)
        {
            await next(context);
            return;
        }

        var key = BuildKey(context);
        if (!await rateLimiter.IsAllowedAsync(key, limit, context.RequestAborted))
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.RetryAfter = "60";
            await context.Response.WriteAsync("Hangfire dashboard rate limit exceeded. Try again later.");
            return;
        }

        await next(context);
    }

    private static string BuildKey(HttpContext context)
    {
        if (context.Items.TryGetValue("Auth", out var authObj) && authObj is AuthContext auth)
            return $"hangfire-dashboard:key:{auth.ApiKeyId}";

        // Auth filter runs as Hangfire filter, not always before middleware for static assets.
        // Fall back to API key header / IP.
        if (context.Request.Headers.TryGetValue("X-Api-Key", out var x) && !string.IsNullOrWhiteSpace(x))
            return $"hangfire-dashboard:hdr:{x}";

        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return $"hangfire-dashboard:ip:{ip}";
    }
}
