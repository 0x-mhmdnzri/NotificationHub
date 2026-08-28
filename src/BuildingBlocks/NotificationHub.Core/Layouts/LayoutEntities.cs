namespace NotificationHub.Core.Layouts;

public sealed class LayoutEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = "";
    public string? TenantId { get; set; }
    public string Html { get; set; } = "";
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class PartialEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = "";
    public string? TenantId { get; set; }
    public string Body { get; set; } = "";
    public bool IsActive { get; set; } = true;
}
