using NotificationHub.Core.Persistence;
using NotificationHub.Domain.Common;

namespace NotificationHub.Infrastructure.Persistence;

public sealed class EfUnitOfWork(NotificationDbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
