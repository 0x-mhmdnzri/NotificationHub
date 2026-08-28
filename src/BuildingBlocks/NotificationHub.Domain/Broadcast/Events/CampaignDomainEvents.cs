using NotificationHub.Domain.Broadcast.ValueObjects;
using NotificationHub.Domain.Common;

namespace NotificationHub.Domain.Broadcast.Events;

public sealed record CampaignCreated(
    CampaignId CampaignId,
    string Name,
    string? TenantId,
    DateTimeOffset OccurredAtUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
}

public sealed record CampaignScheduled(
    CampaignId CampaignId,
    DateTimeOffset ScheduledAtUtc,
    DateTimeOffset OccurredAtUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
}

public sealed record CampaignStarted(
    CampaignId CampaignId,
    CampaignStatus Status,
    DateTimeOffset OccurredAtUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
}

public sealed record CampaignCancelled(
    CampaignId CampaignId,
    DateTimeOffset OccurredAtUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
}

public sealed record CampaignCompleted(
    CampaignId CampaignId,
    CampaignStatus FinalStatus,
    long Total,
    long Sent,
    long Failed,
    DateTimeOffset OccurredAtUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
}
