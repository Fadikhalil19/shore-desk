using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desk.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase4FoundLegacyRef : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LegacyRef",
                table: "FoundItems",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FoundItems_LegacyRef",
                table: "FoundItems",
                column: "LegacyRef");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FoundItems_LegacyRef",
                table: "FoundItems");

            migrationBuilder.DropColumn(
                name: "LegacyRef",
                table: "FoundItems");
        }
    }
}
