using NotificationHub.Host.Security;

namespace NotificationHub.Host.Middleware;

/// <summary>Propagates/creates X-Correlation-ID for request tracing (SEC-19).</summary>
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemKey = "CorrelationId";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ILogger<CorrelationIdMiddleware> logger)
    {
        var incoming = context.Request.Headers[HeaderName].FirstOrDefault();
        var correlationId = string.IsNullOrWhiteSpace(incoming)
            ? Guid.NewGuid().ToString("N")
            : incoming.Trim();

        if (correlationId.Length > 64)
            correlationId = correlationId[..64];

        context.Items[ItemKey] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = LogSanitizer.Sanitize(correlationId, 64),
            ["RequestPath"] = LogSanitizer.Sanitize(context.Request.Path.Value, 256)
        }))
        {
            await _next(context);
        }
    }
}

public static class CorrelationIdExtensions
{
    public static string? GetCorrelationId(this HttpContext http)
        => http.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var v) ? v as string : null;
}
