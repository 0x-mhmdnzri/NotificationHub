using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace NotificationHub.Core.Persistence;

/// <summary>
/// Ensures Phase-1 tables/columns exist without a full EF migration toolchain in CI sandboxes.
/// Safe to run repeatedly (IF NOT EXISTS / ADD COLUMN IF NOT EXISTS).
/// </summary>
public static class Phase1Schema
{
    public static async Task EnsureAsync(NotificationDbContext db, ILogger? logger = null, CancellationToken ct = default)
    {
        // InMemory provider used in tests — skip raw SQL
        if (db.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true)
            return;

        try
        {
            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE in_app_messages ADD COLUMN IF NOT EXISTS "IsArchived" boolean NOT NULL DEFAULT false;
                ALTER TABLE in_app_messages ADD COLUMN IF NOT EXISTS "NotificationId" uuid NULL;
                ALTER TABLE in_app_messages ADD COLUMN IF NOT EXISTS "Category" varchar(128) NULL;
                ALTER TABLE in_app_messages ADD COLUMN IF NOT EXISTS "ActionUrl" varchar(2048) NULL;

                CREATE TABLE IF NOT EXISTS digest_policies (
                    "Id" uuid PRIMARY KEY,
                    "Key" varchar(128) NOT NULL,
                    "TenantId" varchar(128) NULL,
                    "WindowMinutes" int NOT NULL DEFAULT 60,
                    "Channel" varchar(64) NOT NULL DEFAULT 'email',
                    "TemplateKey" varchar(128) NOT NULL DEFAULT 'digest-default',
                    "IsActive" boolean NOT NULL DEFAULT true
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ix_digest_policies_key_tenant ON digest_policies ("Key", "TenantId");

                CREATE TABLE IF NOT EXISTS digest_buffers (
                    "Id" uuid PRIMARY KEY,
                    "PolicyKey" varchar(128) NOT NULL,
                    "Recipient" varchar(512) NOT NULL,
                    "TenantId" varchar(128) NULL,
                    "PayloadJson" text NOT NULL,
                    "CreatedAt" timestamptz NOT NULL,
                    "FlushedAt" timestamptz NULL
                );
                CREATE INDEX IF NOT EXISTS ix_digest_buffers_flush ON digest_buffers ("PolicyKey", "Recipient", "FlushedAt");

                CREATE TABLE IF NOT EXISTS throttle_policies (
                    "Id" uuid PRIMARY KEY,
                    "Key" varchar(128) NOT NULL,
                    "TenantId" varchar(128) NULL,
                    "Channel" varchar(64) NULL,
                    "MaxCount" int NOT NULL DEFAULT 10,
                    "WindowMinutes" int NOT NULL DEFAULT 60,
                    "IsActive" boolean NOT NULL DEFAULT true
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ix_throttle_policies_key_tenant ON throttle_policies ("Key", "TenantId");

                CREATE TABLE IF NOT EXISTS throttle_counters (
                    "Id" uuid PRIMARY KEY,
                    "PolicyKey" varchar(128) NOT NULL,
                    "Recipient" varchar(512) NOT NULL,
                    "TenantId" varchar(128) NULL,
                    "Channel" varchar(64) NULL,
                    "WindowStart" timestamptz NOT NULL,
                    "Count" int NOT NULL DEFAULT 0
                );
                CREATE INDEX IF NOT EXISTS ix_throttle_counters_window ON throttle_counters ("PolicyKey", "Recipient", "WindowStart");

                CREATE TABLE IF NOT EXISTS topics (
                    "Id" uuid PRIMARY KEY,
                    "Key" varchar(128) NOT NULL,
                    "Name" varchar(256) NULL,
                    "TenantId" varchar(128) NULL,
                    "IsActive" boolean NOT NULL DEFAULT true
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ix_topics_key_tenant ON topics ("Key", "TenantId");

                CREATE TABLE IF NOT EXISTS topic_subscribers (
                    "Id" uuid PRIMARY KEY,
                    "TopicKey" varchar(128) NOT NULL,
                    "SubscriberId" varchar(256) NOT NULL,
                    "TenantId" varchar(128) NULL,
                    "Channel" varchar(64) NULL,
                    "Address" varchar(512) NULL,
                    "CreatedAt" timestamptz NOT NULL
                );
                CREATE INDEX IF NOT EXISTS ix_topic_subscribers ON topic_subscribers ("TopicKey", "SubscriberId", "TenantId");

                CREATE TABLE IF NOT EXISTS device_tokens (
                    "Id" uuid PRIMARY KEY,
                    "UserId" varchar(256) NOT NULL,
                    "TenantId" varchar(128) NULL,
                    "Platform" varchar(32) NOT NULL,
                    "Token" varchar(512) NOT NULL,
                    "Locale" varchar(16) NULL,
                    "IsActive" boolean NOT NULL DEFAULT true,
                    "CreatedAt" timestamptz NOT NULL,
                    "UpdatedAt" timestamptz NOT NULL
                );
                CREATE INDEX IF NOT EXISTS ix_device_tokens_user ON device_tokens ("UserId", "Platform", "Token");
                """, ct);
            logger?.LogInformation("Phase1 schema ensured");
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Phase1 schema ensure skipped or failed (non-Postgres?)");
        }
    }
}
