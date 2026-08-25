using Microsoft.EntityFrameworkCore;
using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Persistence;

public sealed class NotificationStatusEntity
{
    public Guid Id { get; set; }
    public string Channel { get; set; } = "";
    public string Recipient { get; set; } = "";
    public DeliveryStatus Status { get; set; }
    public string? ProviderId { get; set; }
    public string? ProviderMessageId { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? ScheduledAt { get; set; }
    public string? TenantId { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? CorrelationId { get; set; }
    public string? Category { get; set; }
    public string? PayloadJson { get; set; }
    public decimal? Cost { get; set; }

    public NotificationStatus ToModel() => new()
    {
        NotificationId = Id, Channel = Channel, Recipient = Recipient, Status = Status,
        ProviderId = ProviderId, ProviderMessageId = ProviderMessageId, ErrorCode = ErrorCode,
        ErrorMessage = ErrorMessage, AttemptCount = AttemptCount, CreatedAt = CreatedAt,
        UpdatedAt = UpdatedAt, ScheduledAt = ScheduledAt, TenantId = TenantId,
        IdempotencyKey = IdempotencyKey, CorrelationId = CorrelationId, Category = Category
    };
}

public sealed class UserPreferenceEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = "";
    public string? TenantId { get; set; }
    public string ChannelOptInJson { get; set; } = "{}";
    public string CategoryOptInJson { get; set; } = "{}";
    public string? PreferredChannel { get; set; }
    public string? QuietHoursStart { get; set; }
    public string? QuietHoursEnd { get; set; }
    public string? TimeZoneId { get; set; }
    public int? MaxPerDay { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AuditEntryEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Action { get; set; } = "";
    public Guid? NotificationId { get; set; }
    public string? TenantId { get; set; }
    public string? Actor { get; set; }
    public string? Details { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class WebhookSubscriptionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Url { get; set; } = "";
    public string? Secret { get; set; }
    public string EventsJson { get; set; } = "[]";
    public string? TenantId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class WorkflowDefinitionEntity
{
    public Guid Id { get; set; }
    public string Key { get; set; } = "";
    public string? TenantId { get; set; }
    public bool IsActive { get; set; } = true;
    public string StepsJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class WorkflowRunEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkflowId { get; set; }
    public string WorkflowKey { get; set; } = "";
    public string Recipient { get; set; } = "";
    public string? TenantId { get; set; }
    public string Status { get; set; } = "running"; // running | completed | failed | cancelled
    public string? CurrentStepId { get; set; }
    public string DataJson { get; set; } = "{}";
    public DateTimeOffset? ContinueAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? LastError { get; set; }
}

public sealed class SegmentDefinitionEntity
{
    public Guid Id { get; set; }
    public string Key { get; set; } = "";
    public string? TenantId { get; set; }
    public bool MatchAll { get; set; } = true;
    public string RulesJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class InAppMessageEntity
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = "";
    public string? TenantId { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}


public sealed class TemplateEntity
{
    public Guid Id { get; set; }
    public string Key { get; set; } = "";
    public string Channel { get; set; } = "";
    public string Locale { get; set; } = "en";
    public string? TenantId { get; set; }
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public string? HtmlBody { get; set; }
    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}


public sealed class WorkflowTimelineEventEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RunId { get; set; }
    public string EventType { get; set; } = "";
    public string? StepId { get; set; }
    public string? Message { get; set; }
    public string? DataJson { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}


public sealed class ApiKeyEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string KeyHash { get; set; } = "";
    public string? TenantId { get; set; }
    public string RolesJson { get; set; } = "[]";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
}


public sealed class ConsentLedgerEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SubjectId { get; set; } = "";
    public string? TenantId { get; set; }
    public string Purpose { get; set; } = "";
    public string? Channel { get; set; }
    public bool Granted { get; set; }
    public string Source { get; set; } = "api";
    public string? Actor { get; set; }
    public string? Evidence { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}


public sealed class EngagementEventEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? NotificationId { get; set; }
    public string? TenantId { get; set; }
    public string EventType { get; set; } = "";
    public string? Recipient { get; set; }
    public string? Channel { get; set; }
    public string? Url { get; set; }
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
    public string? ProviderId { get; set; }
    public string? MetadataJson { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}


public sealed class OutboxMessageEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid NotificationId { get; set; }
    public string PayloadJson { get; set; } = "";
    public string Status { get; set; } = "pending"; // pending | published | failed
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
}

public sealed class InboxMessageEntity
{
    public string MessageId { get; set; } = ""; // PK - notification id or broker message id
    public DateTimeOffset ProcessedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options) { }

    public DbSet<NotificationStatusEntity> NotificationStatuses => Set<NotificationStatusEntity>();
    public DbSet<UserPreferenceEntity> UserPreferences => Set<UserPreferenceEntity>();
    public DbSet<AuditEntryEntity> AuditEntries => Set<AuditEntryEntity>();
    public DbSet<WebhookSubscriptionEntity> WebhookSubscriptions => Set<WebhookSubscriptionEntity>();
    public DbSet<WorkflowDefinitionEntity> Workflows => Set<WorkflowDefinitionEntity>();
    public DbSet<WorkflowRunEntity> WorkflowRuns => Set<WorkflowRunEntity>();
    public DbSet<WorkflowTimelineEventEntity> WorkflowTimelineEvents => Set<WorkflowTimelineEventEntity>();
    public DbSet<SegmentDefinitionEntity> Segments => Set<SegmentDefinitionEntity>();
    public DbSet<InAppMessageEntity> InAppMessages => Set<InAppMessageEntity>();
    public DbSet<TemplateEntity> Templates => Set<TemplateEntity>();
    public DbSet<ApiKeyEntity> ApiKeys => Set<ApiKeyEntity>();
    public DbSet<ConsentLedgerEntity> ConsentLedger => Set<ConsentLedgerEntity>();
    public DbSet<EngagementEventEntity> EngagementEvents => Set<EngagementEventEntity>();
    public DbSet<OutboxMessageEntity> OutboxMessages => Set<OutboxMessageEntity>();
    public DbSet<InboxMessageEntity> InboxMessages => Set<InboxMessageEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var n = modelBuilder.Entity<NotificationStatusEntity>();
        n.ToTable("notification_statuses");
        n.HasKey(x => x.Id);
        n.Property(x => x.Channel).HasMaxLength(64).IsRequired();
        n.Property(x => x.Recipient).HasMaxLength(512).IsRequired();
        n.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        n.Property(x => x.ProviderId).HasMaxLength(64);
        n.Property(x => x.ProviderMessageId).HasMaxLength(256);
        n.Property(x => x.ErrorCode).HasMaxLength(128);
        n.Property(x => x.ErrorMessage).HasMaxLength(2000);
        n.Property(x => x.TenantId).HasMaxLength(128);
        n.Property(x => x.IdempotencyKey).HasMaxLength(256);
        n.Property(x => x.CorrelationId).HasMaxLength(128);
        n.Property(x => x.Category).HasMaxLength(128);
        n.Property(x => x.Cost).HasPrecision(18, 6);
        n.HasIndex(x => x.IdempotencyKey);
        n.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique().HasFilter("\"IdempotencyKey\" IS NOT NULL");
        n.HasIndex(x => x.Status);
        n.HasIndex(x => x.ScheduledAt);
        n.HasIndex(x => x.CreatedAt);
        n.HasIndex(x => x.ProviderId);

        var p = modelBuilder.Entity<UserPreferenceEntity>();
        p.ToTable("user_preferences");
        p.HasKey(x => x.Id);
        p.Property(x => x.UserId).HasMaxLength(256).IsRequired();
        p.Property(x => x.TenantId).HasMaxLength(128);
        p.HasIndex(x => new { x.TenantId, x.UserId }).IsUnique();

        var a = modelBuilder.Entity<AuditEntryEntity>();
        a.ToTable("audit_entries");
        a.HasKey(x => x.Id);
        a.Property(x => x.Action).HasMaxLength(64).IsRequired();
        a.HasIndex(x => x.NotificationId);
        a.HasIndex(x => x.CreatedAt);

        var w = modelBuilder.Entity<WebhookSubscriptionEntity>();
        w.ToTable("webhook_subscriptions");
        w.HasKey(x => x.Id);
        w.Property(x => x.Url).HasMaxLength(2048).IsRequired();
        w.HasIndex(x => x.TenantId);

        var wf = modelBuilder.Entity<WorkflowDefinitionEntity>();
        wf.ToTable("workflows");
        wf.HasKey(x => x.Id);
        wf.Property(x => x.Key).HasMaxLength(128).IsRequired();
        wf.HasIndex(x => new { x.TenantId, x.Key }).IsUnique();

        var wr = modelBuilder.Entity<WorkflowRunEntity>();
        wr.ToTable("workflow_runs");
        wr.HasKey(x => x.Id);
        wr.Property(x => x.WorkflowKey).HasMaxLength(128);
        wr.Property(x => x.Recipient).HasMaxLength(512);
        wr.Property(x => x.Status).HasMaxLength(32);
        wr.HasIndex(x => x.Status);
        wr.Property(x => x.LastError).HasMaxLength(2000);
        wr.HasIndex(x => x.ContinueAt);

        var wte = modelBuilder.Entity<WorkflowTimelineEventEntity>();
        wte.ToTable("workflow_timeline_events");
        wte.HasKey(x => x.Id);
        wte.Property(x => x.EventType).HasMaxLength(64).IsRequired();
        wte.Property(x => x.StepId).HasMaxLength(128);
        wte.Property(x => x.Message).HasMaxLength(2000);
        wte.HasIndex(x => x.RunId);
        wte.HasIndex(x => x.OccurredAt);

        var s = modelBuilder.Entity<SegmentDefinitionEntity>();
        s.ToTable("segments");
        s.HasKey(x => x.Id);
        s.Property(x => x.Key).HasMaxLength(128).IsRequired();
        s.HasIndex(x => new { x.TenantId, x.Key }).IsUnique();

        var i = modelBuilder.Entity<InAppMessageEntity>();
        i.ToTable("in_app_messages");
        i.HasKey(x => x.Id);
        i.Property(x => x.UserId).HasMaxLength(256).IsRequired();
        i.Property(x => x.Title).HasMaxLength(512);
        i.HasIndex(x => new { x.UserId, x.IsRead });
        i.HasIndex(x => x.CreatedAt);

        var t = modelBuilder.Entity<TemplateEntity>();
        t.ToTable("templates");
        t.HasKey(x => x.Id);
        t.Property(x => x.Key).HasMaxLength(128).IsRequired();
        t.Property(x => x.Channel).HasMaxLength(64).IsRequired();
        t.Property(x => x.Locale).HasMaxLength(16).IsRequired();
        t.Property(x => x.TenantId).HasMaxLength(128);
        t.Property(x => x.Subject).HasMaxLength(512).IsRequired();
        t.Property(x => x.Body).IsRequired();
        t.HasIndex(x => new { x.TenantId, x.Key, x.Channel, x.Locale }).IsUnique();
        t.HasIndex(x => x.IsActive);

        var ak = modelBuilder.Entity<ApiKeyEntity>();
        ak.ToTable("api_keys");
        ak.HasKey(x => x.Id);
        ak.Property(x => x.Name).HasMaxLength(128).IsRequired();
        ak.Property(x => x.KeyHash).HasMaxLength(128).IsRequired();
        ak.Property(x => x.TenantId).HasMaxLength(128);
        ak.Property(x => x.RolesJson).HasMaxLength(512).IsRequired();
        ak.HasIndex(x => x.KeyHash).IsUnique();
        ak.HasIndex(x => x.TenantId);
        ak.HasIndex(x => x.IsActive);

        var cl = modelBuilder.Entity<ConsentLedgerEntity>();
        cl.ToTable("consent_ledger");
        cl.HasKey(x => x.Id);
        cl.Property(x => x.SubjectId).HasMaxLength(256).IsRequired();
        cl.Property(x => x.TenantId).HasMaxLength(128);
        cl.Property(x => x.Purpose).HasMaxLength(128).IsRequired();
        cl.Property(x => x.Channel).HasMaxLength(64);
        cl.Property(x => x.Source).HasMaxLength(64);
        cl.Property(x => x.Actor).HasMaxLength(256);
        cl.Property(x => x.Evidence).HasMaxLength(2000);
        cl.HasIndex(x => new { x.TenantId, x.SubjectId, x.Purpose, x.Channel, x.OccurredAt });
        cl.HasIndex(x => x.OccurredAt);

        var ee = modelBuilder.Entity<EngagementEventEntity>();
        ee.ToTable("engagement_events");
        ee.HasKey(x => x.Id);
        ee.Property(x => x.EventType).HasMaxLength(32).IsRequired();
        ee.Property(x => x.TenantId).HasMaxLength(128);
        ee.Property(x => x.Recipient).HasMaxLength(512);
        ee.Property(x => x.Channel).HasMaxLength(64);
        ee.Property(x => x.Url).HasMaxLength(2048);
        ee.Property(x => x.UserAgent).HasMaxLength(512);
        ee.Property(x => x.IpAddress).HasMaxLength(64);
        ee.Property(x => x.ProviderId).HasMaxLength(64);
        ee.HasIndex(x => x.NotificationId);
        ee.HasIndex(x => x.EventType);
        ee.HasIndex(x => x.OccurredAt);
        ee.HasIndex(x => new { x.TenantId, x.OccurredAt });

        var ob = modelBuilder.Entity<OutboxMessageEntity>();
        ob.ToTable("outbox_messages");
        ob.HasKey(x => x.Id);
        ob.Property(x => x.Status).HasMaxLength(32).IsRequired();
        ob.Property(x => x.PayloadJson).IsRequired();
        ob.Property(x => x.LastError).HasMaxLength(2000);
        ob.HasIndex(x => x.Status);
        ob.HasIndex(x => x.NextAttemptAt);
        ob.HasIndex(x => x.NotificationId);

        var ib = modelBuilder.Entity<InboxMessageEntity>();
        ib.ToTable("inbox_messages");
        ib.HasKey(x => x.MessageId);
        ib.Property(x => x.MessageId).HasMaxLength(128);
        ib.HasIndex(x => x.ProcessedAt);
    }
}
