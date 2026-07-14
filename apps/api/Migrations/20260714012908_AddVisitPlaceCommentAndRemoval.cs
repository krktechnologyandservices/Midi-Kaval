using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MidiKaval.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddVisitPlaceCommentAndRemoval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "comment",
                table: "visit_places",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "comment_updated_at_utc",
                table: "visit_places",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "comment_updated_by_user_id",
                table: "visit_places",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "removed_at_utc",
                table: "visit_places",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "removed_by_user_id",
                table: "visit_places",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "comment",
                table: "visit_places");

            migrationBuilder.DropColumn(
                name: "comment_updated_at_utc",
                table: "visit_places");

            migrationBuilder.DropColumn(
                name: "comment_updated_by_user_id",
                table: "visit_places");

            migrationBuilder.DropColumn(
                name: "removed_at_utc",
                table: "visit_places");

            migrationBuilder.DropColumn(
                name: "removed_by_user_id",
                table: "visit_places");
        }
    }
}
