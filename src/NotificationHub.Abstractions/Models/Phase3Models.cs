namespace NotificationHub.Abstractions.Models;

public sealed record WorkflowDefinition
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Key { get; init; }
    public string? TenantId { get; init; }
    public bool IsActive { get; init; } = true;
    public List<WorkflowStep> Steps { get; init; } = [];
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record WorkflowStep
{
    public required string Id { get; init; }
    public required string Type { get; init; } // send | delay | condition | branch
    public string? Channel { get; init; }
    public string? TemplateKey { get; init; }
    public string? PreferredProvider { get; init; }
    public int? DelaySeconds { get; init; }
    public string? ConditionExpression { get; init; } // simple: data.key == value
    public string? NextOnTrue { get; init; }
    public string? NextOnFalse { get; init; }
    public string? Next { get; init; }
}

public sealed record WorkflowStartRequest
{
    public required string WorkflowKey { get; init; }
    public required string Recipient { get; init; }
    public string? TenantId { get; init; }
    public string? Locale { get; init; } = "en";
    public Dictionary<string, object?> Data { get; init; } = new();
    public string? CorrelationId { get; init; }
}

public sealed record SegmentDefinition
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Key { get; init; }
    public string? TenantId { get; init; }
    public List<SegmentRule> Rules { get; init; } = [];
    public bool MatchAll { get; init; } = true;
}

public sealed record SegmentRule
{
    public required string Field { get; init; }
    public required string Operator { get; init; } // eq | neq | contains | in
    public required string Value { get; init; }
}

public sealed record AnalyticsSummary
{
    public long Total { get; init; }
    public long Queued { get; init; }
    public long Sent { get; init; }
    public long Failed { get; init; }
    public long DeadLetter { get; init; }
    public long Suppressed { get; init; }
    public long Scheduled { get; init; }
    public double DeliveryRate { get; init; }
    public double FailureRate { get; init; }
    public Dictionary<string, long> ByChannel { get; init; } = new();
    public Dictionary<string, long> ByProvider { get; init; } = new();
    public decimal EstimatedCost { get; init; }
}

public sealed record ProviderCostConfig
{
    public string ProviderId { get; init; } = "";
    public decimal CostPerMessage { get; init; }
    public string Currency { get; init; } = "IRR";
}

public sealed record InAppMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string UserId { get; init; }
    public string? TenantId { get; init; }
    public required string Title { get; init; }
    public required string Body { get; init; }
    public bool IsRead { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record ComplianceExport
{
    public required string UserId { get; init; }
    public string? TenantId { get; init; }
    public UserPreference? Preference { get; init; }
    public IReadOnlyList<NotificationStatus> Notifications { get; init; } = [];
    public IReadOnlyList<InAppMessage> InAppMessages { get; init; } = [];
    public IReadOnlyList<ConsentRecord> Consents { get; init; } = [];
    public DateTimeOffset ExportedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record WorkflowRunStatusDto
{
    public Guid RunId { get; init; }
    public Guid WorkflowId { get; init; }
    public string WorkflowKey { get; init; } = "";
    public string Recipient { get; init; } = "";
    public string? TenantId { get; init; }
    public string Status { get; init; } = "";
    public string? CurrentStepId { get; init; }
    public DateTimeOffset? ContinueAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public string? LastError { get; init; }
}

public sealed record WorkflowTimelineEventDto
{
    public Guid Id { get; init; }
    public Guid RunId { get; init; }
    public string EventType { get; init; } = ""; // started | step_entered | step_completed | delayed | branched | sent | failed | completed | cancelled
    public string? StepId { get; init; }
    public string? Message { get; init; }
    public string? DataJson { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
}
