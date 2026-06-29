#nullable disable

namespace MidiKaval.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditEventTargetSnapshotAndIp : Microsoft.EntityFrameworkCore.Migrations.Migration
    {
        /// <inheritdoc />
        protected override void Up(Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "actor_ip_address",
                table: "audit_events",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "target_user_snapshot",
                table: "audit_events",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "actor_ip_address",
                table: "audit_events");

            migrationBuilder.DropColumn(
                name: "target_user_snapshot",
                table: "audit_events");
        }
    }
}
