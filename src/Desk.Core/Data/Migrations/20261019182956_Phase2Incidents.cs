using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Desk.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase2Incidents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAdmin",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ListItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ListKey = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Incidents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Form = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    HotelId = table.Column<int>(type: "int", nullable: false),
                    TypeId = table.Column<int>(type: "int", nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    OtherType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Heading = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OccurredLocal = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReportedLocal = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Shift = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    AreaId = table.Column<int>(type: "int", nullable: true),
                    LocationId = table.Column<int>(type: "int", nullable: true),
                    LocationDetail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Room = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Narrative = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: true),
                    FollowUp = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: true),
                    DetailsJson = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: false),
                    PeopleSearch = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    WrittenById = table.Column<int>(type: "int", nullable: false),
                    SupervisorId = table.Column<int>(type: "int", nullable: true),
                    DistributedTo = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedById = table.Column<int>(type: "int", nullable: true),
                    ReviewedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    VoidedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Incidents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Incidents_Hotels_HotelId",
                        column: x => x.HotelId,
                        principalTable: "Hotels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Incidents_ListItems_TypeId",
                        column: x => x.TypeId,
                        principalTable: "ListItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Incidents_LocationAreas_AreaId",
                        column: x => x.AreaId,
                        principalTable: "LocationAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Incidents_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Incidents_Users_ReviewedById",
                        column: x => x.ReviewedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Incidents_Users_SupervisorId",
                        column: x => x.SupervisorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Incidents_Users_WrittenById",
                        column: x => x.WrittenById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IncidentAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IncidentId = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    StoredName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Caption = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    UploadedById = table.Column<int>(type: "int", nullable: false),
                    UploadedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RemovedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RemovedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncidentAttachments_Incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "Incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IncidentAttachments_Users_RemovedById",
                        column: x => x.RemovedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IncidentAttachments_Users_UploadedById",
                        column: x => x.UploadedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "ListItems",
                columns: new[] { "Id", "IsActive", "ListKey", "Name", "SortOrder" },
                values: new object[,]
                {
                    { 1, true, "incident-type", "Alleged Bug Bites", 10 },
                    { 2, true, "incident-type", "Blackout/Power Outage", 20 },
                    { 3, true, "incident-type", "Bugs in Room", 30 },
                    { 4, true, "incident-type", "Burglary", 40 },
                    { 5, true, "incident-type", "Chemical Fumes", 50 },
                    { 6, true, "incident-type", "Chemical Spill", 60 },
                    { 7, true, "incident-type", "Cyber Security Breach", 70 },
                    { 8, true, "incident-type", "Damaged Property", 80 },
                    { 9, true, "incident-type", "Defrauding an Innkeeper - Arrest", 90 },
                    { 10, true, "incident-type", "Drill", 100 },
                    { 11, true, "incident-type", "Earthquake", 110 },
                    { 12, true, "incident-type", "Elevator Entrapment", 120 },
                    { 13, true, "incident-type", "Employee Opened Guest Door with Key Card", 130 },
                    { 14, true, "incident-type", "Employee Violation - Altercation", 140 },
                    { 15, true, "incident-type", "Employee Violation - Key Control", 150 },
                    { 16, true, "incident-type", "Employee Violation - Other", 160 },
                    { 17, true, "incident-type", "Employee Violation - Traffic/Parking", 170 },
                    { 18, true, "incident-type", "Employee Violation - Unauthorized Entry", 180 },
                    { 19, true, "incident-type", "Eviction - Non-Registered Guest", 190 },
                    { 20, true, "incident-type", "Eviction - Registered Guest", 200 },
                    { 21, true, "incident-type", "Fire", 210 },
                    { 22, true, "incident-type", "Fire/Smoke Detector Alarm", 220 },
                    { 23, true, "incident-type", "Flood", 230 },
                    { 24, true, "incident-type", "Gasoline Leak", 240 },
                    { 25, true, "incident-type", "Guest Altercation", 250 },
                    { 26, true, "incident-type", "Guest Harassing/Abuse to Staff/Other", 260 },
                    { 27, true, "incident-type", "Guest Incident", 270 },
                    { 28, true, "incident-type", "Guest Intoxication", 280 },
                    { 29, true, "incident-type", "Guest Traffic/Parking Violation", 290 },
                    { 30, true, "incident-type", "In-House Guest Not Properly Checked In", 300 },
                    { 31, true, "incident-type", "Informational", 310 },
                    { 32, true, "incident-type", "Informational - No Crime", 320 },
                    { 33, true, "incident-type", "Missing Money", 330 },
                    { 34, true, "incident-type", "Missing Person - Adult", 340 },
                    { 35, true, "incident-type", "Missing Person - Child", 350 },
                    { 36, true, "incident-type", "Natural Gas Leak", 360 },
                    { 37, true, "incident-type", "Noise Disturbance", 370 },
                    { 38, true, "incident-type", "Possible Bed Bugs", 380 },
                    { 39, true, "incident-type", "Possible Missing Friend of Guest", 390 },
                    { 40, true, "incident-type", "Property Missing/Damaged", 400 },
                    { 41, true, "incident-type", "Safety Hazard", 410 },
                    { 42, true, "incident-type", "Scam/Fraud", 420 },
                    { 43, true, "incident-type", "Smoking in Guest Room", 430 },
                    { 44, true, "incident-type", "Smoking in Unauthorized Area", 440 },
                    { 45, true, "incident-type", "Suspicious Odor", 450 },
                    { 46, true, "incident-type", "Trespassing - Warned, No Arrest", 460 },
                    { 47, true, "incident-type", "Trespassing - Arrest", 470 },
                    { 48, true, "incident-type", "Unknown Substance", 480 },
                    { 49, true, "incident-type", "Unpaid/Defrauding an Innkeeper", 490 },
                    { 50, true, "incident-type", "Vandalism", 500 },
                    { 51, true, "incident-type", "Welfare Check", 510 },
                    { 52, true, "incident-type", "Welfare Check in Room", 520 },
                    { 53, true, "highlevel-type", "Assault", 10 },
                    { 54, true, "highlevel-type", "Assault with Weapon", 20 },
                    { 55, true, "highlevel-type", "BOLO", 30 },
                    { 56, true, "highlevel-type", "Bomb Threat", 40 },
                    { 57, true, "highlevel-type", "Burglary", 50 },
                    { 58, true, "highlevel-type", "Computer Room Breach", 60 },
                    { 59, true, "highlevel-type", "Death/Suicide/Murder", 70 },
                    { 60, true, "highlevel-type", "Defrauding an Innkeeper - Arrest", 80 },
                    { 61, true, "highlevel-type", "Domestic Assault", 90 },
                    { 62, true, "highlevel-type", "Employee Violation - Drugs", 100 },
                    { 63, true, "highlevel-type", "Employee Violation - Harassment", 110 },
                    { 64, true, "highlevel-type", "Employee Violation - Liquor", 120 },
                    { 65, true, "highlevel-type", "Fire Alarm", 130 },
                    { 66, true, "highlevel-type", "Fraud", 140 },
                    { 67, true, "highlevel-type", "Guest Harassing/Abuse", 150 },
                    { 68, true, "highlevel-type", "Identity Theft/Fraud", 160 },
                    { 69, true, "highlevel-type", "Phone Call Impersonation", 170 },
                    { 70, true, "highlevel-type", "Robbery", 180 },
                    { 71, true, "highlevel-type", "Sexual Assault", 190 },
                    { 72, true, "highlevel-type", "Sexual Offense", 200 },
                    { 73, true, "highlevel-type", "SMS Impersonation", 210 },
                    { 74, true, "highlevel-type", "System Breach", 220 },
                    { 75, true, "highlevel-type", "Theft (over $950)", 230 },
                    { 76, true, "highlevel-type", "Threats/Harassment", 240 },
                    { 77, true, "highlevel-type", "Trespassing - Arrest", 250 },
                    { 78, true, "injury-type", "Abrasion/Scrape", 10 },
                    { 79, true, "injury-type", "Allergic Reaction", 20 },
                    { 80, true, "injury-type", "Bite/Sting", 30 },
                    { 81, true, "injury-type", "Bruise/Contusion", 40 },
                    { 82, true, "injury-type", "Burn", 50 },
                    { 83, true, "injury-type", "Concussion/Head Injury", 60 },
                    { 84, true, "injury-type", "Cut/Laceration", 70 },
                    { 85, true, "injury-type", "Dislocation", 80 },
                    { 86, true, "injury-type", "Eye Injury", 90 },
                    { 87, true, "injury-type", "Food Illness", 100 },
                    { 88, true, "injury-type", "Fracture", 110 },
                    { 89, true, "injury-type", "Illness - Other", 120 },
                    { 90, true, "injury-type", "Pain/Soreness", 130 },
                    { 91, true, "injury-type", "Puncture", 140 },
                    { 92, true, "injury-type", "Sprain/Strain", 150 },
                    { 93, true, "injury-type", "Other", 160 },
                    { 94, true, "body-part", "Ankle", 10 },
                    { 95, true, "body-part", "Arm", 20 },
                    { 96, true, "body-part", "Back", 30 },
                    { 97, true, "body-part", "Chest", 40 },
                    { 98, true, "body-part", "Ear", 50 },
                    { 99, true, "body-part", "Elbow", 60 },
                    { 100, true, "body-part", "Eye", 70 },
                    { 101, true, "body-part", "Face", 80 },
                    { 102, true, "body-part", "Finger", 90 },
                    { 103, true, "body-part", "Foot", 100 },
                    { 104, true, "body-part", "Hand", 110 },
                    { 105, true, "body-part", "Head", 120 },
                    { 106, true, "body-part", "Hip", 130 },
                    { 107, true, "body-part", "Knee", 140 },
                    { 108, true, "body-part", "Leg", 150 },
                    { 109, true, "body-part", "Mouth", 160 },
                    { 110, true, "body-part", "Neck", 170 },
                    { 111, true, "body-part", "Nose", 180 },
                    { 112, true, "body-part", "Shoulder", 190 },
                    { 113, true, "body-part", "Stomach", 200 },
                    { 114, true, "body-part", "Teeth", 210 },
                    { 115, true, "body-part", "Toe", 220 },
                    { 116, true, "body-part", "Wrist", 230 },
                    { 117, true, "body-part", "Multiple/Whole Body", 240 },
                    { 118, true, "body-part-location", "Back", 10 },
                    { 119, true, "body-part-location", "Both", 20 },
                    { 120, true, "body-part-location", "Face", 30 },
                    { 121, true, "body-part-location", "Front", 40 },
                    { 122, true, "body-part-location", "Head-Top", 50 },
                    { 123, true, "body-part-location", "Left", 60 },
                    { 124, true, "body-part-location", "Left Dominant", 70 },
                    { 125, true, "body-part-location", "Lower", 80 },
                    { 126, true, "body-part-location", "Right", 90 },
                    { 127, true, "body-part-location", "Right Dominant", 100 },
                    { 128, true, "body-part-location", "Upper", 110 },
                    { 129, true, "contributing-factor", "Slip/Fall", 10 },
                    { 130, true, "contributing-factor", "Trip/Fall", 20 },
                    { 131, true, "contributing-factor", "Slippery Floor", 30 },
                    { 132, true, "contributing-factor", "Uneven Concrete", 40 },
                    { 133, true, "contributing-factor", "Lack of PPE", 50 },
                    { 134, true, "contributing-factor", "Unsafe Work Practice", 60 },
                    { 135, true, "contributing-factor", "Unsafe Working Condition", 70 },
                    { 136, true, "contributing-factor", "Push/Pull/Lift", 80 },
                    { 137, true, "contributing-factor", "Struck by Object", 90 },
                    { 138, true, "contributing-factor", "Chemical", 100 },
                    { 139, true, "contributing-factor", "Inhalation", 110 },
                    { 140, true, "contributing-factor", "Ingestion", 120 },
                    { 141, true, "contributing-factor", "Medical Condition", 130 },
                    { 142, true, "contributing-factor", "Weather", 140 },
                    { 143, true, "contributing-factor", "Weapon", 150 },
                    { 144, true, "contributing-factor", "Motor Vehicle Collision", 160 },
                    { 145, true, "contributing-factor", "Needle/Sharp", 170 },
                    { 146, true, "contributing-factor", "Other", 180 },
                    { 147, true, "property-category", "Alcohol", 10 },
                    { 148, true, "property-category", "Appliance", 20 },
                    { 149, true, "property-category", "Art", 30 },
                    { 150, true, "property-category", "Baby", 40 },
                    { 151, true, "property-category", "Beauty/Hygiene", 50 },
                    { 152, true, "property-category", "Bedding", 60 },
                    { 153, true, "property-category", "Books", 70 },
                    { 154, true, "property-category", "Cell Phone", 80 },
                    { 155, true, "property-category", "Clothing", 90 },
                    { 156, true, "property-category", "Computer", 100 },
                    { 157, true, "property-category", "Dishware", 110 },
                    { 158, true, "property-category", "Dog & Cat", 120 },
                    { 159, true, "property-category", "Electronics", 130 },
                    { 160, true, "property-category", "Glasses", 140 },
                    { 161, true, "property-category", "Jewelry", 150 },
                    { 162, true, "property-category", "Luggage", 160 },
                    { 163, true, "property-category", "Medication", 170 },
                    { 164, true, "property-category", "Misc", 180 },
                    { 165, true, "property-category", "Money", 190 },
                    { 166, true, "property-category", "Musical Equipment", 200 },
                    { 167, true, "property-category", "Office Supplies", 210 },
                    { 168, true, "property-category", "Passport", 220 },
                    { 169, true, "property-category", "Personal Items", 230 },
                    { 170, true, "property-category", "Shoes", 240 },
                    { 171, true, "property-category", "Sport", 250 },
                    { 172, true, "property-category", "Toys", 260 },
                    { 173, true, "property-category", "Weapons", 270 },
                    { 174, true, "property-category", "Vehicle", 280 },
                    { 175, true, "property-category", "Hotel Property", 290 },
                    { 176, true, "property-category", "Other", 300 },
                    { 177, true, "color", "Beige", 10 },
                    { 178, true, "color", "Black", 20 },
                    { 179, true, "color", "Blue", 30 },
                    { 180, true, "color", "Brown", 40 },
                    { 181, true, "color", "Burgundy", 50 },
                    { 182, true, "color", "Clear", 60 },
                    { 183, true, "color", "Copper", 70 },
                    { 184, true, "color", "Cream", 80 },
                    { 185, true, "color", "Gold", 90 },
                    { 186, true, "color", "Gray", 100 },
                    { 187, true, "color", "Green", 110 },
                    { 188, true, "color", "Multi-Color", 120 },
                    { 189, true, "color", "Navy", 130 },
                    { 190, true, "color", "Orange", 140 },
                    { 191, true, "color", "Pink", 150 },
                    { 192, true, "color", "Purple", 160 },
                    { 193, true, "color", "Red", 170 },
                    { 194, true, "color", "Silver", 180 },
                    { 195, true, "color", "Tan", 190 },
                    { 196, true, "color", "Teal", 200 },
                    { 197, true, "color", "Turquoise", 210 },
                    { 198, true, "color", "White", 220 },
                    { 199, true, "color", "Yellow", 230 },
                    { 200, true, "color", "Other", 240 },
                    { 201, true, "vehicle-circumstance", "Abandoned", 10 },
                    { 202, true, "vehicle-circumstance", "Hit and Run", 20 },
                    { 203, true, "vehicle-circumstance", "Stolen", 30 },
                    { 204, true, "vehicle-circumstance", "Towed", 40 },
                    { 205, true, "vehicle-circumstance", "Vehicle Break-in", 50 },
                    { 206, true, "vehicle-circumstance", "Damaged by Guest", 60 },
                    { 207, true, "vehicle-circumstance", "Damaged by Associate", 70 },
                    { 208, true, "vehicle-circumstance", "Other", 80 },
                    { 209, true, "evidence-status", "In Storage", 10 },
                    { 210, true, "evidence-status", "Sent to Claims", 20 },
                    { 211, true, "evidence-status", "Sent to Attorney", 30 },
                    { 212, true, "evidence-status", "Destroyed", 40 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_IncidentAttachments_IncidentId",
                table: "IncidentAttachments",
                column: "IncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentAttachments_RemovedById",
                table: "IncidentAttachments",
                column: "RemovedById");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentAttachments_UploadedById",
                table: "IncidentAttachments",
                column: "UploadedById");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_AreaId",
                table: "Incidents",
                column: "AreaId");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_HotelId",
                table: "Incidents",
                column: "HotelId");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_LocationId",
                table: "Incidents",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_Number",
                table: "Incidents",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_OccurredLocal",
                table: "Incidents",
                column: "OccurredLocal");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_ReviewedById",
                table: "Incidents",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_Status_SupervisorId",
                table: "Incidents",
                columns: new[] { "Status", "SupervisorId" });

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_SupervisorId",
                table: "Incidents",
                column: "SupervisorId");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_TypeId",
                table: "Incidents",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_WrittenById",
                table: "Incidents",
                column: "WrittenById");

            migrationBuilder.CreateIndex(
                name: "IX_ListItems_ListKey_Name",
                table: "ListItems",
                columns: new[] { "ListKey", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IncidentAttachments");

            migrationBuilder.DropTable(
                name: "Incidents");

            migrationBuilder.DropTable(
                name: "ListItems");

            migrationBuilder.DropColumn(
                name: "IsAdmin",
                table: "Users");
        }
    }
}
