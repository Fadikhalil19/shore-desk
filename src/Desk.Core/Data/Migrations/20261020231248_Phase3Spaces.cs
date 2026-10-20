using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desk.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase3Spaces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SpaceAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SpaceId = table.Column<int>(type: "int", nullable: false),
                    HotelId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: true),
                    VehicleMake = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    VehicleModel = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    VehicleYear = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    VehicleColor = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Plate = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PlateState = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    StickerNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AssignedById = table.Column<int>(type: "int", nullable: false),
                    ReleasedById = table.Column<int>(type: "int", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpaceAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpaceAssignments_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SpaceAssignments_Users_AssignedById",
                        column: x => x.AssignedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SpaceAssignments_Users_ReleasedById",
                        column: x => x.ReleasedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SpaceAssignments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Spaces",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HotelId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Section = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Number = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    LockCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    LockSerial = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CurrentAssignmentId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Spaces", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Spaces_Hotels_HotelId",
                        column: x => x.HotelId,
                        principalTable: "Hotels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Spaces_SpaceAssignments_CurrentAssignmentId",
                        column: x => x.CurrentAssignmentId,
                        principalTable: "SpaceAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SpaceAssignments_AssignedById",
                table: "SpaceAssignments",
                column: "AssignedById");

            migrationBuilder.CreateIndex(
                name: "IX_SpaceAssignments_DepartmentId",
                table: "SpaceAssignments",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_SpaceAssignments_ReleasedById",
                table: "SpaceAssignments",
                column: "ReleasedById");

            migrationBuilder.CreateIndex(
                name: "IX_SpaceAssignments_SpaceId",
                table: "SpaceAssignments",
                column: "SpaceId");

            migrationBuilder.CreateIndex(
                name: "IX_SpaceAssignments_UserId",
                table: "SpaceAssignments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Spaces_CurrentAssignmentId",
                table: "Spaces",
                column: "CurrentAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Spaces_HotelId_Kind_Section_Number",
                table: "Spaces",
                columns: new[] { "HotelId", "Kind", "Section", "Number" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SpaceAssignments_Spaces_SpaceId",
                table: "SpaceAssignments",
                column: "SpaceId",
                principalTable: "Spaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SpaceAssignments_Spaces_SpaceId",
                table: "SpaceAssignments");

            migrationBuilder.DropTable(
                name: "Spaces");

            migrationBuilder.DropTable(
                name: "SpaceAssignments");
        }
    }
}
