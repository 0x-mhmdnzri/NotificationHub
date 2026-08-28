using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace NotificationHub.Core.Persistence;

public static class BroadcastSchema
{
    public static async Task EnsureAsync(NotificationDbContext db, ILogger? logger = null, CancellationToken ct = default)
    {
        // Idempotent DDL for environments without full migrations applied yet
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "BroadcastCampaigns" (
                "Id" uuid PRIMARY KEY,
                "Name" character varying(256) NOT NULL,
                "TenantId" character varying(128),
                "Status" integer NOT NULL,
                "TemplateKey" character varying(128) NOT NULL,
                "ChannelsJson" text NOT NULL,
                "DataJson" text,
                "ScheduledAtUtc" timestamptz,
                "CreatedAtUtc" timestamptz NOT NULL,
                "StartedAtUtc" timestamptz,
                "CompletedAtUtc" timestamptz,
                "CreatedBy" character varying(256)
            );
            CREATE INDEX IF NOT EXISTS "IX_BroadcastCampaigns_Tenant_Status" ON "BroadcastCampaigns" ("TenantId", "Status");
            CREATE INDEX IF NOT EXISTS "IX_BroadcastCampaigns_Created" ON "BroadcastCampaigns" ("CreatedAtUtc");

            CREATE TABLE IF NOT EXISTS "BroadcastRecipients" (
                "Id" uuid PRIMARY KEY,
                "CampaignId" uuid NOT NULL,
                "Address" character varying(320) NOT NULL,
                "Channel" character varying(64) NOT NULL,
                "Status" integer NOT NULL,
                "Attempts" integer NOT NULL DEFAULT 0,
                "NotificationId" uuid,
                "ErrorCode" character varying(64),
                "ErrorMessage" character varying(1024),
                "CreatedAtUtc" timestamptz NOT NULL,
                "ProcessedAtUtc" timestamptz,
                "ContentHash" character varying(128) NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_BroadcastRecipients_Idempotency" ON "BroadcastRecipients" ("ContentHash");
            CREATE INDEX IF NOT EXISTS "IX_BroadcastRecipients_Worker" ON "BroadcastRecipients" ("CampaignId", "Status", "CreatedAtUtc");
            """, ct);
        logger?.LogInformation("Broadcast schema ensured");
    }
}
