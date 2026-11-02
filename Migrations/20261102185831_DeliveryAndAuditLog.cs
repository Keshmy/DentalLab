using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DentalLab.Migrations
{
    /// <inheritdoc />
    public partial class DeliveryAndAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveredAt",
                table: "LabCases",
                type: "datetime2",
                nullable: true);

            // الحالات القديمة التي مرّ موعد تسليمها تُعتبر مُسلَّمة في ذلك الموعد،
            // حتى لا تظهر كلها «متأخرة» في لوحة مواعيد التسليم.
            migrationBuilder.Sql(
                "UPDATE LabCases SET DeliveredAt = DeliveryDate " +
                "WHERE DeliveredAt IS NULL AND DeliveryDate IS NOT NULL AND DeliveryDate <= CAST(GETDATE() AS date)");

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Action = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    EntityLabel = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Changes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LabCases_DeliveryDate_DeliveredAt",
                table: "LabCases",
                columns: new[] { "DeliveryDate", "DeliveredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_LabCases_DeliveryDate_DeliveredAt",
                table: "LabCases");

            migrationBuilder.DropColumn(
                name: "DeliveredAt",
                table: "LabCases");
        }
    }
}
