namespace NotificationHub.Core.Messaging;

public sealed class MessagingHealthOptions
{
    public const string SectionName = "MessagingHealth";
    /// <summary>Warn when oldest pending outbox row is older than this many seconds.</summary>
    public int OutboxPendingAgeWarningSeconds { get; set; } = 60;
    /// <summary>Warn when pending outbox count exceeds this.</summary>
    public int OutboxPendingCountWarning { get; set; } = 100;
    /// <summary>Warn when DLQ depth exceeds this.</summary>
    public int DlqDepthWarning { get; set; } = 1;
    public int PollIntervalSeconds { get; set; } = 30;
}

public sealed record MessagingHealthSnapshot
{
    public int OutboxPendingCount { get; init; }
    public int OutboxFailedCount { get; init; }
    public double? OldestPendingAgeSeconds { get; init; }
    public uint? WorkQueueDepth { get; init; }
    public uint? DlqDepth { get; init; }
    public ushort ConfiguredPrefetchCount { get; init; }
    public bool OutboxLagWarning { get; init; }
    public bool DlqWarning { get; init; }
    public IReadOnlyList<string> Alerts { get; init; } = [];
    public DateTimeOffset CheckedAt { get; init; } = DateTimeOffset.UtcNow;
}
