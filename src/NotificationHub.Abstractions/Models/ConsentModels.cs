namespace NotificationHub.Abstractions.Models;

public sealed record ConsentRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string SubjectId { get; init; } // user/recipient id
    public string? TenantId { get; init; }
    public required string Purpose { get; init; } // e.g. marketing, transactional, otp
    public string? Channel { get; init; } // optional channel scope
    public bool Granted { get; init; }
    public string Source { get; init; } = "api"; // api | import | admin | preference
    public string? Actor { get; init; }
    public string? Evidence { get; init; } // free text / reference
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record ConsentDecision
{
    public bool Allowed { get; init; }
    public string? Reason { get; init; }
    public ConsentRecord? Latest { get; init; }
}

public sealed class RetentionOptions
{
    public const string SectionName = "Retention";
    public int NotificationDays { get; set; } = 90;
    public int AuditDays { get; set; } = 180;
    public int TimelineDays { get; set; } = 90;
    public int ConsentDays { get; set; } = 730; // keep longer for legal
    public bool Enabled { get; set; } = true;
    public int SweepIntervalMinutes { get; set; } = 60;
}

public sealed record RetentionSweepResult
{
    public int NotificationsDeleted { get; init; }
    public int AuditsDeleted { get; init; }
    public int TimelineDeleted { get; init; }
    public DateTimeOffset RanAt { get; init; } = DateTimeOffset.UtcNow;
}
