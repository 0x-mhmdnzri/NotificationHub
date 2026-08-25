using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Store;

public interface INotificationStatusStore
{
    Task SaveAsync(NotificationStatus status, CancellationToken ct = default);
    Task<NotificationStatus?> GetAsync(Guid notificationId, CancellationToken ct = default);
    Task<NotificationStatus?> GetByIdempotencyKeyAsync(string idempotencyKey, string? tenantId = null, CancellationToken ct = default);
    Task UpdateStatusAsync(Guid notificationId, DeliveryStatus status, string? providerMessageId = null, string? errorCode = null, string? errorMessage = null, int? attemptCount = null, CancellationToken ct = default);
}
