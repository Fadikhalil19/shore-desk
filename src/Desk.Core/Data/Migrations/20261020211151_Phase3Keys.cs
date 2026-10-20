using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desk.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase3Keys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KeyCounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HotelId = table.Column<int>(type: "int", nullable: false),
                    ShiftDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Shift = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CountedById = table.Column<int>(type: "int", nullable: false),
                    CountedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KeyCounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KeyCounts_Hotels_HotelId",
                        column: x => x.HotelId,
                        principalTable: "Hotels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KeyCounts_Users_CountedById",
                        column: x => x.CountedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KeyCheckouts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KeyId = table.Column<int>(type: "int", nullable: false),
                    HotelId = table.Column<int>(type: "int", nullable: false),
                    ToType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Company = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    OutLocal = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueBackLocal = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OutById = table.Column<int>(type: "int", nullable: false),
                    OutSignature = table.Column<byte[]>(type: "varbinary(max)", maxLength: -1, nullable: true),
                    OutNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    InLocal = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InById = table.Column<int>(type: "int", nullable: true),
                    InSignature = table.Column<byte[]>(type: "varbinary(max)", maxLength: -1, nullable: true),
                    InNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KeyCheckouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KeyCheckouts_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KeyCheckouts_Hotels_HotelId",
                        column: x => x.HotelId,
                        principalTable: "Hotels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KeyCheckouts_Users_InById",
                        column: x => x.InById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KeyCheckouts_Users_OutById",
                        column: x => x.OutById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KeyCheckouts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Keys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HotelId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Barcode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CurrentCheckoutId = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Keys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Keys_Hotels_HotelId",
                        column: x => x.HotelId,
                        principalTable: "Hotels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Keys_KeyCheckouts_CurrentCheckoutId",
                        column: x => x.CurrentCheckoutId,
                        principalTable: "KeyCheckouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KeyCountLine",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KeyCountId = table.Column<int>(type: "int", nullable: false),
                    KeyId = table.Column<int>(type: "int", nullable: false),
                    ExpectedStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    OnHand = table.Column<bool>(type: "bit", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KeyCountLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KeyCountLine_KeyCounts_KeyCountId",
                        column: x => x.KeyCountId,
                        principalTable: "KeyCounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KeyCountLine_Keys_KeyId",
                        column: x => x.KeyId,
                        principalTable: "Keys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KeyStatusChanges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KeyId = table.Column<int>(type: "int", nullable: false),
                    HotelId = table.Column<int>(type: "int", nullable: false),
                    From = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    To = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ById = table.Column<int>(type: "int", nullable: false),
                    AtLocal = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KeyStatusChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KeyStatusChanges_Keys_KeyId",
                        column: x => x.KeyId,
                        principalTable: "Keys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KeyStatusChanges_Users_ById",
                        column: x => x.ById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KeyCheckouts_DepartmentId",
                table: "KeyCheckouts",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_KeyCheckouts_HotelId_OutLocal",
                table: "KeyCheckouts",
                columns: new[] { "HotelId", "OutLocal" });

            migrationBuilder.CreateIndex(
                name: "IX_KeyCheckouts_InById",
                table: "KeyCheckouts",
                column: "InById");

            migrationBuilder.CreateIndex(
                name: "IX_KeyCheckouts_KeyId",
                table: "KeyCheckouts",
                column: "KeyId");

            migrationBuilder.CreateIndex(
                name: "IX_KeyCheckouts_OutById",
                table: "KeyCheckouts",
                column: "OutById");

            migrationBuilder.CreateIndex(
                name: "IX_KeyCheckouts_UserId",
                table: "KeyCheckouts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_KeyCountLine_KeyCountId",
                table: "KeyCountLine",
                column: "KeyCountId");

            migrationBuilder.CreateIndex(
                name: "IX_KeyCountLine_KeyId",
                table: "KeyCountLine",
                column: "KeyId");

            migrationBuilder.CreateIndex(
                name: "IX_KeyCounts_CountedById",
                table: "KeyCounts",
                column: "CountedById");

            migrationBuilder.CreateIndex(
                name: "IX_KeyCounts_HotelId_ShiftDate_Shift",
                table: "KeyCounts",
                columns: new[] { "HotelId", "ShiftDate", "Shift" });

            migrationBuilder.CreateIndex(
                name: "IX_Keys_CurrentCheckoutId",
                table: "Keys",
                column: "CurrentCheckoutId");

            migrationBuilder.CreateIndex(
                name: "IX_Keys_HotelId_Barcode",
                table: "Keys",
                columns: new[] { "HotelId", "Barcode" },
                unique: true,
                filter: "[Barcode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Keys_HotelId_Name",
                table: "Keys",
                columns: new[] { "HotelId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KeyStatusChanges_ById",
                table: "KeyStatusChanges",
                column: "ById");

            migrationBuilder.CreateIndex(
                name: "IX_KeyStatusChanges_KeyId",
                table: "KeyStatusChanges",
                column: "KeyId");

            migrationBuilder.AddForeignKey(
                name: "FK_KeyCheckouts_Keys_KeyId",
                table: "KeyCheckouts",
                column: "KeyId",
                principalTable: "Keys",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_KeyCheckouts_Keys_KeyId",
                table: "KeyCheckouts");

            migrationBuilder.DropTable(
                name: "KeyCountLine");

            migrationBuilder.DropTable(
                name: "KeyStatusChanges");

            migrationBuilder.DropTable(
                name: "KeyCounts");

            migrationBuilder.DropTable(
                name: "Keys");

            migrationBuilder.DropTable(
                name: "KeyCheckouts");
        }
    }
}
