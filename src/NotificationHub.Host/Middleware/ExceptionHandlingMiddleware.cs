using System.Net;
using System.Text.Json;

namespace NotificationHub.Host.Middleware;

/// <summary>Sanitized error responses — no stack traces or internal details outside Development (SEC-15).</summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // client disconnected
        }
        catch (Exception ex)
        {
            var correlationId = context.GetCorrelationId() ?? "unknown";
            _logger.LogError(ex, "Unhandled exception CorrelationId={CorrelationId} Path={Path}",
                correlationId, context.Request.Path);

            if (context.Response.HasStarted)
                throw;

            context.Response.Clear();
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";

            object body = _env.IsDevelopment()
                ? new
                {
                    error = "An unexpected error occurred",
                    correlationId,
                    detail = ex.Message,
                    type = ex.GetType().Name
                }
                : new
                {
                    error = "An unexpected error occurred",
                    correlationId
                };

            await context.Response.WriteAsync(JsonSerializer.Serialize(body));
        }
    }
}
