using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desk.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase3LostFound : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FoundItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    HotelId = table.Column<int>(type: "int", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: true),
                    Detail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ColorId = table.Column<int>(type: "int", nullable: true),
                    Brand = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ModelSerial = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    HighValue = table.Column<bool>(type: "bit", nullable: false),
                    FoundLocal = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AreaId = table.Column<int>(type: "int", nullable: true),
                    LocationDetail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Room = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    FoundByType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    FoundByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FoundByUserId = table.Column<int>(type: "int", nullable: true),
                    FoundByDepartmentId = table.Column<int>(type: "int", nullable: true),
                    StorageLocation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PurgeDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    OwnerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OwnerContact = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PhotoStoredName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ClosedLocal = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedById = table.Column<int>(type: "int", nullable: true),
                    ReturnedToName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReturnSignature = table.Column<byte[]>(type: "varbinary(max)", maxLength: -1, nullable: true),
                    DisposalMethod = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    CloseNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ForHandover = table.Column<bool>(type: "bit", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FoundItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FoundItems_Departments_FoundByDepartmentId",
                        column: x => x.FoundByDepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FoundItems_Hotels_HotelId",
                        column: x => x.HotelId,
                        principalTable: "Hotels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FoundItems_ListItems_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "ListItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FoundItems_ListItems_ColorId",
                        column: x => x.ColorId,
                        principalTable: "ListItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FoundItems_LocationAreas_AreaId",
                        column: x => x.AreaId,
                        principalTable: "LocationAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FoundItems_Users_ClosedById",
                        column: x => x.ClosedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FoundItems_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FoundItems_Users_FoundByUserId",
                        column: x => x.FoundByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LostReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    HotelId = table.Column<int>(type: "int", nullable: false),
                    ReportedLocal = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OwnerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Room = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ArrivalDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DepartureDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CategoryId = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ColorId = table.Column<int>(type: "int", nullable: true),
                    Brand = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LostWhere = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LostFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    LostTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    FoundItemId = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ForHandover = table.Column<bool>(type: "bit", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LostReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LostReports_FoundItems_FoundItemId",
                        column: x => x.FoundItemId,
                        principalTable: "FoundItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LostReports_Hotels_HotelId",
                        column: x => x.HotelId,
                        principalTable: "Hotels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LostReports_ListItems_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "ListItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LostReports_ListItems_ColorId",
                        column: x => x.ColorId,
                        principalTable: "ListItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LostReports_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Shipments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FoundItemId = table.Column<int>(type: "int", nullable: false),
                    HotelId = table.Column<int>(type: "int", nullable: false),
                    RecipientName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Street = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    State = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Zip = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Country = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Carrier = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    TrackingNumber = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Cost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Payment = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PaymentNote = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ShippedLocal = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedById = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shipments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Shipments_FoundItems_FoundItemId",
                        column: x => x.FoundItemId,
                        principalTable: "FoundItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Shipments_Hotels_HotelId",
                        column: x => x.HotelId,
                        principalTable: "Hotels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Shipments_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FoundItems_AreaId",
                table: "FoundItems",
                column: "AreaId");

            migrationBuilder.CreateIndex(
                name: "IX_FoundItems_CategoryId",
                table: "FoundItems",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_FoundItems_ClosedById",
                table: "FoundItems",
                column: "ClosedById");

            migrationBuilder.CreateIndex(
                name: "IX_FoundItems_ColorId",
                table: "FoundItems",
                column: "ColorId");

            migrationBuilder.CreateIndex(
                name: "IX_FoundItems_CreatedById",
                table: "FoundItems",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_FoundItems_FoundByDepartmentId",
                table: "FoundItems",
                column: "FoundByDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_FoundItems_FoundByUserId",
                table: "FoundItems",
                column: "FoundByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FoundItems_FoundLocal",
                table: "FoundItems",
                column: "FoundLocal");

            migrationBuilder.CreateIndex(
                name: "IX_FoundItems_HotelId_Status_PurgeDate",
                table: "FoundItems",
                columns: new[] { "HotelId", "Status", "PurgeDate" });

            migrationBuilder.CreateIndex(
                name: "IX_FoundItems_Number",
                table: "FoundItems",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LostReports_CategoryId",
                table: "LostReports",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_LostReports_ColorId",
                table: "LostReports",
                column: "ColorId");

            migrationBuilder.CreateIndex(
                name: "IX_LostReports_CreatedById",
                table: "LostReports",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_LostReports_FoundItemId",
                table: "LostReports",
                column: "FoundItemId");

            migrationBuilder.CreateIndex(
                name: "IX_LostReports_HotelId_Status",
                table: "LostReports",
                columns: new[] { "HotelId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LostReports_Number",
                table: "LostReports",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_CreatedById",
                table: "Shipments",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_FoundItemId",
                table: "Shipments",
                column: "FoundItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_HotelId",
                table: "Shipments",
                column: "HotelId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LostReports");

            migrationBuilder.DropTable(
                name: "Shipments");

            migrationBuilder.DropTable(
                name: "FoundItems");
        }
    }
}
