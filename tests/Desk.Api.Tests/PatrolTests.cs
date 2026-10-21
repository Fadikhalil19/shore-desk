using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Desk.Api.Endpoints;
using Desk.Core.Patrol;

namespace Desk.Api.Tests;

// Patrol through the real API. All data is made up.
public class PatrolTests(DeskAppFactory app) : IClassFixture<DeskAppFactory>, IAsyncLifetime
{
    const int Safety = 18, Sho = 1, Smh = 2;
    static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public Task InitializeAsync() => app.EnsureDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    async Task<HttpClient> Person(string level, int? hotel = null)
    {
        var oid = Guid.NewGuid();
        var r = await app.Admin().PostAsJsonAsync("/api/admin/users",
            new AdminEndpoints.NewUserRequest(oid, $"Person {oid:N}"[..15], $"{oid:N}@shorehotel.com", Safety, Sho));
        var id = (await r.Content.ReadFromJsonAsync<AdminEndpoints.UserDto>())!.Id;
        (await app.Admin().PutAsJsonAsync($"/api/admin/users/{id}/access/patrol", new AdminEndpoints.GrantDto(level, hotel, "All", null))).EnsureSuccessStatusCode();
        return app.As(oid);
    }

    async Task<(int Id, string Code)> NewCheckpoint(string name)
    {
        var r = await app.Admin().PostAsJsonAsync("/api/patrol/checkpoints", new PatrolEndpoints.CheckpointRequest(Sho, name, null, "Check the door"), J);
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
        var e = await r.Content.ReadFromJsonAsync<JsonElement>(J);
        return (e.GetProperty("id").GetInt32(), e.GetProperty("code").GetString()!);
    }

    [Fact]
    public async Task Round_with_tags_reports_missed_checkpoints()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var a = await NewCheckpoint($"Lobby {suffix}");
        var b = await NewCheckpoint($"Pool gate {suffix}");
        var cc = await NewCheckpoint($"Garage {suffix}");
        var route = await app.Admin().PostAsJsonAsync("/api/patrol/routes", new PatrolEndpoints.RouteRequest(Sho, $"Night {suffix}", 45, [a.Id, b.Id, cc.Id]), J);
        var routeId = (await route.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("id").GetInt32();

        var officer = await Person("Edit");
        var start = await officer.PostAsJsonAsync("/api/patrol/rounds", new PatrolEndpoints.StartRequest(routeId), J);
        var roundId = (await start.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.BadRequest, (await officer.PostAsJsonAsync("/api/patrol/rounds", new PatrolEndpoints.StartRequest(routeId), J)).StatusCode);

        var scan = await officer.PostAsJsonAsync("/api/patrol/scan", new PatrolEndpoints.ScanRequest(a.Code.ToUpperInvariant(), null, false, false, null), J);
        var result = await scan.Content.ReadFromJsonAsync<PatrolEndpoints.ScanResult>(J);
        Assert.Equal($"Pool gate {suffix}", result!.Next);
        (await officer.PostAsJsonAsync("/api/patrol/scan", new PatrolEndpoints.ScanRequest(cc.Code, null, false, true, "Gate arm broken"), J)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await officer.PostAsJsonAsync("/api/patrol/scan", new PatrolEndpoints.ScanRequest(cc.Code, null, false, true, null), J)).StatusCode);

        var end = await officer.PostAsJsonAsync($"/api/patrol/rounds/{roundId}/end", new PatrolEndpoints.EndRequest(null), J);
        var done = await end.Content.ReadFromJsonAsync<PatrolEndpoints.RoundDto>(J);
        Assert.Equal(RoundStatus.Completed, done!.Status);
        Assert.Equal(1, done.Missed);
        Assert.Equal($"Pool gate {suffix}", Assert.Single(done.MissedStops!).Name);

        var problems = await officer.GetFromJsonAsync<List<PatrolEndpoints.ScanDto>>($"/api/patrol/problems?hotelId={Sho}", J);
        Assert.Contains(problems!, p => p.Note == "Gate arm broken");
    }

    [Fact]
    public async Task Codes_are_secret_and_unknown_tags_refused()
    {
        var cp = await NewCheckpoint($"Roof {Guid.NewGuid():N}"[..12]);
        var officer = await Person("Edit");
        var list = await officer.GetFromJsonAsync<List<PatrolEndpoints.CheckpointDto>>($"/api/patrol/checkpoints?hotelId={Sho}", J);
        Assert.Null(list!.Single(x => x.Id == cp.Id).Code);
        Assert.Equal(HttpStatusCode.NotFound, (await officer.PostAsJsonAsync("/api/patrol/scan", new PatrolEndpoints.ScanRequest("notacode", null, false, false, null), J)).StatusCode);

        // A new code retires the old link.
        var renewed = await app.Admin().PostAsync($"/api/patrol/checkpoints/{cp.Id}/newcode", null);
        renewed.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await officer.PostAsJsonAsync("/api/patrol/scan", new PatrolEndpoints.ScanRequest(cp.Code, null, false, false, null), J)).StatusCode);

        // An officer limited to another hotel cannot use this tag.
        var smh = await Person("Edit", Smh);
        var code = (await renewed.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("code").GetString();
        Assert.Equal(HttpStatusCode.NotFound, (await smh.PostAsJsonAsync("/api/patrol/scan", new PatrolEndpoints.ScanRequest(code, null, false, false, null), J)).StatusCode);
    }

    [Fact]
    public async Task Manual_check_is_marked()
    {
        var cp = await NewCheckpoint($"Dock {Guid.NewGuid():N}"[..12]);
        var officer = await Person("Edit");
        (await officer.PostAsJsonAsync("/api/patrol/scan", new PatrolEndpoints.ScanRequest(null, cp.Id, true, true, "Tag missing from wall"), J)).EnsureSuccessStatusCode();
        var problems = await officer.GetFromJsonAsync<List<PatrolEndpoints.ScanDto>>($"/api/patrol/problems?hotelId={Sho}", J);
        Assert.True(problems!.Single(p => p.CheckpointId == cp.Id).Manual);
    }

    [Fact]
    public void Codes_look_right_and_missed_is_computed()
    {
        var code = PatrolRules.NewCode();
        Assert.Matches("^[a-z2-9]{8}$", code);
        Assert.Equal([2, 3], PatrolRules.Missed([1, 2, 3], [1, 1]));
    }
}
