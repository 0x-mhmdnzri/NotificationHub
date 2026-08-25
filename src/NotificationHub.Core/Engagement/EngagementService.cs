using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Persistence;
using NotificationHub.Core.Store;

namespace NotificationHub.Core.Engagement;

public sealed class EngagementService : IEngagementService
{
    private readonly NotificationDbContext _db;
    private readonly INotificationStatusStore _statusStore;
    private readonly ILogger<EngagementService> _logger;

    public EngagementService(
        NotificationDbContext db,
        INotificationStatusStore statusStore,
        ILogger<EngagementService> logger)
    {
        _db = db;
        _statusStore = statusStore;
        _logger = logger;
    }

    public async Task<EngagementEvent> TrackAsync(EngagementEvent evt, CancellationToken ct = default)
    {
        var type = evt.EventType.Trim().ToLowerInvariant();
        var entity = new EngagementEventEntity
        {
            Id = evt.Id == Guid.Empty ? Guid.NewGuid() : evt.Id,
            NotificationId = evt.NotificationId,
            TenantId = evt.TenantId,
            EventType = type,
            Recipient = evt.Recipient,
            Channel = evt.Channel,
            Url = evt.Url,
            UserAgent = evt.UserAgent,
            IpAddress = evt.IpAddress,
            ProviderId = evt.ProviderId,
            MetadataJson = evt.MetadataJson,
            OccurredAt = evt.OccurredAt == default ? DateTimeOffset.UtcNow : evt.OccurredAt
        };

        // Enrich from notification status when possible
        if (entity.NotificationId.HasValue)
        {
            var status = await _statusStore.GetAsync(entity.NotificationId.Value, ct);
            if (status is not null)
            {
                entity.TenantId ??= status.TenantId;
                entity.Recipient ??= status.Recipient;
                entity.Channel ??= status.Channel;
                entity.ProviderId ??= status.ProviderId;
            }
        }

        _db.EngagementEvents.Add(entity);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Engagement {Type} for notification {NotificationId}", type, entity.NotificationId);
        return ToModel(entity);
    }

    public async Task<IReadOnlyList<EngagementEvent>> ListByNotificationAsync(Guid notificationId, CancellationToken ct = default)
    {
        var rows = await _db.EngagementEvents.AsNoTracking()
            .Where(x => x.NotificationId == notificationId)
            .OrderBy(x => x.OccurredAt)
            .ToListAsync(ct);
        return rows.Select(ToModel).ToList();
    }

    public async Task<(long Opens, long Clicks)> CountAsync(DateTimeOffset? from, DateTimeOffset? to, string? tenantId, CancellationToken ct = default)
    {
        var q = _db.EngagementEvents.AsNoTracking().AsQueryable();
        if (from.HasValue) q = q.Where(x => x.OccurredAt >= from);
        if (to.HasValue) q = q.Where(x => x.OccurredAt <= to);
        if (!string.IsNullOrEmpty(tenantId)) q = q.Where(x => x.TenantId == tenantId);

        var opens = await q.CountAsync(x => x.EventType == EngagementEventTypes.Open, ct);
        var clicks = await q.CountAsync(x => x.EventType == EngagementEventTypes.Click, ct);
        return (opens, clicks);
    }

    private static EngagementEvent ToModel(EngagementEventEntity e) => new()
    {
        Id = e.Id, NotificationId = e.NotificationId, TenantId = e.TenantId, EventType = e.EventType,
        Recipient = e.Recipient, Channel = e.Channel, Url = e.Url, UserAgent = e.UserAgent,
        IpAddress = e.IpAddress, ProviderId = e.ProviderId, MetadataJson = e.MetadataJson, OccurredAt = e.OccurredAt
    };
}
