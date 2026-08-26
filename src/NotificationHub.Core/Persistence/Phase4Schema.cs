using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace NotificationHub.Core.Persistence;

public static class Phase4Schema
{
    public static async Task EnsureAsync(NotificationDbContext db, ILogger? logger = null, CancellationToken ct = default)
    {
        if (db.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true)
            return;
        try
        {
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS cdp_profiles (
                    "Id" uuid PRIMARY KEY,
                    "UserId" varchar(256) NOT NULL,
                    "TenantId" varchar(128) NULL,
                    "Email" varchar(512) NULL,
                    "Phone" varchar(64) NULL,
                    "TraitsJson" text NOT NULL DEFAULT '{{}}',
                    "UpdatedAt" timestamptz NOT NULL
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ix_cdp_profiles_user ON cdp_profiles ("UserId", "TenantId");

                CREATE TABLE IF NOT EXISTS cdp_events (
                    "Id" uuid PRIMARY KEY,
                    "UserId" varchar(256) NOT NULL,
                    "TenantId" varchar(128) NULL,
                    "EventName" varchar(128) NOT NULL,
                    "PropertiesJson" text NOT NULL,
                    "OccurredAt" timestamptz NOT NULL
                );
                CREATE INDEX IF NOT EXISTS ix_cdp_events_user ON cdp_events ("UserId", "OccurredAt");

                CREATE TABLE IF NOT EXISTS localization_entries (
                    "Id" uuid PRIMARY KEY,
                    "Key" varchar(256) NOT NULL,
                    "Locale" varchar(16) NOT NULL,
                    "TenantId" varchar(128) NULL,
                    "Value" text NOT NULL
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ix_localization_key ON localization_entries ("Key", "Locale", "TenantId");
                """, ct);
            logger?.LogInformation("Phase4 schema ensured");
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Phase4 schema ensure failed");
        }
    }
}
