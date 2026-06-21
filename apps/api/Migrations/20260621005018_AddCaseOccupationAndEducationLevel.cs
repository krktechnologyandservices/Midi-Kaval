using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MidiKaval.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCaseOccupationAndEducationLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "education_level_id",
                table: "cases",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "occupation_id",
                table: "cases",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_cases_education_level_id",
                table: "cases",
                column: "education_level_id");

            migrationBuilder.CreateIndex(
                name: "ix_cases_occupation_id",
                table: "cases",
                column: "occupation_id");

            migrationBuilder.AddForeignKey(
                name: "fk_cases_education_levels_education_level_id",
                table: "cases",
                column: "education_level_id",
                principalTable: "legend_education_levels",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_cases_occupations_occupation_id",
                table: "cases",
                column: "occupation_id",
                principalTable: "legend_occupations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_cases_education_levels_education_level_id",
                table: "cases");

            migrationBuilder.DropForeignKey(
                name: "fk_cases_occupations_occupation_id",
                table: "cases");

            migrationBuilder.DropIndex(
                name: "ix_cases_education_level_id",
                table: "cases");

            migrationBuilder.DropIndex(
                name: "ix_cases_occupation_id",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "education_level_id",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "occupation_id",
                table: "cases");
        }
    }
}
