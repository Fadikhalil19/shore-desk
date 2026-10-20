using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desk.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase2OnDuty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CallOuts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HotelId = table.Column<int>(type: "int", nullable: false),
                    ShiftDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Shift = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    CalledLocal = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CoveredBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedById = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RemovedUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CallOuts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CallOuts_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CallOuts_Hotels_HotelId",
                        column: x => x.HotelId,
                        principalTable: "Hotels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CallOuts_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CallOuts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DutyAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HotelId = table.Column<int>(type: "int", nullable: false),
                    ShiftDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Shift = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    Radio = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Post = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    StartLocal = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndLocal = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RemovedUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DutyAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DutyAssignments_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DutyAssignments_Hotels_HotelId",
                        column: x => x.HotelId,
                        principalTable: "Hotels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DutyAssignments_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DutyAssignments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CallOuts_CreatedById",
                table: "CallOuts",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_CallOuts_DepartmentId",
                table: "CallOuts",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CallOuts_HotelId_ShiftDate_Shift",
                table: "CallOuts",
                columns: new[] { "HotelId", "ShiftDate", "Shift" });

            migrationBuilder.CreateIndex(
                name: "IX_CallOuts_UserId",
                table: "CallOuts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DutyAssignments_CreatedById",
                table: "DutyAssignments",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_DutyAssignments_DepartmentId",
                table: "DutyAssignments",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_DutyAssignments_HotelId_ShiftDate_Shift",
                table: "DutyAssignments",
                columns: new[] { "HotelId", "ShiftDate", "Shift" });

            migrationBuilder.CreateIndex(
                name: "IX_DutyAssignments_UserId",
                table: "DutyAssignments",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CallOuts");

            migrationBuilder.DropTable(
                name: "DutyAssignments");
        }
    }
}
