using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Persistence;

namespace NotificationHub.Core.Store;

public sealed class PostgresNotificationStatusStore : INotificationStatusStore
{
    private readonly NotificationDbContext _db;
    public PostgresNotificationStatusStore(NotificationDbContext db) => _db = db;

    public Task SaveAsync(NotificationStatus status, CancellationToken ct = default)
    {
        _db.NotificationStatuses.Add(new NotificationStatusEntity
        {
            Id = status.NotificationId, Channel = status.Channel, Recipient = status.Recipient,
            Status = status.Status, ProviderId = status.ProviderId, ProviderMessageId = status.ProviderMessageId,
            ErrorCode = status.ErrorCode, ErrorMessage = status.ErrorMessage, AttemptCount = status.AttemptCount,
            CreatedAt = status.CreatedAt == default ? DateTimeOffset.UtcNow : status.CreatedAt,
            UpdatedAt = status.UpdatedAt == default ? DateTimeOffset.UtcNow : status.UpdatedAt,
            ScheduledAt = status.ScheduledAt,
            TenantId = status.TenantId, IdempotencyKey = status.IdempotencyKey, CollapseKey = status.CollapseKey,
            CorrelationId = status.CorrelationId, Category = status.Category
        });
        // Staged only — caller commits with outbox via shared DbContext / IUnitOfWork (transactional outbox).
        return Task.CompletedTask;
    }

    public async Task<NotificationStatus?> GetAsync(Guid notificationId, CancellationToken ct = default)
    {
        var e = await _db.NotificationStatuses.AsNoTracking().FirstOrDefaultAsync(x => x.Id == notificationId, ct);
        return e?.ToModel();
    }

    public async Task<NotificationStatus?> GetByIdempotencyKeyAsync(string key, string? tenantId = null, CancellationToken ct = default)
    {
        var e = await StatusQueries.ByIdempotency(_db, key, tenantId);
        return e?.ToModel();
    }

    public async Task UpdateStatusAsync(Guid notificationId, DeliveryStatus status, string? providerMessageId = null,
        string? errorCode = null, string? errorMessage = null, int? attemptCount = null, CancellationToken ct = default)
    {
        var e = await _db.NotificationStatuses.FirstOrDefaultAsync(x => x.Id == notificationId, ct);
        if (e is null) return;
        e.Status = status;
        e.UpdatedAt = DateTimeOffset.UtcNow;
        if (providerMessageId is not null) e.ProviderMessageId = providerMessageId;
        if (errorCode is not null) e.ErrorCode = errorCode;
        if (errorMessage is not null) e.ErrorMessage = errorMessage;
        if (attemptCount is not null) e.AttemptCount = attemptCount.Value;
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateProviderAsync(Guid notificationId, string? providerId, CancellationToken ct = default)
    {
        var e = await _db.NotificationStatuses.FirstOrDefaultAsync(x => x.Id == notificationId, ct);
        if (e is null) return;
        e.ProviderId = providerId;
        e.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task SavePayloadAsync(Guid notificationId, string payloadJson, CancellationToken ct = default)
    {
        var e = await _db.NotificationStatuses.FirstOrDefaultAsync(x => x.Id == notificationId, ct);
        if (e is null) return;
        e.PayloadJson = payloadJson;
        await _db.SaveChangesAsync(ct);
    }

    
    public async Task<NotificationStatus?> FindByCollapseKeyAsync(string collapseKey, string recipient, string? tenantId = null, CancellationToken ct = default)
    {
        var since = DateTimeOffset.UtcNow.AddHours(-24);
        var e = await StatusQueries.ByCollapse(_db, collapseKey, recipient, tenantId, since);
        return e?.ToModel();
    }

    public async Task<List<NotificationStatusEntity>> GetDueScheduledAsync(DateTimeOffset now, int take = 50, CancellationToken ct = default)
    {
        return await _db.NotificationStatuses
            .Where(x => x.Status == DeliveryStatus.Scheduled && x.ScheduledAt != null && x.ScheduledAt <= now)
            .OrderBy(x => x.ScheduledAt)
            .Take(take)
            .ToListAsync(ct);
    }
}
