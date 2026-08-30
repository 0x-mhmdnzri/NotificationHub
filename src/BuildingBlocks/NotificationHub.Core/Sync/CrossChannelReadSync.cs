using Microsoft.EntityFrameworkCore;
using NotificationHub.Core.Persistence;

namespace NotificationHub.Core.Sync;

public sealed class CrossChannelReadSync : ICrossChannelReadSync
{
    private readonly NotificationDbContext _db;
    public CrossChannelReadSync(NotificationDbContext db) => _db = db;

    public async Task<int> SyncReadAsync(Guid notificationId, string? userId, string? tenantId, CancellationToken ct = default)
    {
        var q = _db.InAppMessages.Where(x => x.NotificationId == notificationId && !x.IsRead);
        if (!string.IsNullOrEmpty(userId))
            q = q.Where(x => x.UserId == userId);
        if (!string.IsNullOrEmpty(tenantId))
            q = q.Where(x => x.TenantId == tenantId);
        var rows = await q.ToListAsync(ct);
        foreach (var r in rows)
            r.IsRead = true;
        if (rows.Count > 0)
            await _db.SaveChangesAsync(ct);
        return rows.Count;
    }
}
