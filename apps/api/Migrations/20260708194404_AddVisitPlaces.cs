using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MidiKaval.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddVisitPlaces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "visit_places",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organisation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    visit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    address = table.Column<string>(type: "text", nullable: false),
                    osm_reference = table.Column<string>(type: "text", nullable: true),
                    planned_latitude = table.Column<decimal>(type: "numeric", nullable: true),
                    planned_longitude = table.Column<decimal>(type: "numeric", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    logged_latitude = table.Column<decimal>(type: "numeric", nullable: true),
                    logged_longitude = table.Column<decimal>(type: "numeric", nullable: true),
                    logged_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    logged_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_visit_places", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "visit_places");
        }
    }
}
