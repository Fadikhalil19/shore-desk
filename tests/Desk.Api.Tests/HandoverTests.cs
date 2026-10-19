using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Desk.Api.Endpoints;
using Desk.Core.Data;
using Desk.Core.Events;
using Desk.Core.Handover;
using Desk.Core.Incidents;

namespace Desk.Api.Tests;

// Handover through the real API, plus the shift arithmetic. All data is made up.
public class HandoverTests(DeskAppFactory app) : IClassFixture<DeskAppFactory>, IAsyncLifetime
{
    const int Safety = 18, Sho = 1, Smh = 2;
    static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public Task InitializeAsync() => app.EnsureDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    async Task<(int Id, HttpClient Client)> Person(params (string Module, string Level)[] grants)
    {
        var oid = Guid.NewGuid();
        var r = await app.Admin().PostAsJsonAsync("/api/admin/users",
            new AdminEndpoints.NewUserRequest(oid, $"Person {oid:N}"[..15], $"{oid:N}@shorehotel.com", Safety, Sho));
        var id = (await r.Content.ReadFromJsonAsync<AdminEndpoints.UserDto>())!.Id;
        foreach (var (m, l) in grants)
            (await app.Admin().PutAsJsonAsync($"/api/admin/users/{id}/access/{m}", new AdminEndpoints.GrantDto(l, null, "All", null))).EnsureSuccessStatusCode();
        return (id, app.As(oid));
    }

    static readonly DateOnly Day = new(2026, 9, 10);

    [Theory]
    [InlineData(2026, 9, 10, 4, 2026, 9, 9, Shift.Overnight)]
    [InlineData(2026, 9, 10, 5, 2026, 9, 10, Shift.Morning)]
    [InlineData(2026, 9, 10, 21, 2026, 9, 10, Shift.Overnight)]
    [InlineData(2026, 9, 10, 23, 2026, 9, 10, Shift.Overnight)]
    public void Overnight_belongs_to_the_day_it_started(int y, int m, int d, int h, int ey, int em, int ed, Shift es) =>
        Assert.Equal((new DateOnly(ey, em, ed), es), HandoverRules.ShiftOf(new DateTime(y, m, d, h, 30, 0)));

    [Fact]
    public void Previous_shift_wraps_to_yesterday()
    {
        Assert.Equal((Day.AddDays(-1), Shift.Overnight), HandoverRules.Previous(Day, Shift.Morning));
        Assert.Equal((Day, Shift.Afternoon), HandoverRules.Previous(Day, Shift.Overnight));
        Assert.Equal((Day.ToDateTime(new TimeOnly(21, 0)), Day.AddDays(1).ToDateTime(new TimeOnly(5, 0))), HandoverRules.Window(Day, Shift.Overnight));
    }

