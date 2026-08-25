using System.Collections.Concurrent;
using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Store;

public sealed class InMemoryNotificationStatusStore : INotificationStatusStore
{
    private readonly ConcurrentDictionary<Guid, NotificationStatus> _byId = new();
    private readonly ConcurrentDictionary<string, Guid> _byIdempotency = new();

    public Task SaveAsync(NotificationStatus status, CancellationToken ct = default)
    {
        _byId[status.NotificationId] = status;
        if (!string.IsNullOrEmpty(status.IdempotencyKey))
        {
            var key = BuildIdempotencyKey(status.IdempotencyKey, status.TenantId);
            _byIdempotency[key] = status.NotificationId;
        }
        return Task.CompletedTask;
    }

    public Task<NotificationStatus?> GetAsync(Guid notificationId, CancellationToken ct = default)
    {
        _byId.TryGetValue(notificationId, out var status);
        return Task.FromResult(status);
    }

    public Task<NotificationStatus?> GetByIdempotencyKeyAsync(string idempotencyKey, string? tenantId = null, CancellationToken ct = default)
    {
        var key = BuildIdempotencyKey(idempotencyKey, tenantId);
        if (_byIdempotency.TryGetValue(key, out var id) && _byId.TryGetValue(id, out var status))
            return Task.FromResult<NotificationStatus?>(status);
        return Task.FromResult<NotificationStatus?>(null);
    }

    public Task UpdateStatusAsync(Guid notificationId, DeliveryStatus status, string? providerMessageId = null, string? errorCode = null, string? errorMessage = null, int? attemptCount = null, CancellationToken ct = default)
    {
        if (_byId.TryGetValue(notificationId, out var existing))
        {
            var updated = existing with
            {
                Status = status,
                ProviderMessageId = providerMessageId ?? existing.ProviderMessageId,
                ErrorCode = errorCode ?? existing.ErrorCode,
                ErrorMessage = errorMessage ?? existing.ErrorMessage,
                AttemptCount = attemptCount ?? existing.AttemptCount,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _byId[notificationId] = updated;
        }
        return Task.CompletedTask;
    }

    private static string BuildIdempotencyKey(string key, string? tenantId) => $"{tenantId ?? "global"}:{key}";
}
