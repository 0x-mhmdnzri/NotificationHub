using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using NotificationHub.Application.Common.Interfaces;

namespace NotificationHub.Application.Common.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var name = typeof(TRequest).Name;
        var side = typeof(TRequest).GetInterfaces().Any(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>))
            || typeof(ICommand).IsAssignableFrom(typeof(TRequest))
            ? "CMD" : "QRY";

        logger.LogInformation("[{Side}] Handling {Request}", side, name);
        var sw = Stopwatch.StartNew();
        try
        {
            var response = await next();
            sw.Stop();
            if (sw.ElapsedMilliseconds > 500)
                logger.LogWarning("[{Side}] {Request} slow: {Ms}ms", side, name, sw.ElapsedMilliseconds);
            else
                logger.LogDebug("[{Side}] {Request} {Ms}ms", side, name, sw.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex, "[{Side}] {Request} failed after {Ms}ms", side, name, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
