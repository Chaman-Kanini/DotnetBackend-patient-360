using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustFirstPlatform.API.Migrations
{
    /// <inheritdoc />
    public partial class AddUploadBatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UploadBatchId",
                table: "ClinicalDocuments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UploadBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TotalDocuments = table.Column<int>(type: "integer", nullable: false),
                    ProcessedDocuments = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UploadBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UploadBatches_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalDocuments_UploadBatchId",
                table: "ClinicalDocuments",
                column: "UploadBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_UploadBatches_CreatedAt",
                table: "UploadBatches",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UploadBatches_Status",
                table: "UploadBatches",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_UploadBatches_UserId",
                table: "UploadBatches",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ClinicalDocuments_UploadBatches_UploadBatchId",
                table: "ClinicalDocuments",
                column: "UploadBatchId",
                principalTable: "UploadBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClinicalDocuments_UploadBatches_UploadBatchId",
                table: "ClinicalDocuments");

            migrationBuilder.DropTable(
                name: "UploadBatches");

            migrationBuilder.DropIndex(
                name: "IX_ClinicalDocuments_UploadBatchId",
                table: "ClinicalDocuments");

            migrationBuilder.DropColumn(
                name: "UploadBatchId",
                table: "ClinicalDocuments");
        }
    }
}
