using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustFirstPlatform.API.Migrations
{
    /// <inheritdoc />
    public partial class AddConflictTrackingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConflictCount",
                table: "PatientContexts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "HasConflicts",
                table: "PatientContexts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_PatientContexts_HasConflicts",
                table: "PatientContexts",
                column: "HasConflicts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PatientContexts_HasConflicts",
                table: "PatientContexts");

            migrationBuilder.DropColumn(
                name: "ConflictCount",
                table: "PatientContexts");

            migrationBuilder.DropColumn(
                name: "HasConflicts",
                table: "PatientContexts");
        }
    }
}
