using NotificationHub.Domain.Delivery.ValueObjects;

namespace NotificationHub.Domain.Delivery;

public interface INotificationRepository
{
    Task<Notification?> GetAsync(NotificationId id, CancellationToken ct = default);
    Task AddAsync(Notification notification, CancellationToken ct = default);
    Task UpdateAsync(Notification notification, CancellationToken ct = default);
}
