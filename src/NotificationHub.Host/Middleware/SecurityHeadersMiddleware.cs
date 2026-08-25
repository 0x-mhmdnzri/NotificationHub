namespace NotificationHub.Host.Middleware;

/// <summary>Baseline browser security headers (SEC-05).</summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["X-XSS-Protection"] = "0"; // modern browsers; rely on CSP
        headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";


        if (context.Request.Path.StartsWithSegments("/swagger"))
        {
            headers["Content-Security-Policy"] =
                "default-src 'self'; " +
                "script-src 'self' 'unsafe-inline' https://localhost:7089; " +
                "style-src 'self' 'unsafe-inline'; " +
                "img-src 'self' data:; " +
                "font-src 'self' data:; " +
                "connect-src 'self' https://localhost:7089; " +
                "frame-ancestors 'self'; " +
                "base-uri 'self'; " +
                "form-action 'self';";
        }
        else
        {
            headers["Content-Security-Policy"] =
                "default-src 'none'; " +
                "script-src 'self'; " +
                "style-src 'self'; " +
                "img-src 'self' data:; " +
                "font-src 'self'; " +
                "connect-src 'self'; " +
                "frame-ancestors 'none'; " +
                "base-uri 'none'; " +
                "form-action 'none';";
        }

        if (context.Request.IsHttps)
            headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";

        await _next(context);
    }
}