using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MidiKaval.Api.Migrations
{
    /// <inheritdoc />
    public partial class EncryptPiiColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Npgsql cannot auto-cast decimal(numeric) → bytea or varchar → bytea,
            // so we use raw SQL with explicit USING clauses.
            // For a fresh database with no real data, the text representation is
            // converted as a placeholder — real encryption happens at the app layer.

            migrationBuilder.Sql(
                "ALTER TABLE cases ALTER COLUMN longitude TYPE bytea USING longitude::text::bytea;");

            migrationBuilder.Sql(
                "ALTER TABLE cases ALTER COLUMN latitude TYPE bytea USING latitude::text::bytea;");

            migrationBuilder.Sql(
                "ALTER TABLE cases ALTER COLUMN landmark TYPE bytea USING landmark::bytea;");

            migrationBuilder.Sql(
                "ALTER TABLE cases ALTER COLUMN beneficiary_name TYPE bytea USING beneficiary_name::bytea;");

            migrationBuilder.Sql(
                "ALTER TABLE cases ALTER COLUMN beneficiary_contact TYPE bytea USING beneficiary_contact::bytea;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse: bytea → numeric for lat/lng, bytea → varchar for text columns.
            // The original values are lost (encrypted), so we use NULL as fallback.

            migrationBuilder.Sql(
                "ALTER TABLE cases ALTER COLUMN longitude TYPE numeric(9,6) USING NULL::numeric;");

            migrationBuilder.Sql(
                "ALTER TABLE cases ALTER COLUMN latitude TYPE numeric(9,6) USING NULL::numeric;");

            migrationBuilder.Sql(
                "ALTER TABLE cases ALTER COLUMN landmark TYPE character varying(500) USING NULL::character varying;");

            migrationBuilder.Sql(
                "ALTER TABLE cases ALTER COLUMN beneficiary_name TYPE character varying(256) USING ''::character varying;");

            migrationBuilder.Sql(
                "ALTER TABLE cases ALTER COLUMN beneficiary_contact TYPE character varying(32) USING NULL::character varying;");
        }
    }
}
