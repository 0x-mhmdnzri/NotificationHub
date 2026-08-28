using System.Net;
using System.Text.Json;
using FluentValidation;
using NotificationHub.Application.Abstractions;

namespace NotificationHub.Host.Middleware;

/// <summary>
/// Top-of-pipeline exception handler (SEC-15). Must run early so Auth/CORS failures are also catchable when thrown downstream.
/// </summary>
public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger,
    IHostEnvironment env)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // client disconnected — no response
        }
        catch (ValidationException vex)
        {
            await WriteJsonAsync(context, StatusCodes.Status400BadRequest, new
            {
                error = "validation_failed",
                details = vex.Errors.Select(e => new { e.PropertyName, e.ErrorMessage }),
                correlationId = context.GetCorrelationId() ?? "unknown"
            });
        }
        catch (AuthorizationException authEx)
        {
            await WriteJsonAsync(context, authEx.StatusCode, new
            {
                error = authEx.Code,
                message = authEx.Message,
                correlationId = context.GetCorrelationId() ?? "unknown"
            });
        }
        catch (Exception ex)
        {
            var correlationId = context.GetCorrelationId() ?? "unknown";
            logger.LogError(ex, "Unhandled exception CorrelationId={CorrelationId} Path={Path}",
                correlationId, context.Request.Path);

            object body = env.IsDevelopment()
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

            await WriteJsonAsync(context, (int)HttpStatusCode.InternalServerError, body);
        }
    }

    private static async Task WriteJsonAsync(HttpContext context, int statusCode, object body)
    {
        if (context.Response.HasStarted)
            throw new InvalidOperationException("The response has already started; cannot write error payload.");

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOpts));
    }
}
