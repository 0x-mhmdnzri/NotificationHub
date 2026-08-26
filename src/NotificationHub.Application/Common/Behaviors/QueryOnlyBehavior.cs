using MediatR;
using Microsoft.Extensions.Logging;
using NotificationHub.Application.Common.Interfaces;

namespace NotificationHub.Application.Common.Behaviors;

/// <summary>
/// Runs only for read-side (IQuery) — no writes, no transactions.
/// </summary>
public sealed class QueryOnlyBehavior<TRequest, TResponse>(ILogger<QueryOnlyBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var isQuery = typeof(TRequest).GetInterfaces().Any(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQuery<>));

        if (!isQuery)
            return await next();

        logger.LogDebug("Query pipeline engaged for {Request}", typeof(TRequest).Name);
        return await next();
    }
}
