using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MidiKaval.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganisationsAndActivationTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_suspended",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "totp_enrolled_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "totp_secret",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "organisations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_organisations", x => x.id);
                });

            // Seed the pilot organisation (matching Seed:OrganisationId in test/dev config)
            // so that the seeder's self-healing check finds it and does not create a duplicate.
            // The UUID matches AuthTestData.OrganisationId / Seed:OrganisationId.
            migrationBuilder.Sql("""
                INSERT INTO organisations (id, name, is_active, created_at_utc)
                SELECT '00000000-0000-4000-8000-000000000001', 'Pilot Organisation', true, NOW() AT TIME ZONE 'UTC'
                WHERE NOT EXISTS (SELECT 1 FROM organisations LIMIT 1);
                """);

            // Update any existing users whose OrganisationId does not point to a valid org
            // to reference the pilot organisation.
            migrationBuilder.Sql("""
                UPDATE users
                SET organisation_id = '00000000-0000-4000-8000-000000000001'
                WHERE organisation_id NOT IN (
                    SELECT id FROM organisations
                );
                """);

            migrationBuilder.CreateTable(
                name: "activation_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organisation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    target_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    consumed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_activation_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_activation_tokens_organisations_organisation_id",
                        column: x => x.organisation_id,
                        principalTable: "organisations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_activation_tokens_organisation_id_token_hash",
                table: "activation_tokens",
                columns: new[] { "organisation_id", "token_hash" });

            migrationBuilder.AddForeignKey(
                name: "fk_users_organisations_organisation_id",
                table: "users",
                column: "organisation_id",
                principalTable: "organisations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_users_organisations_organisation_id",
                table: "users");

            migrationBuilder.DropTable(
                name: "activation_tokens");

            migrationBuilder.DropTable(
                name: "organisations");

            migrationBuilder.DropColumn(
                name: "is_suspended",
                table: "users");

            migrationBuilder.DropColumn(
                name: "totp_enrolled_at",
                table: "users");

            migrationBuilder.DropColumn(
                name: "totp_secret",
                table: "users");
        }
    }
}
