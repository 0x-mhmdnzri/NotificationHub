using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotificationHub.Core.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BroadcastCampaigns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent: BroadcastSchema.Ensure may have created these tables already
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "BroadcastCampaigns" (
                    "Id" uuid NOT NULL,
                    "Name" text NOT NULL,
                    "TenantId" text NULL,
                    "Status" integer NOT NULL,
                    "TemplateKey" text NOT NULL,
                    "ChannelsJson" text NOT NULL,
                    "DataJson" text NULL,
                    "ScheduledAtUtc" timestamp with time zone NULL,
                    "CreatedAtUtc" timestamp with time zone NOT NULL,
                    "StartedAtUtc" timestamp with time zone NULL,
                    "CompletedAtUtc" timestamp with time zone NULL,
                    "CreatedBy" text NULL,
                    CONSTRAINT "PK_BroadcastCampaigns" PRIMARY KEY ("Id")
                );
                CREATE INDEX IF NOT EXISTS "IX_BroadcastCampaigns_CreatedAtUtc" ON "BroadcastCampaigns" ("CreatedAtUtc");
                CREATE INDEX IF NOT EXISTS "IX_BroadcastCampaigns_TenantId_Status" ON "BroadcastCampaigns" ("TenantId", "Status");

                CREATE TABLE IF NOT EXISTS "BroadcastRecipients" (
                    "Id" uuid NOT NULL,
                    "CampaignId" uuid NOT NULL,
                    "Address" character varying(320) NOT NULL,
                    "Channel" character varying(64) NOT NULL,
                    "Status" integer NOT NULL,
                    "Attempts" integer NOT NULL,
                    "NotificationId" uuid NULL,
                    "ErrorCode" text NULL,
                    "ErrorMessage" text NULL,
                    "CreatedAtUtc" timestamp with time zone NOT NULL,
                    "ProcessedAtUtc" timestamp with time zone NULL,
                    "ContentHash" character varying(128) NOT NULL,
                    CONSTRAINT "PK_BroadcastRecipients" PRIMARY KEY ("Id")
                );
                CREATE INDEX IF NOT EXISTS "IX_BroadcastRecipients_CampaignId_Status_CreatedAtUtc"
                    ON "BroadcastRecipients" ("CampaignId", "Status", "CreatedAtUtc");
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_BroadcastRecipients_Idempotency"
                    ON "BroadcastRecipients" ("ContentHash");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TABLE IF EXISTS "BroadcastRecipients";
                DROP TABLE IF EXISTS "BroadcastCampaigns";
                """);
        }
    }
}
