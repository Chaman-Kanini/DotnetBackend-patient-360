using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TrustFirstPlatform.API.Migrations
{
    /// <inheritdoc />
    public partial class RecreateMedicalCodeTablesAsStandalone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop old tables with foreign keys
            migrationBuilder.DropTable(
                name: "CPTCodes");

            migrationBuilder.DropTable(
                name: "ICD10Codes");

            // Create new standalone ICD10Codes table
            migrationBuilder.CreateTable(
                name: "ICD10Codes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Diagnosis = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ICD10Codes", x => x.Id);
                });

            // Create new standalone CPTCodes table
            migrationBuilder.CreateTable(
                name: "CPTCodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Procedure = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CPTCodes", x => x.Id);
                });

            // Create indexes
            migrationBuilder.CreateIndex(
                name: "IX_ICD10Codes_Code",
                table: "ICD10Codes",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_CPTCodes_Code",
                table: "CPTCodes",
                column: "Code");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop standalone tables
            migrationBuilder.DropTable(
                name: "CPTCodes");

            migrationBuilder.DropTable(
                name: "ICD10Codes");

            // Recreate old tables with foreign keys
            migrationBuilder.CreateTable(
                name: "ICD10Codes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientContextId = table.Column<Guid>(type: "uuid", nullable: false),
                    Diagnosis = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ICD10Codes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ICD10Codes_PatientContexts_PatientContextId",
                        column: x => x.PatientContextId,
                        principalTable: "PatientContexts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CPTCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientContextId = table.Column<Guid>(type: "uuid", nullable: false),
                    Procedure = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CPTCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CPTCodes_PatientContexts_PatientContextId",
                        column: x => x.PatientContextId,
                        principalTable: "PatientContexts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ICD10Codes_Code",
                table: "ICD10Codes",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_ICD10Codes_PatientContextId",
                table: "ICD10Codes",
                column: "PatientContextId");

            migrationBuilder.CreateIndex(
                name: "IX_CPTCodes_Code",
                table: "CPTCodes",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_CPTCodes_PatientContextId",
                table: "CPTCodes",
                column: "PatientContextId");
        }
    }
}
