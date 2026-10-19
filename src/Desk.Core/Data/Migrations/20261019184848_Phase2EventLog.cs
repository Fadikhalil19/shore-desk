using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Desk.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase2EventLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventReminders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HotelId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DueLocal = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RepeatsDaily = table.Column<bool>(type: "bit", nullable: false),
                    AssignedDepartmentId = table.Column<int>(type: "int", nullable: true),
                    CreatedById = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastDoneUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastDoneById = table.Column<int>(type: "int", nullable: true),
                    ClosedUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventReminders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventReminders_Departments_AssignedDepartmentId",
                        column: x => x.AssignedDepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EventReminders_Hotels_HotelId",
                        column: x => x.HotelId,
                        principalTable: "Hotels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EventReminders_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EventReminders_Users_LastDoneById",
                        column: x => x.LastDoneById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HotelId = table.Column<int>(type: "int", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AreaId = table.Column<int>(type: "int", nullable: true),
                    LocationId = table.Column<int>(type: "int", nullable: true),
                    LocationDetail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Room = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    RequestedByType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    RequestedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RequestedByUserId = table.Column<int>(type: "int", nullable: true),
                    RequestedByDepartmentId = table.Column<int>(type: "int", nullable: true),
                    RequestedLocal = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RelatesToType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    RelatesToName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RelatesToRoom = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Priority = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AssignedDepartmentId = table.Column<int>(type: "int", nullable: true),
                    AssignedToId = table.Column<int>(type: "int", nullable: true),
                    StartedLocal = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedLocal = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Shift = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Narrative = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: true),
                    CreatedById = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VoidedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Events_Departments_AssignedDepartmentId",
                        column: x => x.AssignedDepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Events_Departments_RequestedByDepartmentId",
                        column: x => x.RequestedByDepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Events_Hotels_HotelId",
                        column: x => x.HotelId,
                        principalTable: "Hotels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Events_ListItems_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "ListItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Events_LocationAreas_AreaId",
                        column: x => x.AreaId,
                        principalTable: "LocationAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Events_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Events_Users_AssignedToId",
                        column: x => x.AssignedToId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Events_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Events_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "ListItems",
                columns: new[] { "Id", "IsActive", "ListKey", "Name", "SortOrder" },
                values: new object[,]
                {
                    { 213, true, "event-category", "Administrative", 10 },
                    { 214, true, "event-category", "Alarm Activation", 20 },
                    { 215, true, "event-category", "Call Off", 30 },
                    { 216, true, "event-category", "Cybersecurity", 40 },
                    { 217, true, "event-category", "Desk Duty", 50 },
                    { 218, true, "event-category", "Door Lock Assist", 60 },
                    { 219, true, "event-category", "Elevator", 70 },
                    { 220, true, "event-category", "Employee Assist", 80 },
                    { 221, true, "event-category", "Escort", 90 },
                    { 222, true, "event-category", "Eviction", 100 },
                    { 223, true, "event-category", "Field Contact", 110 },
                    { 224, true, "event-category", "Fire Command Station", 120 },
                    { 225, true, "event-category", "Fire Drill", 130 },
                    { 226, true, "event-category", "Fire Extinguisher", 140 },
                    { 227, true, "event-category", "Fire Watch", 150 },
                    { 228, true, "event-category", "Guest Assist", 160 },
                    { 229, true, "event-category", "Guest Warning", 170 },
                    { 230, true, "event-category", "Hard Key", 180 },
                    { 231, true, "event-category", "Hazards", 190 },
                    { 232, true, "event-category", "Investigation", 200 },
                    { 233, true, "event-category", "Key Concern - Employee", 210 },
                    { 234, true, "event-category", "Lock Interrogation", 220 },
                    { 235, true, "event-category", "Lockout", 230 },
                    { 236, true, "event-category", "Lost and Found", 240 },
                    { 237, true, "event-category", "Medical", 250 },
                    { 238, true, "event-category", "Meeting", 260 },
                    { 239, true, "event-category", "Monthly Inventory", 270 },
                    { 240, true, "event-category", "Officer", 280 },
                    { 241, true, "event-category", "Orange Safety Card", 290 },
                    { 242, true, "event-category", "Packages and Mail", 300 },
                    { 243, true, "event-category", "Parking", 310 },
                    { 244, true, "event-category", "Safe Assist", 320 },
                    { 245, true, "event-category", "Safety", 330 },
                    { 246, true, "event-category", "Vendor", 340 },
                    { 247, true, "event-category", "Water Leak/Plumbing", 350 },
                    { 248, true, "event-category", "Wellness Check", 360 },
                    { 249, true, "event-category", "Wheelchair", 370 },
                    { 250, true, "event-category", "Other", 380 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventReminders_AssignedDepartmentId",
                table: "EventReminders",
                column: "AssignedDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_EventReminders_ClosedUtc_DueLocal",
                table: "EventReminders",
                columns: new[] { "ClosedUtc", "DueLocal" });

            migrationBuilder.CreateIndex(
                name: "IX_EventReminders_CreatedById",
                table: "EventReminders",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_EventReminders_HotelId",
                table: "EventReminders",
                column: "HotelId");

            migrationBuilder.CreateIndex(
                name: "IX_EventReminders_LastDoneById",
                table: "EventReminders",
                column: "LastDoneById");

            migrationBuilder.CreateIndex(
                name: "IX_Events_AreaId",
                table: "Events",
                column: "AreaId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_AssignedDepartmentId",
                table: "Events",
                column: "AssignedDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_AssignedToId",
                table: "Events",
                column: "AssignedToId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_CategoryId",
                table: "Events",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_CompletedLocal_HotelId",
                table: "Events",
                columns: new[] { "CompletedLocal", "HotelId" });

            migrationBuilder.CreateIndex(
                name: "IX_Events_CreatedById",
                table: "Events",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Events_HotelId",
                table: "Events",
                column: "HotelId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_LocationId",
                table: "Events",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_RequestedByDepartmentId",
                table: "Events",
                column: "RequestedByDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_RequestedByUserId",
                table: "Events",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_StartedLocal",
                table: "Events",
                column: "StartedLocal");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventReminders");

            migrationBuilder.DropTable(
                name: "Events");

            migrationBuilder.DeleteData(
                table: "ListItems",
                keyColumn: "Id",
                keyValue: 213);

            migrationBuilder.DeleteData(
                table: "ListItems",
                keyColumn: "Id",
                keyValue: 214);

            migrationBuilder.DeleteData(
                table: "ListItems",
                keyColumn: "Id",
                keyValue: 215);

            migrationBuilder.DeleteData(
                table: "ListItems",
                keyColumn: "Id",
                keyValue: 216);

            migrationBuilder.DeleteData(
                table: "ListItems",
                keyColumn: "Id",
                keyValue: 217);

            migrationBuilder.DeleteData(
                table: "ListItems",
                keyColumn: "Id",
                keyValue: 218);

            migrationBuilder.DeleteData(
                table: "ListItems",
                keyColumn: "Id",
                keyValue: 219);

            migrationBuilder.DeleteData(
                table: "ListItems",
                keyColumn: "Id",
                keyValue: 220);

            migrationBuilder.DeleteData(
                table: "ListItems",
                keyColumn: "Id",
                keyValue: 221);

            migrationBuilder.DeleteData(
                table: "ListItems",
                keyColumn: "Id",
                keyValue: 222);

            migrationBuilder.DeleteData(
                table: "ListItems",
                keyColumn: "Id",
                keyValue: 223);

            migrationBuilder.DeleteData(
                table: "ListItems",
                keyColumn: "Id",
                keyValue: 224);

            migrationBuilder.DeleteData(
                table: "ListItems",
                keyColumn: "Id",
                keyValue: 225);

            migrationBuilder.DeleteData(
                table: "ListItems",
                keyColumn: "Id",
                keyValue: 226);

            migrationBuilder.DeleteData(
                table: "ListItems",
                keyColumn: "Id",
                keyValue: 227);

            migrationBuilder.DeleteData(
                table: "ListItems",
                keyColumn: "Id",
                keyValue: 228);

            migrationBuilder.DeleteData(
                table: "ListItems",
                keyColumn: "Id",
                keyValue: 229);

            migrationBuilder.DeleteData(
                table: "ListItems",
                keyColumn: "Id",
                keyValue: 230);

            migrationBuilder.DeleteData(
                table: "ListItems",
                keyColumn: "Id",
                keyValue: 231);

            migrationBuilder.DeleteData(
                table: "ListItems",
                keyColumn: "Id",
                keyValue: 232);

            migrationBuilder.DeleteData(
                table: "ListItems",
                keyColumn: "Id",
                keyValue: 233);

            migrationBuilder.DeleteData(
                table: "ListItems",
                keyColumn: "Id",
                keyValue: 234);

            migrationBuilder.DeleteData(
                table: "ListItems",
                keyColumn: "Id",
                keyValue: 235);

            migrationBuilder.DeleteData(
                table: "ListItems",
                keyColumn: "Id",
                keyValue: 236);

            migrationBuilder.DeleteData(
                table: "ListItems",
                keyColumn: "Id",
                keyValue: 237);

            migrationBuilder.DeleteData(
                table: "ListItems",
                keyColumn: "Id",
                keyValue: 238);

            migrationBuilder.DeleteData(
                table: "ListItems",
                keyColumn: "Id",
                keyValue: 239);

            migrationBuilder.DeleteData(
                table: "ListItems",
                keyColumn: "Id",
                keyValue: 240);

            migrationBuilder.DeleteData(
                table: "ListItems",
                keyColumn: "Id",
                keyValue: 241);

            migrationBuilder.DeleteData(
                table: "ListItems",
                keyColumn: "Id",
                keyValue: 242);

            migrationBuilder.DeleteData(
                table: "ListItems",
                keyColumn: "Id",
                keyValue: 243);

            migrationBuilder.DeleteData(
                table: "ListItems",
                keyColumn: "Id",
                keyValue: 244);

            migrationBuilder.DeleteData(
                table: "ListItems",
                keyColumn: "Id",
                keyValue: 245);

            migrationBuilder.DeleteData(
                table: "ListItems",
                keyColumn: "Id",
                keyValue: 246);

            migrationBuilder.DeleteData(
                table: "ListItems",
                keyColumn: "Id",
                keyValue: 247);

            migrationBuilder.DeleteData(
                table: "ListItems",
                keyColumn: "Id",
                keyValue: 248);

            migrationBuilder.DeleteData(
                table: "ListItems",
                keyColumn: "Id",
                keyValue: 249);

            migrationBuilder.DeleteData(
                table: "ListItems",
                keyColumn: "Id",
                keyValue: 250);
        }
    }
}
