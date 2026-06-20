using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MidiKaval.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organisation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    crime_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    st_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    beneficiary_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    beneficiary_age = table.Column<int>(type: "integer", nullable: true),
                    beneficiary_contact = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    type_of_offence = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    offence_classification = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    domicile = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    is_first_time_offender = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    current_stage = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    visit_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cases", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cases_organisation_id_crime_number",
                table: "cases",
                columns: new[] { "organisation_id", "crime_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cases_organisation_id_st_number",
                table: "cases",
                columns: new[] { "organisation_id", "st_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cases");
        }
    }
}
