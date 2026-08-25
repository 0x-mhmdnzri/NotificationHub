namespace NotificationHub.Abstractions.Models;

public sealed record LayoutDefinition
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Key { get; init; }
    public string? TenantId { get; init; }
    public required string Html { get; init; } // must contain {{content}}
    public string? Description { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed record PartialDefinition
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Key { get; init; }
    public string? TenantId { get; init; }
    public required string Body { get; init; }
    public bool IsActive { get; init; } = true;
}
