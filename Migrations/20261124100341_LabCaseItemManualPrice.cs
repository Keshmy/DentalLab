using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DentalLab.Migrations
{
    /// <inheritdoc />
    public partial class LabCaseItemManualPrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsManualPrice",
                table: "LabCaseItems",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsManualPrice",
                table: "LabCaseItems");
        }
    }
}
