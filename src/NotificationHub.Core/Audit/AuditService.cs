using NotificationHub.Core.Persistence;

namespace NotificationHub.Core.Audit;

public sealed class AuditService : IAuditService
{
    private readonly NotificationDbContext _db;
    public AuditService(NotificationDbContext db) => _db = db;

    public async Task LogAsync(string action, Guid? notificationId = null, string? tenantId = null, string? actor = null, string? details = null, CancellationToken ct = default)
    {
        _db.AuditEntries.Add(new AuditEntryEntity
        {
            Action = action, NotificationId = notificationId, TenantId = tenantId, Actor = actor, Details = details
        });
        await _db.SaveChangesAsync(ct);
    }
}
