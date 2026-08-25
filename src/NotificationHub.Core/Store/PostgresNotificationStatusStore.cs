using Microsoft.EntityFrameworkCore;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Persistence;

namespace NotificationHub.Core.Store;

public sealed class PostgresNotificationStatusStore : INotificationStatusStore
{
    private readonly NotificationDbContext _db;

    public PostgresNotificationStatusStore(NotificationDbContext db)
    {
        _db = db;
    }

    public async Task SaveAsync(NotificationStatus status, CancellationToken ct = default)
    {
        var entity = NotificationStatusEntity.FromModel(status);
        _db.NotificationStatuses.Add(entity);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<NotificationStatus?> GetAsync(Guid notificationId, CancellationToken ct = default)
    {
        var entity = await _db.NotificationStatuses.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == notificationId, ct);
        return entity?.ToModel();
    }

    public async Task<NotificationStatus?> GetByIdempotencyKeyAsync(string idempotencyKey, string? tenantId = null, CancellationToken ct = default)
    {
        var query = _db.NotificationStatuses.AsNoTracking()
            .Where(x => x.IdempotencyKey == idempotencyKey);

        query = tenantId is null
            ? query.Where(x => x.TenantId == null)
            : query.Where(x => x.TenantId == tenantId);

        var entity = await query.FirstOrDefaultAsync(ct);
        return entity?.ToModel();
    }

    public async Task UpdateStatusAsync(Guid notificationId, DeliveryStatus status, string? providerMessageId = null, string? errorCode = null, string? errorMessage = null, int? attemptCount = null, CancellationToken ct = default)
    {
        var entity = await _db.NotificationStatuses.FirstOrDefaultAsync(x => x.Id == notificationId, ct);
        if (entity is null) return;

        entity.Status = status;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        if (providerMessageId is not null) entity.ProviderMessageId = providerMessageId;
        if (errorCode is not null) entity.ErrorCode = errorCode;
        if (errorMessage is not null) entity.ErrorMessage = errorMessage;
        if (attemptCount is not null) entity.AttemptCount = attemptCount.Value;

        await _db.SaveChangesAsync(ct);
    }
}
