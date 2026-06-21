using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MidiKaval.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCaseRecidivismAndFamilyHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "family_history_of_crime",
                table: "cases",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "recidivism_after_count",
                table: "cases",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "recidivism_before_count",
                table: "cases",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "family_history_of_crime",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "recidivism_after_count",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "recidivism_before_count",
                table: "cases");
        }
    }
}
