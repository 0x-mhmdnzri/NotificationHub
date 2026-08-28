using System.Collections.Concurrent;
using NotificationHub.Domain.Delivery;
using NotificationHub.Domain.Delivery.ValueObjects;

namespace NotificationHub.Infrastructure.DomainAdapters;

/// <summary>
/// Transitional repository for the Domain Notification aggregate.
/// Production path still uses legacy status store; this adapter enables domain handlers/tests
/// without coupling Domain to EF. Replace with EF-mapped aggregate persistence in next increment.
/// </summary>
public sealed class InMemoryNotificationRepository : INotificationRepository
{
    private readonly ConcurrentDictionary<Guid, Notification> _store = new();

    public Task<Notification?> GetAsync(NotificationId id, CancellationToken ct = default)
    {
        _store.TryGetValue(id.Value, out var n);
        return Task.FromResult(n);
    }

    public Task AddAsync(Notification notification, CancellationToken ct = default)
    {
        if (!_store.TryAdd(notification.Id.Value, notification))
            throw new InvalidOperationException($"Notification {notification.Id} already exists.");
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Notification notification, CancellationToken ct = default)
    {
        _store[notification.Id.Value] = notification;
        return Task.CompletedTask;
    }
}
