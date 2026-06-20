using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MidiKaval.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCaseAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "assigned_at_utc",
                table: "cases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "assigned_worker_id",
                table: "cases",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "case_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organisation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_worker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    to_worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    prior_actions = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    open_items = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    next_visit_purpose = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_case_assignments", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cases_assigned_worker_id",
                table: "cases",
                column: "assigned_worker_id");

            migrationBuilder.CreateIndex(
                name: "ix_cases_organisation_id_assigned_worker_id",
                table: "cases",
                columns: new[] { "organisation_id", "assigned_worker_id" });

            migrationBuilder.CreateIndex(
                name: "ix_case_assignments_organisation_id_case_id",
                table: "case_assignments",
                columns: new[] { "organisation_id", "case_id" });

            migrationBuilder.CreateIndex(
                name: "ix_case_assignments_organisation_id_to_worker_id",
                table: "case_assignments",
                columns: new[] { "organisation_id", "to_worker_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_cases_users_assigned_worker_id",
                table: "cases",
                column: "assigned_worker_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_cases_users_assigned_worker_id",
                table: "cases");

            migrationBuilder.DropTable(
                name: "case_assignments");

            migrationBuilder.DropIndex(
                name: "ix_cases_assigned_worker_id",
                table: "cases");

            migrationBuilder.DropIndex(
                name: "ix_cases_organisation_id_assigned_worker_id",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "assigned_at_utc",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "assigned_worker_id",
                table: "cases");
        }
    }
}
