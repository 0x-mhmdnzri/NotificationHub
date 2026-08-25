using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Security;

namespace NotificationHub.Host.Middleware;

public sealed class ApiKeyAuthMiddleware
{
    public const string AuthContextItem = "AuthContext";
    private const string ApiKeyHeader = "X-Api-Key";

    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyAuthMiddleware> _logger;

    public ApiKeyAuthMiddleware(RequestDelegate next, ILogger<ApiKeyAuthMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IApiKeyValidator validator)
    {
        var path = context.Request.Path.Value ?? "";
        if (path.StartsWith("/health") || path.StartsWith("/swagger") || path.StartsWith("/openapi"))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(ApiKeyHeader, out var providedKey) ||
            string.IsNullOrWhiteSpace(providedKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Missing X-Api-Key header" });
            return;
        }

        var auth = await validator.ValidateAsync(providedKey.ToString(), context.RequestAborted);
        if (auth is null)
        {
            _logger.LogWarning("Unauthorized request from {IP}", context.Connection.RemoteIpAddress);
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
        if (!auth.HasAnyRole(roles))
            return Results.Json(new { error = "Forbidden", required = roles }, statusCode: StatusCodes.Status403Forbidden);
        return null;
    }

    /// <summary>
    /// Non-admin keys are bound to their TenantId; body/query tenant cannot escalate.
    /// </summary>
    public static string? ResolveTenantId(this HttpContext http, string? requestedTenantId)
    {
        var auth = http.GetAuthContext();
        if (auth is null) return requestedTenantId;
        if (auth.IsAdmin) return requestedTenantId ?? auth.TenantId;
        return auth.TenantId;
    }
}
