using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desk.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase4DutyLegacyRef : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LegacyRef",
                table: "DutyAssignments",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyRef",
                table: "CallOuts",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DutyAssignments_LegacyRef",
                table: "DutyAssignments",
                column: "LegacyRef");

            migrationBuilder.CreateIndex(
                name: "IX_CallOuts_LegacyRef",
                table: "CallOuts",
                column: "LegacyRef");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DutyAssignments_LegacyRef",
                table: "DutyAssignments");

            migrationBuilder.DropIndex(
                name: "IX_CallOuts_LegacyRef",
                table: "CallOuts");

            migrationBuilder.DropColumn(
                name: "LegacyRef",
                table: "DutyAssignments");

            migrationBuilder.DropColumn(
                name: "LegacyRef",
                table: "CallOuts");
        }
    }
}
