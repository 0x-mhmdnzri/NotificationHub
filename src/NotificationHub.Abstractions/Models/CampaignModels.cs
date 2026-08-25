namespace NotificationHub.Abstractions.Models;

public sealed record BroadcastRequest
{
    public required string Name { get; init; }
    public string? TenantId { get; init; }
    public required string Channel { get; init; }
    public required string TemplateKey { get; init; }
    public string? SegmentKey { get; init; }
    public List<string>? Recipients { get; init; }
    public Dictionary<string, object?> Data { get; init; } = new();
    public string? Locale { get; init; } = "en";
}

public sealed record BroadcastResult
{
    public Guid CampaignId { get; init; }
    public int Accepted { get; init; }
    public int Failed { get; init; }
    public string Status { get; init; } = "completed";
}
