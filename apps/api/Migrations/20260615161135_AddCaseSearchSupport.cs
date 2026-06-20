using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MidiKaval.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCaseSearchSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "next_visit_due_at_utc",
                table: "cases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "case_search_presets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organisation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    filters_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_case_search_presets", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cases_organisation_id_updated_at_utc",
                table: "cases",
                columns: new[] { "organisation_id", "updated_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_case_search_presets_organisation_id_user_id_name",
                table: "case_search_presets",
                columns: new[] { "organisation_id", "user_id", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "case_search_presets");

            migrationBuilder.DropIndex(
                name: "ix_cases_organisation_id_updated_at_utc",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "next_visit_due_at_utc",
                table: "cases");
        }
    }
}
