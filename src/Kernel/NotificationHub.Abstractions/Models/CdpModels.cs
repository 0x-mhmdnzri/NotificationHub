namespace NotificationHub.Abstractions.Models;

public sealed record CdpIdentifyRequest
{
    public required string UserId { get; init; }
    public string? TenantId { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public Dictionary<string, object?> Traits { get; init; } = new();
}

public sealed record CdpTrackRequest
{
    public required string UserId { get; init; }
    public required string Event { get; init; }
    public string? TenantId { get; init; }
    public Dictionary<string, object?> Properties { get; init; } = new();
    /// <summary>Optional workflow key to start when event matches.</summary>
    public string? TriggerWorkflowKey { get; init; }
    public string? Channel { get; init; }
    public string? TemplateKey { get; init; }
}

public sealed record CdpProfile
{
    public required string UserId { get; init; }
    public string? TenantId { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public Dictionary<string, object?> Traits { get; init; } = new();
    public DateTimeOffset UpdatedAt { get; init; }
}
