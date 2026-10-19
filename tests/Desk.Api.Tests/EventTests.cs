using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Desk.Api.Endpoints;
using Desk.Core.Data;
using Desk.Core.Events;
using Microsoft.EntityFrameworkCore;

namespace Desk.Api.Tests;

// Event log and reminders through the real API. All data is made up.
public class EventTests(DeskAppFactory app) : IClassFixture<DeskAppFactory>, IAsyncLifetime
{
    const int Safety = 18, Sho = 1, Smh = 2, ShoArea = 1;
    static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public Task InitializeAsync() => app.EnsureDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    async Task<(int Id, HttpClient Client)> Officer(string level = "Edit", int? hotelLimit = null)
    {
        var oid = Guid.NewGuid();
        var r = await app.Admin().PostAsJsonAsync("/api/admin/users",
            new AdminEndpoints.NewUserRequest(oid, $"Officer {oid:N}"[..16], $"{oid:N}@shorehotel.com", Safety, Sho));
        var id = (await r.Content.ReadFromJsonAsync<AdminEndpoints.UserDto>())!.Id;
        (await app.Admin().PutAsJsonAsync($"/api/admin/users/{id}/access/eventlog",
            new AdminEndpoints.GrantDto(level, hotelLimit, "All", null))).EnsureSuccessStatusCode();
        return (id, app.As(oid));
    }

    async Task<int> Category(string name) =>
        (await app.Admin().GetFromJsonAsync<MeEndpoints.LookupsDto>("/api/lookups"))!
            .ListItems.Single(i => i.ListKey == ListKeys.EventCategory && i.Name == name).Id;

    async Task<EventEndpoints.SaveRequest> Entry(int hotel = Sho, int? area = ShoArea, string narrative = "Made-up lockout, room 305", DateTime? started = null) =>
        new(hotel, await Category("Lockout"), "Guest locked out", area, null, null, "305", "Guest", "Test Guest", null, null,
            null, null, null, null, Priority.Medium, Safety, null, started ?? DateTime.Now.AddMinutes(-30), null, narrative);

    async Task<int> Create(HttpClient c, EventEndpoints.SaveRequest req)
    {
        var r = await c.PostAsJsonAsync("/api/events", req, J);
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
        return (await r.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task Officer_logs_an_event_and_the_next_shift_completes_it()
    {
        var (_, officer) = await Officer();
        var (_, nextShift) = await Officer();
        var id = await Create(officer, await Entry());

        var open = await nextShift.GetFromJsonAsync<EventEndpoints.EventPage>("/api/events?open=true", J);
        Assert.Contains(open!.Items, e => e.Id == id);

        // The next shift may close it, but not rewrite someone else's entry.
        var dto = await nextShift.GetFromJsonAsync<EventEndpoints.EventDto>($"/api/events/{id}", J);
        Assert.True(dto!.Can.Complete);
        Assert.False(dto.Can.Edit);
        Assert.Equal(HttpStatusCode.BadRequest, (await nextShift.PutAsJsonAsync($"/api/events/{id}", await Entry(narrative: "changed"), J)).StatusCode);

        (await nextShift.PostAsJsonAsync($"/api/events/{id}/complete", new EventEndpoints.CompleteRequest(null), J)).EnsureSuccessStatusCode();
        open = await nextShift.GetFromJsonAsync<EventEndpoints.EventPage>("/api/events?open=true", J);
        Assert.DoesNotContain(open!.Items, e => e.Id == id);

        await using var db = app.Db();
        var actions = await db.ActivityLog.Where(a => a.EntityType == "Event" && a.EntityId == id.ToString()).Select(a => a.Action).ToListAsync();
        Assert.Equal(["Complete", "Create", "View"], actions.Distinct().Order());
    }

    [Fact]
    public async Task View_only_cannot_log_and_no_access_is_refused()
    {
        var (_, viewer) = await Officer("View");
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync("/api/events", await Entry(), J)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync("/api/events")).StatusCode);

        var oid = Guid.NewGuid();
        await app.Admin().PostAsJsonAsync("/api/admin/users", new AdminEndpoints.NewUserRequest(oid, "Nobody Here", null, null, null));
        Assert.Equal(HttpStatusCode.Forbidden, (await app.As(oid).GetAsync("/api/events")).StatusCode);
    }

