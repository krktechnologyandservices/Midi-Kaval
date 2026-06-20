using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MidiKaval.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTravelClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "travel_claims",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organisation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claimant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    start_location = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    destination = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    transport_mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    auto_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    submitted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_travel_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_travel_claims_users_claimant_user_id",
                        column: x => x.claimant_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "travel_claim_cases",
                columns: table => new
                {
                    travel_claim_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organisation_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_travel_claim_cases", x => new { x.travel_claim_id, x.case_id });
                    table.ForeignKey(
                        name: "fk_travel_claim_cases_cases_case_id",
                        column: x => x.case_id,
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_travel_claim_cases_travel_claims_travel_claim_id",
                        column: x => x.travel_claim_id,
                        principalTable: "travel_claims",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_travel_claim_cases_case_id",
                table: "travel_claim_cases",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_travel_claim_cases_organisation_id_case_id",
                table: "travel_claim_cases",
                columns: new[] { "organisation_id", "case_id" });

            migrationBuilder.CreateIndex(
                name: "ix_travel_claims_claimant_user_id",
                table: "travel_claims",
                column: "claimant_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_travel_claims_organisation_id_claimant_user_id_claim_date",
                table: "travel_claims",
                columns: new[] { "organisation_id", "claimant_user_id", "claim_date" });

            migrationBuilder.CreateIndex(
                name: "ix_travel_claims_organisation_id_status_claim_date",
                table: "travel_claims",
                columns: new[] { "organisation_id", "status", "claim_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "travel_claim_cases");

            migrationBuilder.DropTable(
                name: "travel_claims");
        }
    }
}
