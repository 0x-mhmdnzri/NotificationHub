using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Persistence;

namespace NotificationHub.Core.Store;

public interface INotificationStatusStore
{
    Task SaveAsync(NotificationStatus status, CancellationToken ct = default);
    /// <summary>Add to DbContext without SaveChanges (transactional outbox).</summary>
    void Stage(NotificationStatus status);
    Task<NotificationStatus?> GetAsync(Guid notificationId, CancellationToken ct = default);
    Task<NotificationStatus?> GetByIdempotencyKeyAsync(string idempotencyKey, string? tenantId = null, CancellationToken ct = default);
    Task UpdateStatusAsync(Guid notificationId, DeliveryStatus status, string? providerMessageId = null, string? errorCode = null, string? errorMessage = null, int? attemptCount = null, CancellationToken ct = default);
    Task UpdateProviderAsync(Guid notificationId, string? providerId, CancellationToken ct = default);
    Task SavePayloadAsync(Guid notificationId, string payloadJson, CancellationToken ct = default);
    Task<NotificationStatus?> FindByCollapseKeyAsync(string collapseKey, string recipient, string? tenantId = null, CancellationToken ct = default);
    Task<List<NotificationStatusEntity>> GetDueScheduledAsync(DateTimeOffset now, int take = 50, CancellationToken ct = default);
}
