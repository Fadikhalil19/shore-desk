using System.Net;
using System.Text.Json;
using Desk.Core.Data;
using Desk.Core.Incidents;
using Microsoft.EntityFrameworkCore;
using MsShiftImport;

namespace Desk.Api.Tests;

// The MSShift importer (tools/MsShiftImport) on parsed records. All data is made up.
public class ImportTests(DeskAppFactory app) : IClassFixture<DeskAppFactory>, IAsyncLifetime
{
    public Task InitializeAsync() => app.EnsureDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    const string Parsed = """
    [
      {"form":"IR","number":"90001","status":"Closed","type":"Noise Disturbance","occurred":"2024-03-02T23:15",
       "reported":"2024-03-02T23:20","written":"2024-03-03T01:00","location":"Guest Room, 214",
       "narrative":"Test narrative one.","reportedBy":"Pat Example","writtenBy":"Sam Officer","reviewedBy":"Lee Boss",
       "subject":{"name":"Alex Q Sample","type":"Guest","room":"214","stay":"03/01/2024 - 03/04/2024"},
       "witnesses":[{"name":"Robin Test","phone":"555-0100"}],
       "vehicle":{"make":"Testmobile","plate":"TEST123","color":"Purple Sparkle"}},
      {"form":"GAI","number":"90002","status":"Deleted","type":"Guest Illness","occurred":"2023-07-04T09:00",
       "written":"2023-07-04T10:00","location":"OVH, Lobby","narrative":"Test narrative two.",
       "injury":{"injuryType":"Made Up Injury","bodyPart":"Ankle"},"medical":{"ems":"Yes","rma":"No","hospital":"Test General"},
       "subject":{"name":"Jo Sample","type":"Guest","dateOfBirth":"01/02/1990","home":{"City":"Testville"}}},
      {"form":"EAI","number":"90003","status":"Pending","type":"Employee Accident","occurred":"2025-01-10T14:00",
       "written":"2025-01-10T15:00","location":"Somewhere unknown","narrative":"Test narrative three.",
       "subject":{"name":"Casey Worker","department":"SHO-Housekeeping","position":"Room attendant"}}
    ]
    """;

