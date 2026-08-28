using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace NotificationHub.Core.Persistence;

public static class Phase2Schema
{
    public static async Task EnsureAsync(NotificationDbContext db, ILogger? logger = null, CancellationToken ct = default)
    {
        if (db.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true)
            return;
        try
        {
            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE user_preferences ADD COLUMN IF NOT EXISTS "WeeklyScheduleJson" text NULL;
                ALTER TABLE notification_statuses ADD COLUMN IF NOT EXISTS "CollapseKey" varchar(256) NULL;
                CREATE INDEX IF NOT EXISTS ix_notification_statuses_collapse ON notification_statuses ("Recipient", "CollapseKey", "CreatedAt");

                CREATE TABLE IF NOT EXISTS layouts (
                    "Id" uuid PRIMARY KEY,
                    "Key" varchar(128) NOT NULL,
                    "TenantId" varchar(128) NULL,
                    "Html" text NOT NULL,
                    "Description" varchar(512) NULL,
                    "IsActive" boolean NOT NULL DEFAULT true
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ix_layouts_key_tenant ON layouts ("Key", "TenantId");

                CREATE TABLE IF NOT EXISTS partials (
                    "Id" uuid PRIMARY KEY,
                    "Key" varchar(128) NOT NULL,
                    "TenantId" varchar(128) NULL,
                    "Body" text NOT NULL,
                    "IsActive" boolean NOT NULL DEFAULT true
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ix_partials_key_tenant ON partials ("Key", "TenantId");
                """, ct);
            logger?.LogInformation("Phase2 schema ensured");
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Phase2 schema ensure failed");
        }
    }
}
