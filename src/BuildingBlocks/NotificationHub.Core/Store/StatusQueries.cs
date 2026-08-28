using Microsoft.EntityFrameworkCore;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Persistence;

namespace NotificationHub.Core.Store;

internal static class StatusQueries
{
    public static readonly Func<NotificationDbContext, string, string?, Task<NotificationStatusEntity?>> ByIdempotency =
        EF.CompileAsyncQuery((NotificationDbContext db, string key, string? tenantId) =>
            db.NotificationStatuses.AsNoTracking()
                .Where(x => x.IdempotencyKey == key && x.TenantId == tenantId)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefault());

    public static readonly Func<NotificationDbContext, string, string, string?, DateTimeOffset, Task<NotificationStatusEntity?>> ByCollapse =
        EF.CompileAsyncQuery((NotificationDbContext db, string collapseKey, string recipient, string? tenantId, DateTimeOffset since) =>
            db.NotificationStatuses.AsNoTracking()
                .Where(x => x.CollapseKey == collapseKey
                            && x.Recipient == recipient
                            && x.CreatedAt >= since
                            && x.Status != DeliveryStatus.Cancelled
                            && x.Status != DeliveryStatus.Suppressed
                            && x.Status != DeliveryStatus.DeadLetter
                            && x.TenantId == tenantId)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefault());
}
