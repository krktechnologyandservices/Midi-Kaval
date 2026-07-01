using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MidiKaval.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCaseGenderFamilyTypeEconomicStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "economic_status",
                table: "cases",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "family_type",
                table: "cases",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "gender",
                table: "cases",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "legend_areas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organisation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_legend_areas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "legend_classifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organisation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_legend_classifications", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "legend_court_outcomes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organisation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_legend_court_outcomes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "legend_designations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organisation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_legend_designations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "legend_education_levels",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organisation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_legend_education_levels", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "legend_intervention_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organisation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_legend_intervention_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "legend_occupations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organisation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_legend_occupations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "legend_offence_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organisation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_legend_offence_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "legend_police_stations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organisation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_legend_police_stations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "legend_visit_outcomes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organisation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_legend_visit_outcomes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "report_export_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organisation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    format = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    blob_path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    error_message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    from_date = table.Column<DateOnly>(type: "date", nullable: true),
                    to_date = table.Column<DateOnly>(type: "date", nullable: true),
                    year = table.Column<int>(type: "integer", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_report_export_jobs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_actor_user_id",
                table: "audit_events",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_subject_user_id",
                table: "audit_events",
                column: "subject_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_legend_areas_organisation_id_name",
                table: "legend_areas",
                columns: new[] { "organisation_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_legend_classifications_organisation_id_name",
                table: "legend_classifications",
                columns: new[] { "organisation_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_legend_court_outcomes_organisation_id_name",
                table: "legend_court_outcomes",
                columns: new[] { "organisation_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_legend_designations_organisation_id_name",
                table: "legend_designations",
                columns: new[] { "organisation_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_legend_education_levels_organisation_id_name",
                table: "legend_education_levels",
                columns: new[] { "organisation_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_legend_intervention_categories_organisation_id_name",
                table: "legend_intervention_categories",
                columns: new[] { "organisation_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_legend_occupations_organisation_id_name",
                table: "legend_occupations",
                columns: new[] { "organisation_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_legend_offence_types_organisation_id_name",
                table: "legend_offence_types",
                columns: new[] { "organisation_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_legend_police_stations_organisation_id_name",
                table: "legend_police_stations",
                columns: new[] { "organisation_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_legend_visit_outcomes_organisation_id_name",
                table: "legend_visit_outcomes",
                columns: new[] { "organisation_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_report_export_jobs_organisation_id_created_by_user_id_creat",
                table: "report_export_jobs",
                columns: new[] { "organisation_id", "created_by_user_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_report_export_jobs_organisation_id_status_created_at_utc",
                table: "report_export_jobs",
                columns: new[] { "organisation_id", "status", "created_at_utc" });

            migrationBuilder.AddForeignKey(
                name: "fk_audit_events_users_actor_user_id",
                table: "audit_events",
                column: "actor_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_audit_events_users_subject_user_id",
                table: "audit_events",
                column: "subject_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_audit_events_users_actor_user_id",
                table: "audit_events");

            migrationBuilder.DropForeignKey(
                name: "fk_audit_events_users_subject_user_id",
                table: "audit_events");

            migrationBuilder.DropTable(
                name: "legend_areas");

            migrationBuilder.DropTable(
                name: "legend_classifications");

            migrationBuilder.DropTable(
                name: "legend_court_outcomes");

            migrationBuilder.DropTable(
                name: "legend_designations");

            migrationBuilder.DropTable(
                name: "legend_education_levels");

            migrationBuilder.DropTable(
                name: "legend_intervention_categories");

            migrationBuilder.DropTable(
                name: "legend_occupations");

            migrationBuilder.DropTable(
                name: "legend_offence_types");

            migrationBuilder.DropTable(
                name: "legend_police_stations");

            migrationBuilder.DropTable(
                name: "legend_visit_outcomes");

            migrationBuilder.DropTable(
                name: "report_export_jobs");

            migrationBuilder.DropIndex(
                name: "ix_audit_events_actor_user_id",
                table: "audit_events");

            migrationBuilder.DropIndex(
                name: "ix_audit_events_subject_user_id",
                table: "audit_events");

            migrationBuilder.DropColumn(
                name: "economic_status",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "family_type",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "gender",
                table: "cases");
        }
    }
}
