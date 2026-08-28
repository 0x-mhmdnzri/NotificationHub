namespace NotificationHub.Core.Cdp;

public sealed class CdpProfileEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = "";
    public string? TenantId { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string TraitsJson { get; set; } = "{}";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CdpEventEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = "";
    public string? TenantId { get; set; }
    public string EventName { get; set; } = "";
    public string PropertiesJson { get; set; } = "{}";
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}
