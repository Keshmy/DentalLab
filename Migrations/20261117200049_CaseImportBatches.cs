using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DentalLab.Migrations
{
    /// <inheritdoc />
    public partial class CaseImportBatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ImportBatchId",
                table: "ServiceTypes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ImportBatchId",
                table: "LabCases",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ImportBatchId",
                table: "Doctors",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ImportBatchId",
                table: "Clinics",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ImportBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CaseCount = table.Column<int>(type: "int", nullable: false),
                    ItemCount = table.Column<int>(type: "int", nullable: false),
                    DoctorsCreated = table.Column<int>(type: "int", nullable: false),
                    ClinicsCreated = table.Column<int>(type: "int", nullable: false),
                    ProductsCreated = table.Column<int>(type: "int", nullable: false),
                    UndoneAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UndoneBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportBatches", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTypes_ImportBatchId",
                table: "ServiceTypes",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_LabCases_ImportBatchId",
                table: "LabCases",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Doctors_ImportBatchId",
                table: "Doctors",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Clinics_ImportBatchId",
                table: "Clinics",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_Created",
                table: "ImportBatches",
                column: "Created");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportBatches");

            migrationBuilder.DropIndex(
                name: "IX_ServiceTypes_ImportBatchId",
                table: "ServiceTypes");

            migrationBuilder.DropIndex(
                name: "IX_LabCases_ImportBatchId",
                table: "LabCases");

            migrationBuilder.DropIndex(
                name: "IX_Doctors_ImportBatchId",
                table: "Doctors");

            migrationBuilder.DropIndex(
                name: "IX_Clinics_ImportBatchId",
                table: "Clinics");

            migrationBuilder.DropColumn(
                name: "ImportBatchId",
                table: "ServiceTypes");

            migrationBuilder.DropColumn(
                name: "ImportBatchId",
                table: "LabCases");

            migrationBuilder.DropColumn(
                name: "ImportBatchId",
                table: "Doctors");

            migrationBuilder.DropColumn(
                name: "ImportBatchId",
                table: "Clinics");
        }
    }
}
