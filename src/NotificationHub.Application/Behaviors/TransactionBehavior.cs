using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NotificationHub.Application.Abstractions;
using NotificationHub.Core.Persistence;

namespace NotificationHub.Application.Behaviors;

/// <summary>
/// Wraps ITransactional handlers in a single DB transaction.
/// DbContext is resolved only when the request is transactional (keeps non-DB unit tests light).
/// </summary>
public sealed class TransactionBehavior<TRequest, TResponse>(
    IServiceProvider services,
    ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not ITransactional)
            return await next();

        var db = services.GetRequiredService<NotificationDbContext>();

        if (db.Database.CurrentTransaction is not null)
            return await next();

        logger.LogDebug("Beginning transaction for {Request}", typeof(TRequest).Name);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var response = await next();
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            logger.LogDebug("Committed transaction for {Request}", typeof(TRequest).Name);
            return response;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            logger.LogWarning("Rolled back transaction for {Request}", typeof(TRequest).Name);
            throw;
        }
    }
}
