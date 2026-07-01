using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MidiKaval.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCaseStage2Data : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "case_stage2_data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organisation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bio_psycho_social_assessment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    community_program_attendance = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    group_work = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    icp_records = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    life_skill_training = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    overall_progress = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    parent_management = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    pma_status = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_case_stage2_data", x => x.id);
                    table.ForeignKey(
                        name: "fk_case_stage2_data_cases_case_id",
                        column: x => x.case_id,
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_case_stage2_data_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_case_stage2_data_case_id",
                table: "case_stage2_data",
                column: "case_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_case_stage2_data_created_by_user_id",
                table: "case_stage2_data",
                column: "created_by_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "case_stage2_data");
        }
    }
}
