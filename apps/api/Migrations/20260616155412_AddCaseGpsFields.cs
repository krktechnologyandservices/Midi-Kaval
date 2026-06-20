using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MidiKaval.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCaseGpsFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "gps_verified",
                table: "cases",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "gps_verified_at_utc",
                table: "cases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "gps_verified_by_user_id",
                table: "cases",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "landmark",
                table: "cases",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "latitude",
                table: "cases",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "longitude",
                table: "cases",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "gps_verified",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "gps_verified_at_utc",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "gps_verified_by_user_id",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "landmark",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "latitude",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "longitude",
                table: "cases");
        }
    }
}
