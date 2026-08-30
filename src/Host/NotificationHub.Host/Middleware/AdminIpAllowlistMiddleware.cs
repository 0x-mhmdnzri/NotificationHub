using System.Net;

namespace NotificationHub.Host.Middleware;

/// <summary>
/// Optional IP allowlist for /api/v1/admin/* (SEC-14).
/// When Auth:AdminIpAllowlist is empty, all IPs are allowed (role check still applies).
/// </summary>
public sealed class AdminIpAllowlistMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AdminIpAllowlistMiddleware> _logger;
    private readonly HashSet<string> _allowed;

    public AdminIpAllowlistMiddleware(RequestDelegate next, IConfiguration config, ILogger<AdminIpAllowlistMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        var raw = config.GetSection("Auth:AdminIpAllowlist").Get<string[]>()
                  ?? config["Auth:AdminIpAllowlist"]?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                  ?? [];
        _allowed = new HashSet<string>(raw.Select(Normalize), StringComparer.OrdinalIgnoreCase);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";
        if (_allowed.Count > 0 && path.StartsWith("/api/v1/admin", StringComparison.OrdinalIgnoreCase))
        {
            var ip = context.Connection.RemoteIpAddress;
            var candidate = NormalizeIp(ip);
            if (candidate is null || !_allowed.Contains(candidate))
            {
                _logger.LogWarning("Admin IP blocked: {IP} Path={Path}", candidate ?? "unknown", path);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = "Admin access denied from this network location" });
                return;
            }
        }

        await _next(context);
    }

    private static string Normalize(string s) => NormalizeIp(IPAddress.TryParse(s.Trim(), out var ip) ? ip : null) ?? s.Trim();

    private static string? NormalizeIp(IPAddress? ip)
    {
        if (ip is null)
            return null;
        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();
        return ip.ToString();
    }
}