    [Fact]
    public async Task Notes_flagged_items_and_reads()
    {
        var (_, writer) = await Person(("handover", "Edit"), ("eventlog", "Edit"), ("incidents", "Edit"));
        var (_, reader) = await Person(("handover", "View"));
        var (_, other) = await Person(("handover", "Edit"));

        // A note on the Overnight shift of Sept 10, and an event + incident in that shift, flagged.
        var n = await writer.PostAsJsonAsync("/api/handover/notes", new HandoverEndpoints.NoteRequest(Sho, Day, Shift.Overnight, "Pool gate lock is broken - check every hour."), J);
        var noteId = (await n.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("id").GetInt32();

        var lists = await app.Admin().GetFromJsonAsync<MeEndpoints.LookupsDto>("/api/lookups");
        var cat = lists!.ListItems.First(i => i.ListKey == ListKeys.EventCategory).Id;
        var night = Day.ToDateTime(new TimeOnly(23, 15));
        var ev = await writer.PostAsJsonAsync("/api/events", new EventEndpoints.SaveRequest(Sho, cat, "Made-up", null, null, null, null, null,
            null, null, null, night, null, null, null, Priority.Medium, null, null, night, null, "Made-up test event"), J);
        var evId = (await ev.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("id").GetInt32();
        (await writer.PostAsJsonAsync($"/api/events/{evId}/handover", new EventEndpoints.FlagRequest(true), J)).EnsureSuccessStatusCode();

        var ir = await writer.PostAsJsonAsync("/api/incidents", new IncidentEndpoints.SaveRequest(IncidentForm.Incident, Sho, null, null, null,
            "Made-up noise", Day.AddDays(1).ToDateTime(new TimeOnly(1, 0)), null, null, null, null, null, null, null, null, null, null, null), J);
        var irId = (await ir.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("id").GetInt32();
        (await writer.PostAsJsonAsync($"/api/incidents/{irId}/handover", new IncidentEndpoints.FlagRequest(true), J)).EnsureSuccessStatusCode();

        var h = await writer.GetFromJsonAsync<HandoverEndpoints.HandoverDto>($"/api/handover?hotelId={Sho}&date=2026-09-10&shift=Overnight", J);
        Assert.Single(h!.Notes);
        Assert.True(h.Notes[0].CanEdit);
        Assert.Contains(h.Events!, e => e.Id == evId);
        Assert.Contains(h.OpenEvents!, e => e.Id == evId);
        Assert.Contains(h.Incidents!, i => i.Id == irId);          // 01:00 next day is still the Overnight shift

        // The reader (handover only) sees the note but not the event or incident lists.
        var hr = await reader.GetFromJsonAsync<HandoverEndpoints.HandoverDto>($"/api/handover?hotelId={Sho}&date=2026-09-10&shift=Overnight", J);
        Assert.Single(hr!.Notes);
        Assert.Null(hr.Events);
        Assert.Null(hr.Incidents);
        Assert.False(hr.CanWrite);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsJsonAsync("/api/handover/notes",
            new HandoverEndpoints.NoteRequest(Sho, Day, Shift.Overnight, "x"), J)).StatusCode);

        // Someone else cannot change the writer's note.
        Assert.Equal(HttpStatusCode.BadRequest, (await other.PutAsJsonAsync($"/api/handover/notes/{noteId}", new HandoverEndpoints.EditNoteRequest("changed"), J)).StatusCode);

        // Marking read twice keeps one record.
        var shift = new HandoverEndpoints.ShiftRequest(Sho, Day, Shift.Overnight);
        (await reader.PostAsJsonAsync("/api/handover/read", shift, J)).EnsureSuccessStatusCode();
        (await reader.PostAsJsonAsync("/api/handover/read", shift, J)).EnsureSuccessStatusCode();
        hr = await reader.GetFromJsonAsync<HandoverEndpoints.HandoverDto>($"/api/handover?hotelId={Sho}&date=2026-09-10&shift=Overnight", J);
        Assert.True(hr!.ReadByMe);
        Assert.Single(hr.Reads);

        // Unflagged items leave the handover.
        (await writer.PostAsJsonAsync($"/api/events/{evId}/handover", new EventEndpoints.FlagRequest(false), J)).EnsureSuccessStatusCode();
        h = await writer.GetFromJsonAsync<HandoverEndpoints.HandoverDto>($"/api/handover?hotelId={Sho}&date=2026-09-10&shift=Overnight", J);
        Assert.DoesNotContain(h!.Events!, e => e.Id == evId);
    }

    [Fact]
    public async Task Hotel_limit_applies_to_handover()
    {
        var oid = Guid.NewGuid();
        var r = await app.Admin().PostAsJsonAsync("/api/admin/users", new AdminEndpoints.NewUserRequest(oid, "Smh Only", null, Safety, Smh));
        var id = (await r.Content.ReadFromJsonAsync<AdminEndpoints.UserDto>())!.Id;
        (await app.Admin().PutAsJsonAsync($"/api/admin/users/{id}/access/handover", new AdminEndpoints.GrantDto("Edit", Smh, "All", null))).EnsureSuccessStatusCode();
        var c = app.As(oid);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/handover?hotelId={Sho}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync($"/api/handover?hotelId={Smh}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsJsonAsync("/api/handover/notes",
            new HandoverEndpoints.NoteRequest(Sho, Day, Shift.Morning, "x"), J)).StatusCode);
    }
}