    [Fact]
    public async Task Hotel_limit_applies()
    {
        var (_, smhOfficer) = await Officer(hotelLimit: Smh);
        var sho = await Create(app.Admin(), await Entry());
        Assert.Equal(HttpStatusCode.NotFound, (await smhOfficer.GetAsync($"/api/events/{sho}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await smhOfficer.PostAsJsonAsync("/api/events", await Entry(), J)).StatusCode);
        var list = await smhOfficer.GetFromJsonAsync<EventEndpoints.EventPage>("/api/events?open=true", J);
        Assert.DoesNotContain(list!.Items, e => e.Id == sho);
    }

    [Fact]
    public async Task Bad_entries_are_refused()
    {
        var admin = app.Admin();
        var good = await Entry();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/events", good with { CategoryId = 1 }, J)).StatusCode); // an incident type
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/events", good with { HotelId = Smh }, J)).StatusCode);   // SHO area
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/events",
            good with { CompletedLocal = good.StartedLocal!.Value.AddHours(-2) }, J)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/events",
            good with { StartedLocal = DateTime.Now.AddDays(2) }, J)).StatusCode);
    }

    [Fact]
    public async Task Void_needs_a_reason_and_hides_the_entry()
    {
        var id = await Create(app.Admin(), await Entry());
        Assert.Equal(HttpStatusCode.BadRequest, (await app.Admin().PostAsJsonAsync($"/api/events/{id}/void", new EventEndpoints.VoidRequest(" "), J)).StatusCode);
        (await app.Admin().PostAsJsonAsync($"/api/events/{id}/void", new EventEndpoints.VoidRequest("Logged twice"), J)).EnsureSuccessStatusCode();
        var open = await app.Admin().GetFromJsonAsync<EventEndpoints.EventPage>("/api/events?open=true", J);
        Assert.DoesNotContain(open!.Items, e => e.Id == id);
        var voided = await app.Admin().GetFromJsonAsync<EventEndpoints.EventPage>("/api/events?voided=true&open=true", J);
        Assert.Contains(voided!.Items, e => e.Id == id);
    }

    [Fact]
    public async Task Search_finds_text()
    {
        var id = await Create(app.Admin(), await Entry(narrative: "Zebra-striped umbrella left at pool"));
        var found = await app.Admin().GetFromJsonAsync<EventEndpoints.EventPage>("/api/events?q=Zebra-striped", J);
        Assert.Equal([id], found!.Items.Select(e => e.Id));
    }

    [Fact]
    public async Task Daily_reminder_moves_to_the_next_day_and_one_off_closes()
    {
        var (_, officer) = await Officer();
        var due = DateTime.Now.AddHours(-1);
        var daily = await officer.PostAsJsonAsync("/api/events/reminders",
            new EventEndpoints.ReminderRequest(Sho, "Check pool gate", null, due, true, null), J);
        var dailyId = (await daily.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("id").GetInt32();
        var once = await officer.PostAsJsonAsync("/api/events/reminders",
            new EventEndpoints.ReminderRequest(Sho, "Call locksmith", null, due, false, null), J);
        var onceId = (await once.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("id").GetInt32();

        var list = await officer.GetFromJsonAsync<List<EventEndpoints.ReminderDto>>("/api/events/reminders", J);
        Assert.True(list!.Single(r => r.Id == dailyId).Overdue);

        (await officer.PostAsync($"/api/events/reminders/{dailyId}/done", null)).EnsureSuccessStatusCode();
        (await officer.PostAsync($"/api/events/reminders/{onceId}/done", null)).EnsureSuccessStatusCode();
        list = await officer.GetFromJsonAsync<List<EventEndpoints.ReminderDto>>("/api/events/reminders?all=true", J);
        var moved = list!.Single(r => r.Id == dailyId);
        Assert.False(moved.Overdue);
        Assert.Equal(due.AddDays(1).Date, moved.DueLocal.Date);
        Assert.DoesNotContain(list!, r => r.Id == onceId);
    }

    [Fact]
    public void Next_due_skips_to_the_future()
    {
        var due = new DateTime(2026, 10, 1, 22, 0, 0);
        Assert.Equal(new DateTime(2026, 10, 5, 22, 0, 0), EventRules.NextDue(due, new DateTime(2026, 10, 4, 23, 0, 0)));
    }
}
