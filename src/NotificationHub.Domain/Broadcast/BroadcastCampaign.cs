using NotificationHub.Domain.Broadcast.Events;
using NotificationHub.Domain.Broadcast.ValueObjects;
using NotificationHub.Domain.Common;
using NotificationHub.Domain.Delivery.ValueObjects;

namespace NotificationHub.Domain.Broadcast;

/// <summary>
/// Aggregate root for broadcast campaign lifecycle metadata.
/// Recipients are NOT part of this aggregate (fan-out size); they are coordinated by application/workers
/// and referenced by CampaignId. Invariant: status transitions + schedule + channel set.
/// </summary>
public sealed class BroadcastCampaign : AggregateRoot<CampaignId>
{
    private readonly List<ChannelCode> _channels = [];

    public string Name { get; private set; } = null!;
    public TenantId? TenantId { get; private set; }
    public CampaignStatus Status { get; private set; }
    public TemplateKey TemplateKey { get; private set; } = null!;
    public IReadOnlyList<ChannelCode> Channels => _channels.AsReadOnly();
    public IReadOnlyDictionary<string, string>? Data { get; private set; }
    public DateTimeOffset? ScheduledAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? StartedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public string? CreatedBy { get; private set; }

    private BroadcastCampaign() { }

    public static BroadcastCampaign Create(
        CampaignId id,
        string name,
        TemplateKey templateKey,
        IEnumerable<ChannelCode> channels,
        TenantId? tenantId,
        IReadOnlyDictionary<string, string>? data,
        DateTimeOffset? scheduledAtUtc,
        string? createdBy,
        DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Campaign name cannot be empty.");
        var ch = channels.Select(c => c).Distinct().ToList();
        if (ch.Count == 0)
            throw new DomainException("Campaign requires at least one channel.");

        var status = scheduledAtUtc is { } s && s > nowUtc
            ? CampaignStatus.Scheduled
            : CampaignStatus.Draft;

        var c = new BroadcastCampaign
        {
            Id = id,
            Name = name.Trim(),
            TenantId = tenantId,
            Status = status,
            TemplateKey = templateKey,
            Data = data,
            ScheduledAtUtc = scheduledAtUtc,
            CreatedAtUtc = nowUtc,
            CreatedBy = createdBy
        };
        c._channels.AddRange(ch);

        if (status == CampaignStatus.Scheduled && scheduledAtUtc is { } sa)
            c.Raise(new CampaignScheduled(id, sa, nowUtc));

        c.Raise(new CampaignCreated(id, c.Name, tenantId?.Value, nowUtc));
        return c;
    }

    public static BroadcastCampaign Rehydrate(
        CampaignId id,
        string name,
        TenantId? tenantId,
        CampaignStatus status,
        TemplateKey templateKey,
        IEnumerable<ChannelCode> channels,
        IReadOnlyDictionary<string, string>? data,
        DateTimeOffset? scheduledAtUtc,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? startedAtUtc,
        DateTimeOffset? completedAtUtc,
        string? createdBy)
    {
        var c = new BroadcastCampaign
        {
            Id = id,
            Name = name,
            TenantId = tenantId,
            Status = status,
            TemplateKey = templateKey,
            Data = data,
            ScheduledAtUtc = scheduledAtUtc,
            CreatedAtUtc = createdAtUtc,
            StartedAtUtc = startedAtUtc,
            CompletedAtUtc = completedAtUtc,
            CreatedBy = createdBy
        };
        c._channels.AddRange(channels);
        return c;
    }

    public void Schedule(DateTimeOffset scheduledAtUtc, DateTimeOffset nowUtc)
    {
        if (scheduledAtUtc <= nowUtc)
            throw new DomainException("Scheduled time must be in the future.");
        CampaignLifecycle.Ensure(Status, CampaignStatus.Scheduled);
        Status = CampaignStatus.Scheduled;
        ScheduledAtUtc = scheduledAtUtc;
        Raise(new CampaignScheduled(Id, scheduledAtUtc, nowUtc));
    }

    public void Start(DateTimeOffset nowUtc)
    {
        CampaignLifecycle.Ensure(Status, CampaignStatus.Preparing);
        Status = CampaignStatus.Preparing;
        StartedAtUtc = nowUtc;
        Raise(new CampaignStarted(Id, Status, nowUtc));

        CampaignLifecycle.Ensure(Status, CampaignStatus.Processing);
        Status = CampaignStatus.Processing;
    }

    public void MarkDispatching()
    {
        CampaignLifecycle.Ensure(Status, CampaignStatus.Dispatching);
        Status = CampaignStatus.Dispatching;
    }

    public void MarkDelivering()
    {
        CampaignLifecycle.Ensure(Status, CampaignStatus.Delivering);
        Status = CampaignStatus.Delivering;
    }

    public void Cancel(DateTimeOffset nowUtc)
    {
        if (CampaignLifecycle.IsTerminal(Status) && Status != CampaignStatus.Cancelled)
            throw new DomainException($"Cannot cancel campaign in terminal status {Status}.");
        CampaignLifecycle.Ensure(Status, CampaignStatus.Cancelled);
        Status = CampaignStatus.Cancelled;
        CompletedAtUtc = nowUtc;
        Raise(new CampaignCancelled(Id, nowUtc));
    }

    /// <summary>
    /// Completion uses delivery counts from outside the aggregate (recipient store / projections).
    /// </summary>
    public void CompleteWithCounts(long total, long sent, long failed, long cancelled, long skipped, DateTimeOffset nowUtc)
    {
        var next = CampaignLifecycle.ResolveCompletion(total, sent, failed, cancelled, skipped);
        if (next == CampaignStatus.Delivering)
        {
            if (Status != CampaignStatus.Delivering)
                MarkDelivering();
            return;
        }

        CampaignLifecycle.Ensure(Status, next);
        Status = next;
        CompletedAtUtc = nowUtc;
        Raise(new CampaignCompleted(Id, next, total, sent, failed, nowUtc));
    }

    public bool CanAcceptRecipients =>
        Status is CampaignStatus.Draft or CampaignStatus.Scheduled;
}
