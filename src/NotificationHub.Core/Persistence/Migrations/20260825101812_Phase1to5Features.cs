using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotificationHub.Core.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase1to5Features : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent SQL aligned with Phase1/2/4Schema.EnsureAsync (safe on existing DBs)
            migrationBuilder.Sql("""

                ALTER TABLE user_preferences ADD COLUMN IF NOT EXISTS "WeeklyScheduleJson" text NULL;
                ALTER TABLE notification_statuses ADD COLUMN IF NOT EXISTS "CollapseKey" varchar(256) NULL;
                CREATE INDEX IF NOT EXISTS "IX_notification_statuses_Recipient_CollapseKey" ON notification_statuses ("Recipient", "CollapseKey");


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

                CREATE TABLE IF NOT EXISTS cdp_profiles (
                                    "Id" uuid PRIMARY KEY,
                                    "UserId" varchar(256) NOT NULL,
                                    "TenantId" varchar(128) NULL,
                                    "Email" varchar(512) NULL,
                                    "Phone" varchar(64) NULL,
                                    "TraitsJson" text NOT NULL DEFAULT '{}',
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
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_notification_statuses_Recipient_CollapseKey";
                DROP TABLE IF EXISTS localization_entries;
                DROP TABLE IF EXISTS cdp_events;
                DROP TABLE IF EXISTS cdp_profiles;
                DROP TABLE IF EXISTS partials;
                DROP TABLE IF EXISTS layouts;
                DROP TABLE IF EXISTS device_tokens;
                DROP TABLE IF EXISTS topic_subscribers;
                DROP TABLE IF EXISTS topics;
                DROP TABLE IF EXISTS throttle_counters;
                DROP TABLE IF EXISTS throttle_policies;
                DROP TABLE IF EXISTS digest_buffers;
                DROP TABLE IF EXISTS digest_policies;
                """);
        }
    }
}
