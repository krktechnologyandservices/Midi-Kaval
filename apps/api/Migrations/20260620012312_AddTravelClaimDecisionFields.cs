using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MidiKaval.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTravelClaimDecisionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "decided_at_utc",
                table: "travel_claims",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "decided_by_user_id",
                table: "travel_claims",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "decision_comment",
                table: "travel_claims",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_travel_claims_decided_by_user_id",
                table: "travel_claims",
                column: "decided_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_travel_claims_users_decided_by_user_id",
                table: "travel_claims",
                column: "decided_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_travel_claims_users_decided_by_user_id",
                table: "travel_claims");

            migrationBuilder.DropIndex(
                name: "ix_travel_claims_decided_by_user_id",
                table: "travel_claims");

            migrationBuilder.DropColumn(
                name: "decided_at_utc",
                table: "travel_claims");

            migrationBuilder.DropColumn(
                name: "decided_by_user_id",
                table: "travel_claims");

            migrationBuilder.DropColumn(
                name: "decision_comment",
                table: "travel_claims");
        }
    }
}
