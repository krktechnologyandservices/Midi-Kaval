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
            migrationBuilder.CreateIndex(
                name: "ix_case_stage2_data_created_by_user_id",
                table: "case_stage2_data",
                column: "created_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_case_stage2_data_cases_case_id",
                table: "case_stage2_data",
                column: "case_id",
                principalTable: "cases",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_case_stage2_data_users_created_by_user_id",
                table: "case_stage2_data",
                column: "created_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_case_stage2_data_cases_case_id",
                table: "case_stage2_data");

            migrationBuilder.DropForeignKey(
                name: "fk_case_stage2_data_users_created_by_user_id",
                table: "case_stage2_data");

            migrationBuilder.DropIndex(
                name: "ix_case_stage2_data_created_by_user_id",
                table: "case_stage2_data");
        }
    }
}
