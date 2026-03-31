using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustFirstPlatform.API.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicalDocumentsAndPatientContexts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PatientContexts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientIdentifier = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PatientName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientContexts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientContexts_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClinicalDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientContextId = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    StoredFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    FileExtension = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FileHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ValidationError = table.Column<string>(type: "text", nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata = table.Column<JsonDocument>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicalDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClinicalDocuments_PatientContexts_PatientContextId",
                        column: x => x.PatientContextId,
                        principalTable: "PatientContexts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ClinicalDocuments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalDocuments_FileHash",
                table: "ClinicalDocuments",
                column: "FileHash");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalDocuments_PatientContextId",
                table: "ClinicalDocuments",
                column: "PatientContextId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalDocuments_Status",
                table: "ClinicalDocuments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalDocuments_UploadedAt",
                table: "ClinicalDocuments",
                column: "UploadedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalDocuments_UserId",
                table: "ClinicalDocuments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientContexts_CreatedAt",
                table: "PatientContexts",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PatientContexts_CreatedByUserId",
                table: "PatientContexts",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientContexts_PatientIdentifier",
                table: "PatientContexts",
                column: "PatientIdentifier",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClinicalDocuments");

            migrationBuilder.DropTable(
                name: "PatientContexts");
        }
    }
}
