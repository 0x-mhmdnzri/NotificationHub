using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Common;
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

    public async Task<EngagementEvent?> TrackAsync(EngagementEvent evt, bool requireExistingNotification = true, CancellationToken ct = default)
    {
        var type = evt.EventType.Trim().ToLowerInvariant();

        NotificationStatus? status = null;
        if (evt.NotificationId.HasValue)
        {
            status = await _statusStore.GetAsync(evt.NotificationId.Value, ct);
            if (requireExistingNotification && status is null)
            {
                _logger.LogDebug("Skipping engagement {Type}: notification {Id} not found", type, evt.NotificationId);
                return null;
            }
        }
        else if (requireExistingNotification)
        {
            return null;
        }

        var entity = new EngagementEventEntity
        {
            Id = ServerIds.New(),
            NotificationId = evt.NotificationId,
            TenantId = evt.TenantId ?? status?.TenantId,
            EventType = type,
            Recipient = evt.Recipient ?? status?.Recipient,
            Channel = evt.Channel ?? status?.Channel ?? "email",
            Url = evt.Url,
            UserAgent = evt.UserAgent,
            IpAddress = evt.IpAddress,
            ProviderId = evt.ProviderId ?? status?.ProviderId,
            MetadataJson = evt.MetadataJson,
            OccurredAt = evt.OccurredAt == default ? DateTimeOffset.UtcNow : evt.OccurredAt
        };

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
        if (from.HasValue)
            q = q.Where(x => x.OccurredAt >= from);
        if (to.HasValue)
            q = q.Where(x => x.OccurredAt <= to);
        if (!string.IsNullOrEmpty(tenantId))
            q = q.Where(x => x.TenantId == tenantId);

        var opens = await q.CountAsync(x => x.EventType == EngagementEventTypes.Open, ct);
        var clicks = await q.CountAsync(x => x.EventType == EngagementEventTypes.Click, ct);
        return (opens, clicks);
    }

    private static EngagementEvent ToModel(EngagementEventEntity e) => new()
    {
        Id = e.Id,
        NotificationId = e.NotificationId,
        TenantId = e.TenantId,
        EventType = e.EventType,
        Recipient = e.Recipient,
        Channel = e.Channel,
        Url = e.Url,
        UserAgent = e.UserAgent,
        IpAddress = e.IpAddress,
        ProviderId = e.ProviderId,
        MetadataJson = e.MetadataJson,
        OccurredAt = e.OccurredAt
    };
}
