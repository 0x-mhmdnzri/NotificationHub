namespace NotificationHub.Abstractions.Models;

public sealed record ProviderHealthSnapshot
{
    public required string ProviderId { get; init; }
    public required string Channel { get; init; }
    public double SuccessRate { get; init; }
    public int TotalSamples { get; init; }
    public int SuccessCount { get; init; }
    public int FailureCount { get; init; }
    public bool IsHealthy { get; init; }
    public DateTimeOffset? LastSuccessAt { get; init; }
    public DateTimeOffset? LastFailureAt { get; init; }
    public string? LastErrorCode { get; init; }
}

public sealed class ProviderHealthOptions
{
    public const string SectionName = "ProviderHealth";
    /// <summary>Minimum samples before health influences routing.</summary>
    public int MinSamples { get; set; } = 5;
    /// <summary>Success rate below this is considered unhealthy (0-1).</summary>
    public double UnhealthyThreshold { get; set; } = 0.5;
    /// <summary>Sliding window size for recent outcomes.</summary>
    public int WindowSize { get; set; } = 50;
    /// <summary>If true, unhealthy providers are deprioritized but still tried last when fallback is allowed.</summary>
    public bool DeprioritizeUnhealthy { get; set; } = true;
}
