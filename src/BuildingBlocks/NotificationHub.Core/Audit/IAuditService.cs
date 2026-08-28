namespace NotificationHub.Core.Audit;

public interface IAuditService
{
    Task LogAsync(string action, Guid? notificationId = null, string? tenantId = null, string? actor = null, string? details = null, CancellationToken ct = default);
}
