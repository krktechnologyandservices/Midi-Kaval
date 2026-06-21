using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MidiKaval.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgetSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "project_budgets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organisation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    financial_year_start = table.Column<DateOnly>(type: "date", nullable: false),
                    financial_year_end = table.Column<DateOnly>(type: "date", nullable: false),
                    approval_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    approved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decision_comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    decided_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_budgets", x => x.id);
                    table.ForeignKey(
                        name: "fk_project_budgets_users_approved_by_user_id",
                        column: x => x.approved_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_project_budgets_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "budget_line_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_budget_id = table.Column<Guid>(type: "uuid", nullable: false),
                    budget_head = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    amount_allocated = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    amount_utilized = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_budget_line_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_budget_line_items_project_budgets_project_budget_id",
                        column: x => x.project_budget_id,
                        principalTable: "project_budgets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_budget_line_items_project_budget_id",
                table: "budget_line_items",
                column: "project_budget_id");

            migrationBuilder.CreateIndex(
                name: "ix_budget_line_items_project_budget_id_budget_head",
                table: "budget_line_items",
                columns: new[] { "project_budget_id", "budget_head" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_project_budgets_approved_by_user_id",
                table: "project_budgets",
                column: "approved_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_budgets_created_by_user_id",
                table: "project_budgets",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_budgets_organisation_id",
                table: "project_budgets",
                column: "organisation_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_budgets_organisation_id_financial_year_start_source",
                table: "project_budgets",
                columns: new[] { "organisation_id", "financial_year_start", "source" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "budget_line_items");

            migrationBuilder.DropTable(
                name: "project_budgets");
        }
    }
}
