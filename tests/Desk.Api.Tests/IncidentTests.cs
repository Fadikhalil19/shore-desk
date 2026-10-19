using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Desk.Api.Endpoints;
using Desk.Core.Data;
using Desk.Core.Incidents;
using Microsoft.EntityFrameworkCore;

namespace Desk.Api.Tests;

// Incident reports through the real API: access per form and hotel, the
// draft -> review -> approved flow, voiding, and photos. All data is made up.
public class IncidentTests(DeskAppFactory app) : IClassFixture<DeskAppFactory>, IAsyncLifetime
{
    const int Safety = 18, Sho = 1, Smh = 2;
    const int ShoArea = 1;           // "A Building 1st Floor" (SHO)
    const int SmhArea = 15;          // "Santa Monica Hotel"

    static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public Task InitializeAsync() => app.EnsureDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    async Task<(int Id, Guid Oid)> NewUser(string name, int? department = Safety, int? hotel = Sho)
    {
        var oid = Guid.NewGuid();
        var r = await app.Admin().PostAsJsonAsync("/api/admin/users",
            new AdminEndpoints.NewUserRequest(oid, name, $"{name.Replace(" ", "")}{oid:N}@shorehotel.com", department, hotel));
        r.EnsureSuccessStatusCode();
        return ((await r.Content.ReadFromJsonAsync<AdminEndpoints.UserDto>())!.Id, oid);
    }

    async Task Grant(int userId, string module, string level, int? hotel = null) =>
        (await app.Admin().PutAsJsonAsync($"/api/admin/users/{userId}/access/{module}",
            new AdminEndpoints.GrantDto(level, hotel, "All", null))).EnsureSuccessStatusCode();

    async Task<int> ListId(string key, string name)
    {
        var l = await app.Admin().GetFromJsonAsync<MeEndpoints.LookupsDto>("/api/lookups");
        return l!.ListItems.Single(i => i.ListKey == key && i.Name == name).Id;
    }

    // An officer (Edit) and their supervisor (Manage) in the incidents module.
    async Task<(HttpClient Officer, int OfficerId, HttpClient Super, int SuperId)> Team(string module = "incidents")
    {
        var (oid, o) = await NewUser($"Officer {Guid.NewGuid():N}"[..14]);
        var (sid, s) = await NewUser($"Super {Guid.NewGuid():N}"[..12]);
        await Grant(oid, module, "Edit");
        await Grant(sid, module, "Manage");
        return (app.As(o), oid, app.As(s), sid);
    }

    static IncidentEndpoints.SaveRequest Draft(IncidentForm form = IncidentForm.Incident, int hotel = Sho, int? area = null,
        int? type = null, int? supervisor = null, string? narrative = null, IncidentDetails? details = null, string? kind = null) =>
        new(form, hotel, type, kind, null, "Guest locked out of room", new DateTime(2026, 10, 1, 14, 30, 0), null, null,
            area, null, "Near elevator", "214", narrative, null, details, supervisor, null);

    async Task<int> Create(HttpClient c, IncidentEndpoints.SaveRequest req)
    {
        var r = await c.PostAsJsonAsync("/api/incidents", req, J);
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
        return (await r.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("id").GetInt32();
    }

    static async Task<IncidentEndpoints.IncidentDto> Get(HttpClient c, int id) =>
        (await c.GetFromJsonAsync<IncidentEndpoints.IncidentDto>($"/api/incidents/{id}", J))!;

    [Fact]
    public async Task Draft_then_review_then_approved_and_locked()
    {
        var (officer, officerId, super, superId) = await Team();
        var type = await ListId(ListKeys.IncidentType, "Noise Disturbance");

        // A draft can be saved half-finished...
        var id = await Create(officer, Draft());
        var d = await Get(officer, id);
        Assert.Equal(IncidentStatus.Draft, d.Status);
        Assert.Matches(@"^\d{4}-\d{4}$", d.Number);
        Assert.Equal(Shift.Afternoon, d.Shift);                 // 14:30
        Assert.Contains("Statement of facts", d.Missing);

        // ...but cannot go to the supervisor until it is complete.
        Assert.Equal(HttpStatusCode.BadRequest, (await officer.PostAsync($"/api/incidents/{id}/submit", null)).StatusCode);

        (await officer.PutAsJsonAsync($"/api/incidents/{id}",
            Draft(area: ShoArea, type: type, supervisor: superId, narrative: "Loud music in 214; asked to turn it down."), J))
            .EnsureSuccessStatusCode();
        (await officer.PostAsync($"/api/incidents/{id}/submit", null)).EnsureSuccessStatusCode();

        // Waiting for review: the officer can no longer change it; the supervisor sees it in their queue.
        Assert.False((await Get(officer, id)).Can.Edit);
        var queue = await super.GetFromJsonAsync<IncidentEndpoints.IncidentPage>("/api/incidents?toReview=true", J);
        Assert.Contains(queue!.Items, r => r.Id == id);

        (await super.PostAsJsonAsync($"/api/incidents/{id}/approve", new IncidentEndpoints.NoteRequest(null), J)).EnsureSuccessStatusCode();
        var approved = await Get(officer, id);
        Assert.Equal(IncidentStatus.Approved, approved.Status);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await officer.PutAsJsonAsync($"/api/incidents/{id}", Draft(area: ShoArea, type: type, supervisor: superId, narrative: "changed"), J)).StatusCode);

