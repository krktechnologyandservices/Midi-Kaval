using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MidiKaval.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCaseRelatedCases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "case_related_cases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id_a = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id_b = table.Column<Guid>(type: "uuid", nullable: false),
                    relationship_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_case_related_cases", x => x.id);
                    table.CheckConstraint("ck_case_related_cases_ordered_pair", "case_id_a < case_id_b");
                    table.ForeignKey(
                        name: "fk_case_related_cases_cases_case_id_a",
                        column: x => x.case_id_a,
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_case_related_cases_cases_case_id_b",
                        column: x => x.case_id_b,
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_case_related_cases_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_case_related_cases_case_id_a_case_id_b",
                table: "case_related_cases",
                columns: new[] { "case_id_a", "case_id_b" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_case_related_cases_case_id_b",
                table: "case_related_cases",
                column: "case_id_b");

            migrationBuilder.CreateIndex(
                name: "ix_case_related_cases_created_by_user_id",
                table: "case_related_cases",
                column: "created_by_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "case_related_cases");
        }
    }
}
