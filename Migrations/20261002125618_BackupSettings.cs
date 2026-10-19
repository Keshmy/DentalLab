using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DentalLab.Migrations
{
    /// <inheritdoc />
    public partial class BackupSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BackupSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FolderPath = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    AutoEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AutoHour = table.Column<int>(type: "int", nullable: false),
                    AutoMinute = table.Column<int>(type: "int", nullable: false),
                    LastAutoBackupDate = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    LastBackupAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastBackupMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Modified = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupSettings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "BackupSettings",
                columns: new[] { "Id", "AutoEnabled", "AutoHour", "AutoMinute", "Created", "FolderPath", "LastAutoBackupDate", "LastBackupAt", "LastBackupMessage", "Modified" },
                values: new object[] { new Guid("b1000000-0000-0000-0000-000000000001"), true, 2, 0, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "C:\\DentalLabBackups", null, null, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BackupSettings");
        }
    }
}