    [Fact]
    public async Task Imports_maps_and_removes_again()
    {
        var file = Path.Combine(Path.GetTempPath(), $"msshift-test-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(file, Parsed);
        try
        {
            await using (var db = app.Db())
            {
                var imp = new Importer(db);
                await imp.PrepareAsync();
                var res = await new IncidentImport(imp).RunAsync(file);
                Assert.Equal(3, res.Added);
            }

            await using (var db = app.Db())
            {
                var rows = await db.Incidents.Include(r => r.Hotel).Include(r => r.Area).Include(r => r.Type)
                    .Where(r => r.LegacyRef!.StartsWith("MSShift ")).OrderBy(r => r.Number).ToListAsync();
                Assert.Equal(["MS-90001", "MS-90002", "MS-90003"], rows.Select(r => r.Number));

                var ir = rows[0];
                Assert.Equal(IncidentStatus.Approved, ir.Status);
                Assert.Equal("Noise Disturbance", ir.Type!.Name);
                Assert.Equal("Guest Room", ir.Area!.Name);
                Assert.Equal("214", ir.Room);
                Assert.Equal(Shift.Overnight, ir.Shift);
                Assert.Equal("MSShift IR 90001", ir.LegacyRef);
                var d = IncidentDetails.Parse(ir.DetailsJson);
                Assert.Equal("Alex Q", d.Subjects[0].FirstName);
                Assert.Equal("Sample", d.Subjects[0].LastName);
                Assert.Equal(new DateOnly(2024, 3, 4), d.Subjects[0].DepartureDate);
                Assert.Equal("Sam Officer", d.WrittenByOther);
                Assert.Equal("Lee Boss", d.SupervisorOther);
                Assert.Equal("TEST123", d.Vehicle!.Plate);
                var color = await db.ListItems.SingleAsync(i => i.Id == d.Vehicle.ColorId);
                Assert.False(color.IsActive);                       // new entries come in switched off
                Assert.Contains("Robin Test", ir.PeopleSearch);

                var gai = rows[1];
                Assert.Equal(IncidentStatus.Voided, gai.Status);
                Assert.Equal("OVH", gai.Hotel.Code);
                Assert.Equal("Illness", gai.Kind);
                var gd = IncidentDetails.Parse(gai.DetailsJson);
                Assert.True(gd.Medical!.EmsCalled);
                Assert.False(gd.Medical.RefusedMedicalAttention);
                Assert.Equal(new DateOnly(1990, 1, 2), gd.Subjects[0].DateOfBirth);

                var eai = rows[2];
                Assert.Equal(IncidentStatus.Draft, eai.Status);
                Assert.Null(eai.AreaId);
                Assert.Equal("Somewhere unknown", eai.LocationDetail);
                var ed = IncidentDetails.Parse(eai.DetailsJson);
                var housekeeping = await db.Departments.SingleAsync(x => x.Name == "Housekeeping");
                Assert.Equal(housekeeping.Id, ed.Subjects[0].DepartmentId);
                Assert.Equal(PersonTypes.Employee, ed.Subjects[0].Type);

                var importUser = await db.Users.SingleAsync(u => u.EntraObjectId == Importer.ImportUserGuid);
                Assert.NotNull(importUser.BlockedUtc);
                Assert.Equal(importUser.Id, ir.WrittenById);

                // The app shows an imported report like any other.
                var view = await app.Admin().GetAsync($"/api/incidents/{ir.Id}");
                Assert.Equal(HttpStatusCode.OK, view.StatusCode);
            }

            // Running it again adds nothing; --remove takes the import out.
            await using (var db = app.Db())
            {
                var imp = new Importer(db);
                await imp.PrepareAsync();
                Assert.Equal(0, (await new IncidentImport(imp).RunAsync(file)).Added);
                Assert.Equal(3, (await Importer.RemoveAsync(db)).incidents);
                Assert.False(await db.Incidents.AnyAsync(r => r.LegacyRef != null));
            }
        }
        finally
        {
            File.Delete(file);
        }
    }

    const string ParsedEvents = """
    [
      {"number":"EV#24-0001","category":"Guest Assist Luggage","locationType":"SH A Building 1st Floor",
       "locationDetail":"Test Spot","shift":"Afternoon","priority":"High","started":"2024-05-01T14:05",
       "completed":"2024-05-01T14:30","enteredBy":"Sam Officer","assignedTo":"Lee Boss (Safety)",
       "requested":"2024-05-01T14:00","requestedByName":"Pat Example","requestedByType":"Guest","requestedByDetail":"Room 101"},
      {"number":"EV#24-0002","category":"Totally New Thing","locationType":"OVH","started":"2024-05-02T23:00",
       "priority":"Low","narrative":"Made-up narrative."}
    ]
    """;

    [Fact]
    public async Task Imports_events_closes_old_open_ones()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"msshift-events-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "events-2024-05.json"), ParsedEvents);
        try
        {
            await using (var db = app.Db())
            {
                var imp = new Importer(db);
                await imp.PrepareAsync();
                Assert.Equal(2, (await new EventImport(imp).RunAsync(dir)).Added);
            }

            await using (var db = app.Db())
            {
                var rows = await db.Events.Include(e => e.Category).Include(e => e.Hotel).Include(e => e.Area)
                    .Where(e => e.LegacyRef!.StartsWith("MSShift EV#24-")).OrderBy(e => e.LegacyRef).ToListAsync();
                var a = rows[0];
                Assert.Equal("Guest Assist", a.Category.Name);           // matched by the start of the text
                Assert.Equal("Guest Assist Luggage", a.Detail);          // full MSShift text kept
                Assert.Equal("A Building 1st Floor", a.Area!.Name);
                Assert.Equal(Desk.Core.Events.Priority.High, a.Priority);
                Assert.Equal(Shift.Afternoon, a.Shift);
                Assert.Equal("Guest", a.RequestedByType);
                Assert.Equal("Pat Example (Room 101)", a.RequestedByName);
                Assert.Contains("Entered by (MSShift): Sam Officer", a.Narrative);

                var b = rows[1];
                Assert.Equal("OVH", b.Hotel.Code);
                Assert.False(b.Category.IsActive);                       // unknown category added switched off
                Assert.Equal(b.StartedLocal, b.CompletedLocal);          // left open in MSShift -> closed
                Assert.Contains("Still open in MSShift", b.Narrative);

                var view = await app.Admin().GetAsync($"/api/events/{a.Id}");
                Assert.Equal(HttpStatusCode.OK, view.StatusCode);
            }

            await using (var db = app.Db())
                Assert.Equal(2, (await Importer.RemoveAsync(db)).events);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
