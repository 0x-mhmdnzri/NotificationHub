using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotificationHub.Core.Persistence.Migrations;

/// <summary>
/// Ensures Hangfire PostgreSQL schema exists and optimizes outbox for Hangfire dispatch/reconciliation.
/// Hangfire.PostgreSql stores jobs under schema "hangfire" (not EF entities).
/// Tables are created by Hangfire installer at startup; this migration creates the schema + app indexes.
/// </summary>
[Migration("20260828120000_HangfireSchemaAndOutboxIndexes")]
public partial class HangfireSchemaAndOutboxIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Hangfire storage lives in its own schema (visible in pgAdmin / \dn)
        migrationBuilder.Sql("""
            CREATE SCHEMA IF NOT EXISTS hangfire;
            COMMENT ON SCHEMA hangfire IS 'Hangfire.PostgreSql job storage (outbox dispatch, reconciliation, recurring jobs)';
            """);

        // Helpful indexes for OutboxRelayWorker + OutboxReconciliationJob (Hangfire safety net)
        migrationBuilder.Sql("""
            CREATE INDEX IF NOT EXISTS ix_outbox_messages_status_next_attempt
                ON outbox_messages (status, "NextAttemptAt")
                WHERE status = 'pending';

            CREATE INDEX IF NOT EXISTS ix_outbox_messages_status_created
                ON outbox_messages (status, "CreatedAt")
                WHERE status IN ('pending', 'failed');

            """);

        // Marker row so operators can see Hangfire readiness was applied via EF history
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS hangfire_ef_marker (
                id          int PRIMARY KEY DEFAULT 1 CHECK (id = 1),
                installed_at timestamptz NOT NULL DEFAULT now(),
                note        text NOT NULL DEFAULT 'Hangfire schema prepared; tables created by Hangfire.PostgreSql installer on Host start'
            );
            INSERT INTO hangfire_ef_marker (id) VALUES (1) ON CONFLICT DO NOTHING;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP INDEX IF EXISTS ix_outbox_messages_status_next_attempt;
            DROP INDEX IF EXISTS ix_outbox_messages_status_created;
            DROP INDEX IF EXISTS ix_outbox_messages_notification_id;
            DROP TABLE IF EXISTS hangfire_ef_marker;
            -- Do not DROP SCHEMA hangfire — may contain live jobs
            """);
    }
}
