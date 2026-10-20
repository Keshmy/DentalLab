using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DentalLab.Migrations
{
    /// <inheritdoc />
    public partial class ClinicCreditEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClinicCreditEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    ClinicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LabCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Modified = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicCreditEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClinicCreditEntries_Clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "Clinics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClinicCreditEntries_LabCases_LabCaseId",
                        column: x => x.LabCaseId,
                        principalTable: "LabCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicCreditEntries_ClinicId",
                table: "ClinicCreditEntries",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicCreditEntries_LabCaseId",
                table: "ClinicCreditEntries",
                column: "LabCaseId",
                unique: true,
                filter: "[LabCaseId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClinicCreditEntries");
        }
    }
}
