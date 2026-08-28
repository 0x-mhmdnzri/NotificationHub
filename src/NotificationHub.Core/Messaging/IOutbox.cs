using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Messaging;

/// <summary>Transactional outbox for reliable publish (SRP).</summary>
public interface IOutbox
{
    /// <summary>Stages outbox row; returns message id for Hangfire dispatch after COMMIT.</summary>
    Task<Guid> AddAsync(NotificationRequest request, CancellationToken ct = default);
}

public interface IInbox
{
    Task<bool> ExistsAsync(string messageId, CancellationToken ct = default);
    Task<bool> TryMarkProcessedAsync(string messageId, CancellationToken ct = default);
}
