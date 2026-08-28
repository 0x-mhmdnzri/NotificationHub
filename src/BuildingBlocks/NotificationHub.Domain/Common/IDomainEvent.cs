namespace NotificationHub.Domain.Common;

/// <summary>Something meaningful that happened in the domain (past tense).</summary>
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredAtUtc { get; }
}
