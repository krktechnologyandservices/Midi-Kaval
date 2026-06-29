using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MidiKaval.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUniquePendingConfirmationTokenIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_confirmation_tokens_user_id",
                table: "confirmation_tokens");

            migrationBuilder.CreateIndex(
                name: "ix_confirmation_tokens_user_id_pending",
                table: "confirmation_tokens",
                column: "user_id",
                unique: true,
                filter: "\"consumed_at_utc\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_confirmation_tokens_user_id_pending",
                table: "confirmation_tokens");

            migrationBuilder.CreateIndex(
                name: "ix_confirmation_tokens_user_id",
                table: "confirmation_tokens",
                column: "user_id");
        }
    }
}
