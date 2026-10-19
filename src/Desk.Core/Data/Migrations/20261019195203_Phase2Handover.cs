using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desk.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase2Handover : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ForHandover",
                table: "Incidents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ForHandover",
                table: "Events",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "HandoverNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HotelId = table.Column<int>(type: "int", nullable: false),
                    ShiftDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Shift = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RemovedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RemovedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HandoverNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HandoverNotes_Hotels_HotelId",
                        column: x => x.HotelId,
                        principalTable: "Hotels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HandoverNotes_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HandoverNotes_Users_RemovedById",
                        column: x => x.RemovedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HandoverReads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HotelId = table.Column<int>(type: "int", nullable: false),
                    ShiftDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Shift = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ReadUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HandoverReads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HandoverReads_Hotels_HotelId",
                        column: x => x.HotelId,
                        principalTable: "Hotels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HandoverReads_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_ForHandover_OccurredLocal",
                table: "Incidents",
                columns: new[] { "ForHandover", "OccurredLocal" });

            migrationBuilder.CreateIndex(
                name: "IX_Events_ForHandover_StartedLocal",
                table: "Events",
                columns: new[] { "ForHandover", "StartedLocal" });

            migrationBuilder.CreateIndex(
                name: "IX_HandoverNotes_CreatedById",
                table: "HandoverNotes",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_HandoverNotes_HotelId_ShiftDate_Shift",
                table: "HandoverNotes",
                columns: new[] { "HotelId", "ShiftDate", "Shift" });

            migrationBuilder.CreateIndex(
                name: "IX_HandoverNotes_RemovedById",
                table: "HandoverNotes",
                column: "RemovedById");

            migrationBuilder.CreateIndex(
                name: "IX_HandoverReads_HotelId_ShiftDate_Shift_UserId",
                table: "HandoverReads",
                columns: new[] { "HotelId", "ShiftDate", "Shift", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HandoverReads_UserId",
                table: "HandoverReads",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HandoverNotes");

            migrationBuilder.DropTable(
                name: "HandoverReads");

            migrationBuilder.DropIndex(
                name: "IX_Incidents_ForHandover_OccurredLocal",
                table: "Incidents");

            migrationBuilder.DropIndex(
                name: "IX_Events_ForHandover_StartedLocal",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "ForHandover",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "ForHandover",
                table: "Events");
        }
    }
}
