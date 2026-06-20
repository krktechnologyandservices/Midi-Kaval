using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MidiKaval.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffDirectoryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "first_name",
                table: "users",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "last_name",
                table: "users",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "phone_number",
                table: "users",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "phone_number",
                table: "users");

            migrationBuilder.DropColumn(
                name: "last_name",
                table: "users");

            migrationBuilder.DropColumn(
                name: "first_name",
                table: "users");
        }
    }
}
