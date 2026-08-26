using MediatR;
using Microsoft.Extensions.Logging;
using NotificationHub.Application.Common.Interfaces;

namespace NotificationHub.Application.Common.Behaviors;

/// <summary>
/// Runs only for write-side (ICommand) requests — keeps query pipeline free of write concerns.
/// </summary>
public sealed class CommandOnlyBehavior<TRequest, TResponse>(ILogger<CommandOnlyBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var isCommand = typeof(ICommand).IsAssignableFrom(typeof(TRequest))
            || typeof(TRequest).GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>));

        if (!isCommand)
            return await next();

        logger.LogDebug("Command pipeline engaged for {Request}", typeof(TRequest).Name);
        // Future: unit-of-work begin/commit, outbox flush hooks
        return await next();
    }
}
