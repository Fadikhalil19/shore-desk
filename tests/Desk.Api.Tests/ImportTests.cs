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
        var files = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"msshift-files-{Guid.NewGuid():N}")).FullName;
        var storage = Path.Combine(Path.GetTempPath(), $"msshift-storage-{Guid.NewGuid():N}");
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

            // Photos / PDFs read out of MSShift become attachments of the imported report.
            await File.WriteAllBytesAsync(Path.Combine(files, "IR-90001-photo1.jpg"), [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3]);
            await File.WriteAllBytesAsync(Path.Combine(files, "IR-90001-pdf1.pdf"), "%PDF-1.4 test"u8.ToArray());
            await File.WriteAllBytesAsync(Path.Combine(files, "IR-99999-photo1.jpg"), [0xFF, 0xD8]);   // no such report
            await using (var db = app.Db())
            {
                var imp = new Importer(db);
                await imp.PrepareAsync();
                var res = await new AttachmentImport(imp, storage).RunAsync(files);
                Assert.Equal(2, res.Added);
                Assert.Equal(1, res.NoReport);
                Assert.Equal(0, (await new AttachmentImport(imp, storage).RunAsync(files)).Added);   // rerun adds nothing
                var ir = await db.Incidents.SingleAsync(r => r.LegacyRef == "MSShift IR 90001");
                var atts = await db.IncidentAttachments.Where(a => a.IncidentId == ir.Id).OrderBy(a => a.FileName).ToListAsync();
                Assert.Equal(["MSShift pdf1.pdf", "MSShift photo1.jpg"], atts.Select(a => a.FileName));
                Assert.True(File.Exists(Path.Combine(storage, "incidents", ir.Id.ToString(), atts[1].StoredName)));
            }

            // Running it again adds nothing; --remove takes the import out (attachments and their files too).
            await using (var db = app.Db())
            {
                var imp = new Importer(db);
                await imp.PrepareAsync();
                Assert.Equal(0, (await new IncidentImport(imp).RunAsync(file)).Added);
                Assert.Equal(3, (await Importer.RemoveAsync(db, storage)).incidents);
                Assert.False(await db.Incidents.AnyAsync(r => r.LegacyRef != null));
                Assert.Empty(Directory.GetDirectories(Path.Combine(storage, "incidents")));
            }
        }
        finally
        {
            File.Delete(file);
            Directory.Delete(files, true);
            if (Directory.Exists(storage)) Directory.Delete(storage, true);
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

    const string ParsedFound = """
    [
      {"number":"99-0001","site":"1515 Test Ave","category":"Clothing","detail":"Jacket","color":"Testy Teal",
       "description":"Made-up jacket.","found":"2026-02-03T10:15","locationFound":"Guest Room, 305",
       "foundByName":"Sam Finder","foundByDepartment":"SHO-Housekeeping","received":"2026-02-03T10:30",
       "receivedBy":"Lee Desk","storage":"Shelf / B2","wantsItem":"No"},
      {"number":"99-0002","category":"Phone","found":"2025-03-01T08:00","received":"2025-03-01T08:05",
       "returned":"2025-03-02T12:00","returnTo":"Pat Owner","returnToType":"Guest","returnedBy":"Lee Desk"},
      {"number":"99-0003","category":"Umbrella","found":"2025-01-01T08:00","returned":"2025-04-01T09:00",
       "disposition":"Donated"},
      {"number":"99-0004","category":"Bag","found":"2025-01-05T08:00","shipped":"2025-01-09T09:00",
       "returnTo":"Jo Away","carrier":"UPS","shipCity":"Testville"},
      {"number":"99-0005","category":"Hat","found":"2024-07-01T08:00"},
      {"category":"No number","found":"2025-01-05T08:00"}
    ]
    """;

    [Fact]
    public async Task Imports_found_items_with_their_outcome()
    {
        var file = Path.Combine(Path.GetTempPath(), $"msshift-found-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(file, ParsedFound);
        try
        {
            await using (var db = app.Db())
            {
                var imp = new Importer(db);
                await imp.PrepareAsync();
                var res = await new FoundImport(imp).RunAsync(file);
                Assert.Equal(5, res.Added);
                Assert.Equal(1, res.NoNumber);
                Assert.Equal(0, (await new FoundImport(imp).RunAsync(file)).Added);    // rerun adds nothing
            }

            await using (var db = app.Db())
            {
                var rows = await db.FoundItems.Where(f => f.LegacyRef!.StartsWith("MSShift F 99-")).OrderBy(f => f.Number).ToListAsync();
                Assert.Equal(["MS-99-0001", "MS-99-0002", "MS-99-0003", "MS-99-0004", "MS-99-0005"], rows.Select(f => f.Number));

                var kept = rows[0];
                Assert.Equal(Desk.Core.LostFound.FoundStatus.InStorage, kept.Status);
                Assert.Equal("305", kept.Room);
                Assert.Equal(new DateOnly(2026, 5, 4), kept.PurgeDate);            // found + 90 days
                Assert.Equal("Shelf / B2", kept.StorageLocation);
                var housekeeping = await db.Departments.SingleAsync(x => x.Name == "Housekeeping");
                Assert.Equal(housekeeping.Id, kept.FoundByDepartmentId);
                Assert.Contains("Received by (MSShift): Lee Desk", kept.Notes);
                Assert.False((await db.ListItems.SingleAsync(i => i.Id == kept.ColorId)).IsActive);

                Assert.Equal(Desk.Core.LostFound.FoundStatus.ReturnedToOwner, rows[1].Status);
                Assert.Equal("Pat Owner", rows[1].ReturnedToName);
                Assert.Equal(Desk.Core.LostFound.FoundStatus.Disposed, rows[2].Status);
                Assert.Equal("Donated", rows[2].DisposalMethod);
                Assert.Equal(Desk.Core.LostFound.FoundStatus.Shipped, rows[3].Status);
                Assert.Contains("UPS", rows[3].CloseNote);
                // Never closed in MSShift and found before 2026: closed by the import on its purge date.
                Assert.Equal(Desk.Core.LostFound.FoundStatus.Disposed, rows[4].Status);
                Assert.Equal(new DateTime(2024, 9, 29), rows[4].ClosedLocal);
                Assert.Contains("closed by the import", rows[4].CloseNote);

                var view = await app.Admin().GetAsync($"/api/lostfound/found/{kept.Id}");
                Assert.Equal(HttpStatusCode.OK, view.StatusCode);

                Assert.Equal(5, (await Importer.RemoveAsync(db)).found);
            }
        }
        finally
        {
            File.Delete(file);
        }
    }
    [Fact]
    public async Task Imports_on_duty_and_call_outs()
    {
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"msshift-duty-{Guid.NewGuid():N}")).FullName;
        await File.WriteAllTextAsync(Path.Combine(dir, "onduty.json"), """
        [{"number":"TD#991","name":"Sam Officer","shift":"Overnight","radio":"7 Test 2","post":"Overnight",
          "start":"2025-06-02T01:00","end":"2025-06-02T05:00","enteredBy":"Lee Boss"},
         {"number":"TD#992","name":"Robin Guard","shift":"Morning","post":"Pool deck","start":"2025-06-02T05:00","end":"2025-06-02T13:00"}]
        """);
        await File.WriteAllTextAsync(Path.Combine(dir, "callout.json"), """
        [{"number":"TC#25-991","date":"2025-06-03T00:00","shift":"Afternoon","name":"Casey Worker","department":"Safety",
          "reason":"Family Emergency","called":"2025-06-03T10:00","takenBy":"Lee Boss"},
         {"number":"TC#25-992","date":"2025-06-04T00:00","shift":"Morning","name":"Jo Worker","reason":"Jury Duty"}]
        """);
        try
        {
            await using var db = app.Db();
            var imp = new Importer(db);
            await imp.PrepareAsync();
            var res = await new DutyImport(imp).RunAsync(Path.Combine(dir, "onduty.json"), Path.Combine(dir, "callout.json"));
            Assert.Equal(2, res.DutyAdded);
            Assert.Equal(2, res.CallsAdded);
            Assert.Equal(0, (await new DutyImport(imp).RunAsync(Path.Combine(dir, "onduty.json"), Path.Combine(dir, "callout.json"))).DutyAdded);

            var night = await db.DutyAssignments.SingleAsync(d => d.LegacyRef == "MSShift TD#991");
            Assert.Equal(new DateOnly(2025, 6, 1), night.ShiftDate);              // after midnight: the night before
            Assert.Null(night.Post);                                              // post only repeated the shift
            Assert.Equal("Pool deck", (await db.DutyAssignments.SingleAsync(d => d.LegacyRef == "MSShift TD#992")).Post);

            var calls = await db.CallOuts.Where(c => c.LegacyRef!.StartsWith("MSShift TC#25-99")).OrderBy(c => c.LegacyRef).ToListAsync();
            Assert.Equal("Family emergency", calls[0].Reason);
            Assert.Contains("Taken by (MSShift): Lee Boss", calls[0].Notes);
            Assert.Equal("Other", calls[1].Reason);
            Assert.Contains("Reason in MSShift: Jury Duty", calls[1].Notes);

            Assert.Equal(4, (await Importer.RemoveAsync(db)).duty);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
