using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MidiKaval.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCaseStage6TerminationExclusion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "case_stage6_termination_exclusion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organisation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    termination_exclusion_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    jjb_details = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    exclusion_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    report_attachment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_case_stage6_termination_exclusion", x => x.id);
                    table.ForeignKey(
                        name: "fk_case_stage6_termination_exclusion_attachments_report_attach",
                        column: x => x.report_attachment_id,
                        principalTable: "attachments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_case_stage6_termination_exclusion_cases_case_id",
                        column: x => x.case_id,
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_case_stage6_termination_exclusion_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_case_stage6_termination_exclusion_case_id",
                table: "case_stage6_termination_exclusion",
                column: "case_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_case_stage6_termination_exclusion_created_by_user_id",
                table: "case_stage6_termination_exclusion",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_case_stage6_termination_exclusion_report_attachment_id",
                table: "case_stage6_termination_exclusion",
                column: "report_attachment_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "case_stage6_termination_exclusion");
        }
    }
}
