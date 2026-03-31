using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustFirstPlatform.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientConsolidationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<JsonDocument>(
                name: "ConsolidatedData",
                table: "PatientContexts",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastConsolidatedAt",
                table: "PatientContexts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "PatientContexts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatientContexts_Status",
                table: "PatientContexts",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PatientContexts_Status",
                table: "PatientContexts");

            migrationBuilder.DropColumn(
                name: "ConsolidatedData",
                table: "PatientContexts");

            migrationBuilder.DropColumn(
                name: "LastConsolidatedAt",
                table: "PatientContexts");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "PatientContexts");
        }
    }
}
