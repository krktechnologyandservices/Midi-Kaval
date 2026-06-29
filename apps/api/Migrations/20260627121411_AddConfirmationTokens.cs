using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MidiKaval.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddConfirmationTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "totp_secret",
                table: "users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "suspended_at_utc",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "confirmation_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invitation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    consumed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    delivery_attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_delivery_attempt_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_confirmation_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_confirmation_tokens_invitations_invitation_id",
                        column: x => x.invitation_id,
                        principalTable: "invitations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_confirmation_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_confirmation_tokens_invitation_id",
                table: "confirmation_tokens",
                column: "invitation_id");

            migrationBuilder.CreateIndex(
                name: "ix_confirmation_tokens_token_hash",
                table: "confirmation_tokens",
                column: "token_hash");

            migrationBuilder.CreateIndex(
                name: "ix_confirmation_tokens_user_id",
                table: "confirmation_tokens",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "confirmation_tokens");

            migrationBuilder.DropColumn(
                name: "suspended_at_utc",
                table: "users");

            migrationBuilder.AlterColumn<string>(
                name: "totp_secret",
                table: "users",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);
        }
    }
}
