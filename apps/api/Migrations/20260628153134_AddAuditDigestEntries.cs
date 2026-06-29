using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MidiKaval.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditDigestEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_digest_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    audit_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organisation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    digest_sent_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    digest_batch_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_digest_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_audit_digest_entries_audit_events_audit_event_id",
                        column: x => x.audit_event_id,
                        principalTable: "audit_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_audit_digest_entries_organisations_organisation_id",
                        column: x => x.organisation_id,
                        principalTable: "organisations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_digest_entries_audit_event_id",
                table: "audit_digest_entries",
                column: "audit_event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_digest_entries_organisation_id_digest_sent_at_utc",
                table: "audit_digest_entries",
                columns: new[] { "organisation_id", "digest_sent_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_digest_entries");
        }
    }
}
