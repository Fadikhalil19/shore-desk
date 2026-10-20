using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desk.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase3Visitors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Visits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HotelId = table.Column<int>(type: "int", nullable: false),
                    VisitorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Company = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    VisitorType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AreaId = table.Column<int>(type: "int", nullable: true),
                    AreaDetail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    HostName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    HostUserId = table.Column<int>(type: "int", nullable: true),
                    Purpose = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    BadgeColor = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    BadgeNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    InLocal = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpectedOutLocal = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OutLocal = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CompanionsJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    PhotoStoredName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ScheduledVisitId = table.Column<int>(type: "int", nullable: true),
                    CreatedById = table.Column<int>(type: "int", nullable: false),
                    CheckedOutById = table.Column<int>(type: "int", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VoidedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Visits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Visits_Hotels_HotelId",
                        column: x => x.HotelId,
                        principalTable: "Hotels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Visits_LocationAreas_AreaId",
                        column: x => x.AreaId,
                        principalTable: "LocationAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Visits_Users_CheckedOutById",
                        column: x => x.CheckedOutById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Visits_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Visits_Users_HostUserId",
                        column: x => x.HostUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledVisits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HotelId = table.Column<int>(type: "int", nullable: false),
                    VisitorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Company = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    VisitorType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ExpectedLocal = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HostName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    HostUserId = table.Column<int>(type: "int", nullable: true),
                    Purpose = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedById = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VisitId = table.Column<int>(type: "int", nullable: true),
                    CancelledUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledVisits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduledVisits_Hotels_HotelId",
                        column: x => x.HotelId,
                        principalTable: "Hotels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduledVisits_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduledVisits_Users_HostUserId",
                        column: x => x.HostUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduledVisits_Visits_VisitId",
                        column: x => x.VisitId,
                        principalTable: "Visits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledVisits_CreatedById",
                table: "ScheduledVisits",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledVisits_HostUserId",
                table: "ScheduledVisits",
                column: "HostUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledVisits_HotelId_ExpectedLocal",
                table: "ScheduledVisits",
                columns: new[] { "HotelId", "ExpectedLocal" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledVisits_VisitId",
                table: "ScheduledVisits",
                column: "VisitId");

            migrationBuilder.CreateIndex(
                name: "IX_Visits_AreaId",
                table: "Visits",
                column: "AreaId");

            migrationBuilder.CreateIndex(
                name: "IX_Visits_CheckedOutById",
                table: "Visits",
                column: "CheckedOutById");

            migrationBuilder.CreateIndex(
                name: "IX_Visits_CreatedById",
                table: "Visits",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Visits_HostUserId",
                table: "Visits",
                column: "HostUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Visits_HotelId_OutLocal",
                table: "Visits",
                columns: new[] { "HotelId", "OutLocal" });

            migrationBuilder.CreateIndex(
                name: "IX_Visits_InLocal",
                table: "Visits",
                column: "InLocal");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduledVisits");

            migrationBuilder.DropTable(
                name: "Visits");
        }
    }
}
