using MediatR;
using Microsoft.Extensions.Logging;

namespace NotificationHub.Application.Behaviors;

/// <summary>
/// Placeholder for application-level authorization hooks.
/// Tenant/resource checks remain in handlers using trusted auth context from the API adapter.
/// </summary>
public sealed class AuthorizationBehavior<TRequest, TResponse>(ILogger<AuthorizationBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        logger.LogTrace("Authorization pipeline for {Request}", typeof(TRequest).Name);
        return await next();
    }
}
