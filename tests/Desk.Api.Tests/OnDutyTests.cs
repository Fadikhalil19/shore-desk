using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Desk.Api.Endpoints;
using Desk.Core.Incidents;
using Desk.Core.OnDuty;

namespace Desk.Api.Tests;

// Roster and call-outs through the real API. All data is made up.
public class OnDutyTests(DeskAppFactory app) : IClassFixture<DeskAppFactory>, IAsyncLifetime
{
    const int Safety = 18, Sho = 1, Smh = 2;
    static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    static readonly DateOnly Day = new(2026, 9, 15);

    public Task InitializeAsync() => app.EnsureDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    async Task<(int Id, HttpClient Client)> Person(string? level, int? hotelLimit = null, string module = "onduty")
    {
        var oid = Guid.NewGuid();
        var r = await app.Admin().PostAsJsonAsync("/api/admin/users",
            new AdminEndpoints.NewUserRequest(oid, $"Person {oid:N}"[..15], $"{oid:N}@shorehotel.com", Safety, Sho));
        var id = (await r.Content.ReadFromJsonAsync<AdminEndpoints.UserDto>())!.Id;
        if (level is not null)
            (await app.Admin().PutAsJsonAsync($"/api/admin/users/{id}/access/{module}", new AdminEndpoints.GrantDto(level, hotelLimit, "All", null))).EnsureSuccessStatusCode();
        return (id, app.As(oid));
    }

    static OnDutyEndpoints.AssignmentRequest Slot(int? userId, string? name = null, DateOnly? day = null, int startHour = 13, int hours = 8) =>
        new(Sho, day ?? Day, Shift.Afternoon, userId, name, Safety, "R-4", "Lobby",
            (day ?? Day).ToDateTime(new TimeOnly(startHour, 0)), (day ?? Day).ToDateTime(new TimeOnly(startHour, 0)).AddHours(hours));

    static string Url(DateOnly d) => $"/api/onduty?hotelId={Sho}&date={d:yyyy-MM-dd}&shift=Afternoon";

    [Fact]
    public async Task Roster_add_twice_refused_copy_and_remove()
    {
        var (_, sup) = await Person("Edit");
        var (officerId, _) = await Person(null);

        (await sup.PostAsJsonAsync("/api/onduty/assignments", Slot(officerId), J)).EnsureSuccessStatusCode();
        (await sup.PostAsJsonAsync("/api/onduty/assignments", Slot(null, "Agency Guard"), J)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await sup.PostAsJsonAsync("/api/onduty/assignments", Slot(officerId), J)).StatusCode);

        var r = await sup.GetFromJsonAsync<OnDutyEndpoints.RosterDto>(Url(Day), J);
        Assert.Equal(2, r!.Roster.Count);
        Assert.Contains(r.Roster, a => a.Name == "Agency Guard" && a.Post == "Lobby");

        // Copy to the next day: both copied, times moved one day; copying again adds nobody.
        var next = Day.AddDays(1);
        var copy = await sup.PostAsJsonAsync("/api/onduty/copy", new OnDutyEndpoints.CopyRequest(Sho, next, Shift.Afternoon, Day, Shift.Afternoon), J);
        Assert.Equal(2, (await copy.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("added").GetInt32());
        var again = await sup.PostAsJsonAsync("/api/onduty/copy", new OnDutyEndpoints.CopyRequest(Sho, next, Shift.Afternoon, Day, Shift.Afternoon), J);
        Assert.Equal(0, (await again.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("added").GetInt32());
        var copied = await sup.GetFromJsonAsync<OnDutyEndpoints.RosterDto>(Url(next), J);
        Assert.All(copied!.Roster, a => Assert.Equal(next.ToDateTime(new TimeOnly(13, 0)), a.StartLocal));

        (await sup.PostAsync($"/api/onduty/assignments/{r.Roster[0].Id}/remove", null)).EnsureSuccessStatusCode();
        Assert.Single((await sup.GetFromJsonAsync<OnDutyEndpoints.RosterDto>(Url(Day), J))!.Roster);
    }

    [Fact]
    public async Task Times_must_fit_the_shift()
    {
        var (_, sup) = await Person("Edit");
        Assert.Equal(HttpStatusCode.BadRequest, (await sup.PostAsJsonAsync("/api/onduty/assignments", Slot(null, "X", hours: -1), J)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await sup.PostAsJsonAsync("/api/onduty/assignments", Slot(null, "X", hours: 20), J)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await sup.PostAsJsonAsync("/api/onduty/assignments", Slot(null, "X", startHour: 2), J)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await sup.PostAsJsonAsync("/api/onduty/assignments", Slot(null, null), J)).StatusCode);
    }

    [Fact]
    public async Task Call_outs_logged_searched_and_shown_on_handover()
    {
        var (_, sup) = await Person("Edit");
        var (officerId, _) = await Person(null);
        var called = Day.ToDateTime(new TimeOnly(10, 5));
        Assert.Equal(HttpStatusCode.BadRequest, (await sup.PostAsJsonAsync("/api/onduty/callouts",
            new OnDutyEndpoints.CallOutRequest(Sho, Day, Shift.Afternoon, officerId, null, Safety, called, "Hangover", null, null), J)).StatusCode);
        (await sup.PostAsJsonAsync("/api/onduty/callouts",
            new OnDutyEndpoints.CallOutRequest(Sho, Day, Shift.Afternoon, officerId, null, Safety, called, "Sick", null, "Covered by agency"), J))
            .EnsureSuccessStatusCode();

        var r = await sup.GetFromJsonAsync<OnDutyEndpoints.RosterDto>(Url(Day), J);
        Assert.Single(r!.CallOuts);
        var found = await sup.GetFromJsonAsync<List<OnDutyEndpoints.CallOutDto>>($"/api/onduty/callouts?userId={officerId}&from=2026-09-01", J);
        Assert.Single(found!);

        // On the handover of that shift, for someone who may see on duty.
        var (hid, _) = await Person("View", module: "handover");
        await app.Admin().PutAsJsonAsync($"/api/admin/users/{hid}/access/onduty", new AdminEndpoints.GrantDto("View", null, "All", null));
        var h = await app.Admin().GetFromJsonAsync<HandoverEndpoints.HandoverDto>($"/api/handover?hotelId={Sho}&date=2026-09-15&shift=Afternoon", J);
        Assert.Single(h!.CallOuts!);
    }

    [Fact]
    public async Task View_cannot_change_and_hotel_limit_applies()
    {
        var (_, viewer) = await Person("View");
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync(Url(Day))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync("/api/onduty/assignments", Slot(null, "X"), J)).StatusCode);

        var (_, smh) = await Person("Edit", Smh);
        Assert.Equal(HttpStatusCode.NotFound, (await smh.GetAsync(Url(Day))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await smh.PostAsJsonAsync("/api/onduty/assignments", Slot(null, "X"), J)).StatusCode);
    }

    [Fact]
    public void Overnight_shift_may_end_next_morning() =>
        Assert.Null(OnDutyRules.CheckTimes(Day, Shift.Overnight, Day.ToDateTime(new TimeOnly(21, 0)), Day.AddDays(1).ToDateTime(new TimeOnly(5, 30))));
}
