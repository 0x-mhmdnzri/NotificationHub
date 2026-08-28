namespace NotificationHub.Domain.Common;

/// <summary>Commits the ambient persistence transaction (aggregates + outbox rows).</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
