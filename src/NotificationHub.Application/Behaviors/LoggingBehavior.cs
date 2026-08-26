using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var name = typeof(TRequest).Name;
        var side = IsCommand(typeof(TRequest)) ? "CMD" : IsQuery(typeof(TRequest)) ? "QRY" : "REQ";
        logger.LogInformation("[{Side}] {Name} started", side, name);
        var sw = Stopwatch.StartNew();
        try
        {
            var response = await next();
            sw.Stop();
            logger.LogInformation("[{Side}] {Name} completed in {Ms}ms", side, name, sw.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex, "[{Side}] {Name} failed after {Ms}ms", side, name, sw.ElapsedMilliseconds);
            throw;
        }
    }

    private static bool IsCommand(Type t) =>
        typeof(ICommand).IsAssignableFrom(t) ||
        t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>));

    private static bool IsQuery(Type t) =>
        t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQuery<>));
}
