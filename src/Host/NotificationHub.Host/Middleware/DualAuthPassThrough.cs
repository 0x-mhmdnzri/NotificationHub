namespace NotificationHub.Host.Middleware;

/// <summary>
/// Allows JWT Bearer human routes without X-Api-Key.
/// Does not validate the JWT (JwtBearer middleware does). Does not change API Key validation for machine clients.
/// </summary>
public static class DualAuthPassThrough
{
    public static bool ShouldSkipApiKey(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // Human identity surface
        if (path.StartsWith("/api/v1/auth", StringComparison.OrdinalIgnoreCase))
            return true;

        // Bearer present → let JWT auth handle (admin routes later)
        var auth = context.Request.Headers.Authorization.ToString();
        if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
