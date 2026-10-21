using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desk.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase4LegacyRef : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LegacyRef",
                table: "Incidents",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyRef",
                table: "Events",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_LegacyRef",
                table: "Incidents",
                column: "LegacyRef");

            migrationBuilder.CreateIndex(
                name: "IX_Events_LegacyRef",
                table: "Events",
                column: "LegacyRef");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Incidents_LegacyRef",
                table: "Incidents");

            migrationBuilder.DropIndex(
                name: "IX_Events_LegacyRef",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "LegacyRef",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "LegacyRef",
                table: "Events");
        }
    }
}
