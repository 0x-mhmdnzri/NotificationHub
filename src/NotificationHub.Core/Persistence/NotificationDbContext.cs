using Microsoft.EntityFrameworkCore;

namespace NotificationHub.Core.Persistence;

public sealed class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options) { }

    public DbSet<NotificationStatusEntity> NotificationStatuses => Set<NotificationStatusEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<NotificationStatusEntity>();
        e.ToTable("notification_statuses");
        e.HasKey(x => x.Id);
        e.Property(x => x.Channel).HasMaxLength(64).IsRequired();
        e.Property(x => x.Recipient).HasMaxLength(512).IsRequired();
        e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        e.Property(x => x.ProviderMessageId).HasMaxLength(256);
        e.Property(x => x.ErrorCode).HasMaxLength(128);
        e.Property(x => x.ErrorMessage).HasMaxLength(2000);
        e.Property(x => x.TenantId).HasMaxLength(128);
        e.Property(x => x.IdempotencyKey).HasMaxLength(256);
        e.Property(x => x.CorrelationId).HasMaxLength(128);

        e.HasIndex(x => x.IdempotencyKey);
        e.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique()
            .HasFilter("\"IdempotencyKey\" IS NOT NULL");
        e.HasIndex(x => x.Status);
        e.HasIndex(x => x.CreatedAt);
    }
}
