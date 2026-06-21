using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MidiKaval.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgetUtilizations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "budget_utilizations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    budget_line_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount_utilized = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    utilization_date = table.Column<DateOnly>(type: "date", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deleted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_budget_utilizations", x => x.id);
                    table.ForeignKey(
                        name: "fk_budget_utilizations_budget_line_items_budget_line_item_id",
                        column: x => x.budget_line_item_id,
                        principalTable: "budget_line_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_budget_utilizations_cases_case_id",
                        column: x => x.case_id,
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_budget_utilizations_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_budget_utilizations_budget_line_item_id_utilization_date",
                table: "budget_utilizations",
                columns: new[] { "budget_line_item_id", "utilization_date" });

            migrationBuilder.CreateIndex(
                name: "ix_budget_utilizations_case_id",
                table: "budget_utilizations",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_budget_utilizations_created_by_user_id",
                table: "budget_utilizations",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_budget_utilizations_deleted_at_utc",
                table: "budget_utilizations",
                column: "deleted_at_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "budget_utilizations");
        }
    }
}
