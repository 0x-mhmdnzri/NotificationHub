using NotificationHub.Abstractions.Models;
using NotificationHub.Core.RateLimiting;
using NotificationHub.Core.Security;

namespace NotificationHub.Host.Middleware;

public sealed class ApiKeyAuthMiddleware
{
    public const string AuthContextItem = "AuthContext";
    private const string ApiKeyHeader = "X-Api-Key";

    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyAuthMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ApiKeyAuthMiddleware(RequestDelegate next, ILogger<ApiKeyAuthMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context, IApiKeyValidator validator, IRateLimiter rateLimiter, IConfiguration config)
    {
        var path = context.Request.Path.Value ?? "";

        // SEC-27: swagger/openapi only unauthenticated in Development
        if (path.StartsWith("/health") || path.StartsWith("/t/"))
        {
            await _next(context);
            return;
        }

        if ((path.StartsWith("/swagger") || path.StartsWith("/openapi")) && _env.IsDevelopment())
        {
            await _next(context);
            return;
        }

        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var authFailLimit = config.GetValue("RateLimiting:AuthFailuresPerMinute", 30);

        if (!context.Request.Headers.TryGetValue(ApiKeyHeader, out var providedKey) ||
            string.IsNullOrWhiteSpace(providedKey))
        {
            // SEC-23: only count failures
            if (!await rateLimiter.IsAllowedAsync($"auth-fail:ip:{ip}", authFailLimit, context.RequestAborted))
            {
                _logger.LogWarning("Auth failure rate limit exceeded for {IP}", ip);
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.Response.WriteAsJsonAsync(new { error = "Too many authentication attempts" });
                return;
            }

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Missing X-Api-Key header" });
            return;
        }

        var auth = await validator.ValidateAsync(providedKey.ToString(), context.RequestAborted);
        if (auth is null)
        {
            if (!await rateLimiter.IsAllowedAsync($"auth-fail:ip:{ip}", authFailLimit, context.RequestAborted))
            {
                _logger.LogWarning("Auth failure rate limit exceeded for {IP}", ip);
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.Response.WriteAsJsonAsync(new { error = "Too many authentication attempts" });
                return;
            }

            _logger.LogWarning("Unauthorized request from {IP}", ip);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid or expired API key" });
            return;
        }

        context.Items[AuthContextItem] = auth;
        await _next(context);
    }
}

public static class AuthContextExtensions
{
    public static AuthContext? GetAuthContext(this HttpContext http)
        => http.Items.TryGetValue(ApiKeyAuthMiddleware.AuthContextItem, out var v) ? v as AuthContext : null;

    public static IResult? RequireRoles(this HttpContext http, params string[] roles)
    {
        var auth = http.GetAuthContext();
        if (auth is null)
            return Results.Unauthorized();
        if (auth.IsAdmin)
            return null;
        if (roles.Length == 0)
            return null;
        if (auth.HasAnyRole(roles))
            return null;
        return Results.Json(new { error = "Insufficient role" }, statusCode: StatusCodes.Status403Forbidden);
    }

    public static string? ResolveTenantId(this HttpContext http, string? requestedTenantId)
    {
        var auth = http.GetAuthContext()!;
        if (auth.IsAdmin)
            return requestedTenantId ?? auth.TenantId;
        return auth.TenantId;
    }

    public static bool CanAccessTenant(this HttpContext http, string? resourceTenantId)
    {
        var auth = http.GetAuthContext();
        if (auth is null)
            return false;
        if (auth.IsAdmin)
            return true;
        if (string.IsNullOrEmpty(auth.TenantId))
            return string.IsNullOrEmpty(resourceTenantId);
        return string.Equals(auth.TenantId, resourceTenantId, StringComparison.Ordinal);
    }
}
