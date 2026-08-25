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

public sealed class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options) { }

    public DbSet<NotificationStatusEntity> NotificationStatuses => Set<NotificationStatusEntity>();
    public DbSet<UserPreferenceEntity> UserPreferences => Set<UserPreferenceEntity>();
    public DbSet<AuditEntryEntity> AuditEntries => Set<AuditEntryEntity>();
    public DbSet<WebhookSubscriptionEntity> WebhookSubscriptions => Set<WebhookSubscriptionEntity>();

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
        n.HasIndex(x => x.IdempotencyKey);
        n.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique().HasFilter("\"IdempotencyKey\" IS NOT NULL");
        n.HasIndex(x => x.Status);
        n.HasIndex(x => x.ScheduledAt);
        n.HasIndex(x => x.CreatedAt);

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
    }
}
