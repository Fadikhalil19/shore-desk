using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Desk.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivityLog",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Module = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    EntityType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    EntityId = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Details = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Hotels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hotels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DepartmentAccess",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    Module = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Level = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    LimitHotelId = table.Column<int>(type: "int", nullable: true),
                    DepartmentLimit = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    LimitDepartmentId = table.Column<int>(type: "int", nullable: true),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepartmentAccess", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DepartmentAccess_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DepartmentAccess_Departments_LimitDepartmentId",
                        column: x => x.LimitDepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DepartmentAccess_Hotels_LimitHotelId",
                        column: x => x.LimitHotelId,
                        principalTable: "Hotels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LocationAreas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HotelId = table.Column<int>(type: "int", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationAreas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LocationAreas_Hotels_HotelId",
                        column: x => x.HotelId,
                        principalTable: "Hotels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntraObjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    JobTitle = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    HotelId = table.Column<int>(type: "int", nullable: true),
                    FirstSeenUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FirstSignInUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FirstSignInNotifiedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BlockedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RemovedUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Users_Hotels_HotelId",
                        column: x => x.HotelId,
                        principalTable: "Hotels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Locations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AreaId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Locations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Locations_LocationAreas_AreaId",
                        column: x => x.AreaId,
                        principalTable: "LocationAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserAccess",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppUserId = table.Column<int>(type: "int", nullable: false),
                    Module = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Level = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    LimitHotelId = table.Column<int>(type: "int", nullable: true),
                    DepartmentLimit = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    LimitDepartmentId = table.Column<int>(type: "int", nullable: true),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAccess", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserAccess_Departments_LimitDepartmentId",
                        column: x => x.LimitDepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserAccess_Hotels_LimitHotelId",
                        column: x => x.LimitHotelId,
                        principalTable: "Hotels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserAccess_Users_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Departments",
                columns: new[] { "Id", "IsActive", "Name" },
                values: new object[,]
                {
                    { 1, true, "Accounting" },
                    { 2, true, "Admin" },
                    { 3, true, "Concierge" },
                    { 4, true, "Engineering" },
                    { 5, true, "F&B Kitchen" },
                    { 6, true, "F&B Mini Bar" },
                    { 7, true, "F&B Restaurant" },
                    { 8, true, "Finance" },
                    { 9, true, "Guest Services" },
                    { 10, true, "Housekeeping" },
                    { 11, true, "Human Resources" },
                    { 12, true, "IT" },
                    { 13, true, "Laundry" },
                    { 14, true, "Ownership" },
                    { 15, true, "Parking and Valet" },
                    { 16, true, "Reservations" },
                    { 17, true, "Revenue Management" },
                    { 18, true, "Safety" },
                    { 19, true, "Sales" }
                });

            migrationBuilder.InsertData(
                table: "Hotels",
                columns: new[] { "Id", "Address", "Code", "IsActive", "Name" },
                values: new object[,]
                {
                    { 1, "1515 Ocean Ave, Santa Monica, CA", "SHO", true, "Shore Hotel" },
                    { 2, null, "SMH", true, "Santa Monica Hotel" },
                    { 3, null, "OVH", true, "Ocean View Hotel" }
                });

            migrationBuilder.InsertData(
                table: "LocationAreas",
                columns: new[] { "Id", "HotelId", "IsActive", "Name", "SortOrder" },
                values: new object[,]
                {
                    { 17, null, true, "Guest Room", 170 },
                    { 18, null, true, "IDF", 180 },
                    { 19, null, true, "Server Room", 190 },
                    { 20, null, true, "Lost and Found", 200 },
                    { 21, null, true, "Remote", 210 },
                    { 1, 1, true, "A Building 1st Floor", 10 },
                    { 2, 1, true, "A Building 2nd Floor", 20 },
                    { 3, 1, true, "A Building 3rd Floor", 30 },
                    { 4, 1, true, "A Building 4th Floor", 40 },
                    { 5, 1, true, "B Building 1st Floor", 50 },
                    { 6, 1, true, "B Building 2nd Floor", 60 },
                    { 7, 1, true, "B Building 3rd Floor", 70 },
                    { 8, 1, true, "B Building 4th Floor", 80 },
                    { 9, 1, true, "Garage", 90 },
                    { 10, 1, true, "Hallway", 100 },
                    { 11, 1, true, "Mezzanine", 110 },
                    { 12, 1, true, "Parking Structure", 120 },
                    { 13, 1, true, "Property (grounds)", 130 },
                    { 14, 1, true, "BPT", 140 },
                    { 15, 2, true, "Santa Monica Hotel", 150 },
                    { 16, 3, true, "Ocean View Hotel", 160 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLog_Module_EntityType_EntityId",
                table: "ActivityLog",
                columns: new[] { "Module", "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLog_UserId_Utc",
                table: "ActivityLog",
                columns: new[] { "UserId", "Utc" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLog_Utc",
                table: "ActivityLog",
                column: "Utc");

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentAccess_DepartmentId_Module",
                table: "DepartmentAccess",
                columns: new[] { "DepartmentId", "Module" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentAccess_LimitDepartmentId",
                table: "DepartmentAccess",
                column: "LimitDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentAccess_LimitHotelId",
                table: "DepartmentAccess",
                column: "LimitHotelId");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_Name",
                table: "Departments",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Hotels_Code",
                table: "Hotels",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LocationAreas_HotelId_Name",
                table: "LocationAreas",
                columns: new[] { "HotelId", "Name" },
                unique: true,
                filter: "[HotelId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_AreaId_Name",
                table: "Locations",
                columns: new[] { "AreaId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAccess_AppUserId_Module",
                table: "UserAccess",
                columns: new[] { "AppUserId", "Module" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAccess_LimitDepartmentId",
                table: "UserAccess",
                column: "LimitDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAccess_LimitHotelId",
                table: "UserAccess",
                column: "LimitHotelId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_DepartmentId",
                table: "Users",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_EntraObjectId",
                table: "Users",
                column: "EntraObjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_HotelId",
                table: "Users",
                column: "HotelId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityLog");

            migrationBuilder.DropTable(
                name: "DepartmentAccess");

            migrationBuilder.DropTable(
                name: "Locations");

            migrationBuilder.DropTable(
                name: "UserAccess");

            migrationBuilder.DropTable(
                name: "LocationAreas");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Departments");

            migrationBuilder.DropTable(
                name: "Hotels");
        }
    }
}
