using NotificationHub.Abstractions.Models;
using Hangfire.Dashboard;
using NotificationHub.Core.Security;

namespace NotificationHub.Host.Hangfire;

/// <summary>
/// Protects /hangfire with the same API keys as the REST API.
/// Accepts: header X-Api-Key, Authorization: Bearer/ApiKey, or query ?api_key=
/// Requires Admin role (or HangfireMessaging:DashboardRequireAdmin=false for any valid key).
/// </summary>
public sealed class HangfireApiKeyAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();
        var env = http.RequestServices.GetService<IHostEnvironment>();
        var config = http.RequestServices.GetService<IConfiguration>();

        // Explicit opt-out only in Development when configured
        var allowAnonymousDev = config?.GetValue("HangfireMessaging:DashboardAllowAnonymousInDevelopment", false) ?? false;
        if (allowAnonymousDev && env?.IsDevelopment() == true)
            return true;

        var key = ExtractKey(http);
        if (string.IsNullOrWhiteSpace(key))
            return false;

        var validator = http.RequestServices.GetService<IApiKeyValidator>();
        if (validator is null)
            return false;

        // Hangfire filter is sync; block on validation (dashboard requests are rare).
        var auth = validator.ValidateAsync(key, http.RequestAborted)
            .ConfigureAwait(false).GetAwaiter().GetResult();

        if (auth is null)
            return false;

        var requireAdmin = config?.GetValue("HangfireMessaging:DashboardRequireAdmin", true) ?? true;
        if (requireAdmin && !auth.Roles.Contains(AppRoles.Admin, StringComparer.OrdinalIgnoreCase))
            return false;

        http.Items["Auth"] = auth;
        return true;
    }

    private static string? ExtractKey(HttpContext http)
    {
        if (http.Request.Headers.TryGetValue("X-Api-Key", out var xApi) && !string.IsNullOrWhiteSpace(xApi))
            return xApi.ToString();

        var auth = http.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(auth))
        {
            if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return auth["Bearer ".Length..].Trim();
            if (auth.StartsWith("ApiKey ", StringComparison.OrdinalIgnoreCase))
                return auth["ApiKey ".Length..].Trim();
            return auth.Trim();
        }

        if (http.Request.Query.TryGetValue("api_key", out var q) && !string.IsNullOrWhiteSpace(q))
            return q.ToString();

        return null;
    }
}
