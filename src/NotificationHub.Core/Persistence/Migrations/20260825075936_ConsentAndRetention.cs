using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotificationHub.Core.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConsentAndRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "consent_ledger",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Purpose = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Channel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Granted = table.Column<bool>(type: "boolean", nullable: false),
                    Source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Actor = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Evidence = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consent_ledger", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_consent_ledger_OccurredAt",
                table: "consent_ledger",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_consent_ledger_TenantId_SubjectId_Purpose_Channel_OccurredAt",
                table: "consent_ledger",
                columns: new[] { "TenantId", "SubjectId", "Purpose", "Channel", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "consent_ledger");
        }
    }
}
