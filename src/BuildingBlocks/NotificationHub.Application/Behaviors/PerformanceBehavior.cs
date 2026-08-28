using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace NotificationHub.Application.Behaviors;

public sealed class PerformanceBehavior<TRequest, TResponse>(ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private const int ThresholdMs = 500;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var response = await next();
        sw.Stop();
        if (sw.ElapsedMilliseconds > ThresholdMs)
            logger.LogWarning("Slow request {Name} took {Ms}ms (threshold {Threshold}ms)",
                typeof(TRequest).Name, sw.ElapsedMilliseconds, ThresholdMs);
        return response;
    }
}