        await using var db = app.Db();
        var actions = await db.ActivityLog.Where(a => a.EntityType == "IncidentReport" && a.EntityId == d.Number)
            .Select(a => a.Action).ToListAsync();
        Assert.Contains("Create", actions);
        Assert.Contains("View", actions);
        Assert.Contains("Edit", actions);
        Assert.Contains("Submit", actions);
        Assert.Contains("Approve", actions);
        // The edit log names the fields, never the narrative text itself.
        Assert.DoesNotContain(await db.ActivityLog.Select(a => a.Details).ToListAsync(), x => x != null && x.Contains("Loud music"));
        _ = officerId;
    }

    [Fact]
    public async Task Supervisor_must_manage_that_kind_of_report()
    {
        var (officer, officerId, _, _) = await Team();
        var r = await officer.PostAsJsonAsync("/api/incidents", Draft(supervisor: officerId), J);   // officer only has Edit
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Supervisor_list_only_for_forms_you_may_write()
    {
        var (officer, _, _, superId) = await Team();
        var list = await officer.GetFromJsonAsync<List<IncidentEndpoints.PersonOption>>($"/api/incidents/reviewers?form=Incident&hotelId={Sho}", J);
        Assert.Contains(list!, p => p.Id == superId);
        Assert.Equal(HttpStatusCode.Forbidden, (await officer.GetAsync($"/api/incidents/reviewers?form=HighLevel&hotelId={Sho}")).StatusCode);
    }

    [Fact]
    public async Task Sending_back_needs_a_note_and_returns_it_to_the_writer()
    {
        var (officer, _, super, superId) = await Team();
        var type = await ListId(ListKeys.IncidentType, "Informational");
        var id = await Create(officer, Draft(area: ShoArea, type: type, supervisor: superId, narrative: "Made-up test narrative."));
        (await officer.PostAsync($"/api/incidents/{id}/submit", null)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.BadRequest,
            (await super.PostAsJsonAsync($"/api/incidents/{id}/return", new IncidentEndpoints.NoteRequest(""), J)).StatusCode);
        (await super.PostAsJsonAsync($"/api/incidents/{id}/return", new IncidentEndpoints.NoteRequest("Add the guest's name"), J))
            .EnsureSuccessStatusCode();
        var d = await Get(officer, id);
        Assert.Equal(IncidentStatus.Draft, d.Status);
        Assert.Equal("Add the guest's name", d.ReviewNote);
        Assert.True(d.Can.Edit);
    }

    [Fact]
    public async Task A_manager_cannot_approve_their_own_report()
    {
        var (_, _, super, superId) = await Team();
        var (_, other) = await NewUser("Second Supervisor");
        var type = await ListId(ListKeys.IncidentType, "Informational");
        var id = await Create(super, Draft(area: ShoArea, type: type, supervisor: superId, narrative: "Test."));
        (await super.PostAsync($"/api/incidents/{id}/submit", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest,
            (await super.PostAsJsonAsync($"/api/incidents/{id}/approve", new IncidentEndpoints.NoteRequest(null), J)).StatusCode);
        _ = other;
    }

    [Fact]
    public async Task Sensitive_reports_need_their_own_access()
    {
        var (officer, _, _, _) = await Team();
        // Officer has incidents Edit, but nothing in employee-injury or High Level.
        Assert.Equal(HttpStatusCode.Forbidden,
            (await officer.PostAsJsonAsync("/api/incidents", Draft(IncidentForm.EmployeeInjury, kind: "Accident"), J)).StatusCode);

        var hl = await Create(app.Admin(), Draft(IncidentForm.HighLevel));
        Assert.Equal(HttpStatusCode.NotFound, (await officer.GetAsync($"/api/incidents/{hl}")).StatusCode);
        var list = await officer.GetFromJsonAsync<IncidentEndpoints.IncidentPage>("/api/incidents", J);
        Assert.DoesNotContain(list!.Items, r => r.Id == hl);
    }

    [Fact]
    public async Task Hotel_limit_hides_other_hotels_reports()
    {
        var (smhId, smhOid) = await NewUser("Sam Smh", hotel: Smh);
        await Grant(smhId, "incidents", "Edit", hotel: Smh);
        var sho = await Create(app.Admin(), Draft(area: ShoArea));
        var smh = await Create(app.Admin(), Draft(hotel: Smh, area: SmhArea));

        var c = app.As(smhOid);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/incidents/{sho}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync($"/api/incidents/{smh}")).StatusCode);
        var list = await c.GetFromJsonAsync<IncidentEndpoints.IncidentPage>("/api/incidents", J);
        Assert.Contains(list!.Items, r => r.Id == smh);
        Assert.DoesNotContain(list.Items, r => r.Id == sho);
        // Nor can they file one for SHO.
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsJsonAsync("/api/incidents", Draft(), J)).StatusCode);
    }

    [Fact]
    public async Task Location_must_belong_to_the_reports_hotel() =>
        Assert.Equal(HttpStatusCode.BadRequest,
            (await app.Admin().PostAsJsonAsync("/api/incidents", Draft(hotel: Smh, area: ShoArea), J)).StatusCode);

    [Fact]
    public async Task Search_finds_people_by_name()
    {
        var details = new IncidentDetails { Subjects = [new PersonEntry { Type = PersonTypes.Guest, FirstName = "Zed", LastName = "Quimbly" }] };
        var id = await Create(app.Admin(), Draft(details: details));
        var list = await app.Admin().GetFromJsonAsync<IncidentEndpoints.IncidentPage>("/api/incidents?q=Quimbly", J);
        Assert.Equal([id], list!.Items.Select(r => r.Id));
    }

    [Fact]
    public async Task Void_needs_the_number_typed_and_keeps_the_report()
    {
        var id = await Create(app.Admin(), Draft());
        var number = (await Get(app.Admin(), id)).Number;
        Assert.Equal(HttpStatusCode.BadRequest,
            (await app.Admin().PostAsJsonAsync($"/api/incidents/{id}/void", new IncidentEndpoints.VoidRequest("Duplicate", "nope"), J)).StatusCode);
        (await app.Admin().PostAsJsonAsync($"/api/incidents/{id}/void", new IncidentEndpoints.VoidRequest("Duplicate", number), J))
            .EnsureSuccessStatusCode();

        var d = await Get(app.Admin(), id);
        Assert.Equal(IncidentStatus.Voided, d.Status);
        var normal = await app.Admin().GetFromJsonAsync<IncidentEndpoints.IncidentPage>("/api/incidents", J);
        Assert.DoesNotContain(normal!.Items, r => r.Id == id);
        var voided = await app.Admin().GetFromJsonAsync<IncidentEndpoints.IncidentPage>("/api/incidents?status=Voided", J);
        Assert.Contains(voided!.Items, r => r.Id == id);
    }

    [Fact]
    public async Task Photos_are_added_downloaded_and_removed()
    {
        var (officer, _, _, _) = await Team();
        var id = await Create(officer, Draft());

        static MultipartFormDataContent File(string name, byte[] bytes)
        {
            var f = new ByteArrayContent(bytes);
            f.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            return new MultipartFormDataContent { { f, "files", name } };
        }

        Assert.Equal(HttpStatusCode.BadRequest,
            (await officer.PostAsync($"/api/incidents/{id}/attachments", File("run-me.exe", [1, 2, 3]))).StatusCode);
        (await officer.PostAsync($"/api/incidents/{id}/attachments", File("door.jpg", [0xFF, 0xD8, 0xFF, 1, 2]))).EnsureSuccessStatusCode();

        var a = Assert.Single((await Get(officer, id)).Attachments);
        Assert.Equal("door.jpg", a.FileName);
        Assert.True(a.Inline);
        var download = await officer.GetAsync($"/api/incidents/{id}/attachments/{a.Id}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal([0xFF, 0xD8, 0xFF, 1, 2], await download.Content.ReadAsByteArrayAsync());

        // Someone without access to the report cannot fetch the file either.
        var (_, stranger) = await NewUser("No Access");
        Assert.Equal(HttpStatusCode.Forbidden, (await app.As(stranger).GetAsync($"/api/incidents/{id}/attachments/{a.Id}")).StatusCode);

        (await officer.DeleteAsync($"/api/incidents/{id}/attachments/{a.Id}")).EnsureSuccessStatusCode();
        Assert.Empty((await Get(officer, id)).Attachments);
        await using var db = app.Db();
        Assert.True(await db.ActivityLog.AnyAsync(x => x.Action == "Download" && x.EntityType == "IncidentReport"));
        Assert.NotNull((await db.IncidentAttachments.SingleAsync(x => x.Id == a.Id)).RemovedUtc);   // hidden, not deleted
    }

    [Fact]
    public async Task Admin_edits_a_report_list_and_duplicates_are_refused()
    {
        var admin = app.Admin();
        (await admin.PostAsJsonAsync("/api/admin/lists/items", new ListEndpoints.ListItemRequest(ListKeys.InjuryType, "Whiplash", 500)))
            .EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PostAsJsonAsync("/api/admin/lists/items", new ListEndpoints.ListItemRequest(ListKeys.InjuryType, "Whiplash", 510))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PostAsJsonAsync("/api/admin/lists/items", new ListEndpoints.ListItemRequest("not-a-list", "X", 1))).StatusCode);
    }
}
