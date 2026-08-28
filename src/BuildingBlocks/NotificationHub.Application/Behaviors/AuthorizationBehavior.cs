using System.Reflection;
using MediatR;
using Microsoft.Extensions.Logging;
using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Behaviors;

/// <summary>
/// Enforces [AuthorizeRoles] on requests using trusted IRequestContext.
/// Does not replace endpoint checks — defense in depth.
/// </summary>
public sealed class AuthorizationBehavior<TRequest, TResponse>(
    IRequestContext context,
    ILogger<AuthorizationBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var attr = typeof(TRequest).GetCustomAttribute<AuthorizeRolesAttribute>(inherit: true);
        if (attr is null)
            return await next();

        if (attr.RequireAuthenticated && !context.IsAuthenticated)
        {
            logger.LogWarning("Unauthenticated access denied for {Request}", typeof(TRequest).Name);
            throw new AuthorizationException("Authentication required.", "auth.unauthorized", 401);
        }

        if (attr.Roles.Length > 0 && !context.HasAnyRole(attr.Roles))
        {
            logger.LogWarning(
                "Role check failed for {Request}. Required={Required} Actual={Actual}",
                typeof(TRequest).Name,
                string.Join(',', attr.Roles),
                string.Join(',', context.Roles));
            throw new AuthorizationException(
                "Insufficient permissions for this operation.",
                "auth.forbidden",
                403);
        }

        return await next();
    }
}
