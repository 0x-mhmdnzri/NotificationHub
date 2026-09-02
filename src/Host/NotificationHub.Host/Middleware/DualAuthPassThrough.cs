namespace NotificationHub.Host.Middleware;

/// <summary>
/// Allows JWT Bearer human routes without X-Api-Key.
/// Does not validate the JWT (JwtBearer middleware does). Does not change API Key validation for machine clients.
/// </summary>
public static class DualAuthPassThrough
{
    static readonly string[] AnonymousAuthExact =
    [
        "/api/v1/auth/login",
        "/api/v1/auth/register",
        "/api/v1/auth/refresh"
    ];

    public static bool IsAnonymousAuthPath(string path)
    {
        foreach (var p in AnonymousAuthExact)
        {
            if (path.Equals(p, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public static bool ShouldSkipApiKey(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // Entire human identity surface (login/register/refresh/me/orgs/sessions/…)
        if (path.StartsWith("/api/v1/auth", StringComparison.OrdinalIgnoreCase))
            return true;

        // Bearer present → let JWT auth handle
        var auth = context.Request.Headers.Authorization.ToString();
        if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
