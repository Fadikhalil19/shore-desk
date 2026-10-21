using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desk.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase3Packages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Packages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    HotelId = table.Column<int>(type: "int", nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ReceivedLocal = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Carrier = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PackageType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    TrackingNumber = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Count = table.Column<int>(type: "int", nullable: false),
                    Weight = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    HandlingFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PersonType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PersonName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PersonUserId = table.Column<int>(type: "int", nullable: true),
                    Room = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Company = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ArrivalDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DepartureDate = table.Column<DateOnly>(type: "date", nullable: true),
                    GroupName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OtherParty = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    StorageLocation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NotifiedLocal = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NotifiedHow = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    NotifiedById = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ClosedLocal = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedToName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Signature = table.Column<byte[]>(type: "varbinary(max)", maxLength: -1, nullable: true),
                    ClosedById = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ForHandover = table.Column<bool>(type: "bit", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Packages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Packages_Hotels_HotelId",
                        column: x => x.HotelId,
                        principalTable: "Hotels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Packages_Users_ClosedById",
                        column: x => x.ClosedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Packages_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Packages_Users_NotifiedById",
                        column: x => x.NotifiedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Packages_Users_PersonUserId",
                        column: x => x.PersonUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Packages_ClosedById",
                table: "Packages",
                column: "ClosedById");

            migrationBuilder.CreateIndex(
                name: "IX_Packages_CreatedById",
                table: "Packages",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Packages_HotelId_Status",
                table: "Packages",
                columns: new[] { "HotelId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Packages_NotifiedById",
                table: "Packages",
                column: "NotifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_Packages_Number",
                table: "Packages",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Packages_PersonUserId",
                table: "Packages",
                column: "PersonUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Packages_TrackingNumber",
                table: "Packages",
                column: "TrackingNumber");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Packages");
        }
    }
}
