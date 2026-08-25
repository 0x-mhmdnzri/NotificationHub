using Microsoft.EntityFrameworkCore;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Persistence;

namespace NotificationHub.Core.Activity;

public sealed class ActivityService : IActivityService
{
    private readonly NotificationDbContext _db;
    public ActivityService(NotificationDbContext db) => _db = db;

    public async Task<IReadOnlyList<ActivityItem>> ListAsync(string? tenantId, int take = 50, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 200);
        var items = new List<ActivityItem>();

        var notifQ = _db.NotificationStatuses.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(tenantId)) notifQ = notifQ.Where(x => x.TenantId == tenantId);
        var notifs = await notifQ.OrderByDescending(x => x.UpdatedAt).Take(take).ToListAsync(ct);
        items.AddRange(notifs.Select(n => new ActivityItem
        {
            Id = n.NotificationId,
            Kind = "notification",
            Summary = $"{n.Status} {n.Channel} → {n.Recipient}",
            NotificationId = n.NotificationId,
            TenantId = n.TenantId,
            At = n.UpdatedAt,
            Meta = new Dictionary<string, string?>
            {
                ["status"] = n.Status.ToString(),
                ["channel"] = n.Channel,
                ["provider"] = n.ProviderId
            }
        }));

        var auditQ = _db.AuditEntries.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(tenantId)) auditQ = auditQ.Where(x => x.TenantId == tenantId);
        var audits = await auditQ.OrderByDescending(x => x.CreatedAt).Take(take).ToListAsync(ct);
        items.AddRange(audits.Select(a => new ActivityItem
        {
            Id = a.Id,
            Kind = "audit",
            Summary = a.Action + (a.Details is null ? "" : $": {a.Details}"),
            NotificationId = a.NotificationId,
            TenantId = a.TenantId,
            At = a.CreatedAt,
            Meta = new Dictionary<string, string?> { ["actor"] = a.Actor }
        }));

        return items.OrderByDescending(x => x.At).Take(take).ToList();
    }
}
