using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Desk.Api.Endpoints;

namespace Desk.Api.Tests;

// Lockers and parking through the real API. All data is made up.
public class SpaceTests(DeskAppFactory app) : IClassFixture<DeskAppFactory>, IAsyncLifetime
{
    const int Safety = 18, Sho = 1;
    static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public Task InitializeAsync() => app.EnsureDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    async Task<HttpClient> Person(string module, string level)
    {
        var oid = Guid.NewGuid();
        var r = await app.Admin().PostAsJsonAsync("/api/admin/users",
            new AdminEndpoints.NewUserRequest(oid, $"Person {oid:N}"[..15], $"{oid:N}@shorehotel.com", Safety, Sho));
        var id = (await r.Content.ReadFromJsonAsync<AdminEndpoints.UserDto>())!.Id;
        (await app.Admin().PutAsJsonAsync($"/api/admin/users/{id}/access/{module}", new AdminEndpoints.GrantDto(level, null, "All", null))).EnsureSuccessStatusCode();
        return app.As(oid);
    }

    async Task<int> NewSpace(string route, string section, string number, string? code = null)
    {
        var r = await app.Admin().PostAsJsonAsync($"/api/{route}", new SpaceEndpoints.SpaceRequest(Sho, section, number, code, null, null), J);
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
        return (await r.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("id").GetInt32();
    }

    static SpaceEndpoints.AssignRequest To(string name, string? plate = null) =>
        new(null, name, Safety, null, "Toyota", "Corolla", "2019", "Blue", plate, "CA", "S-100", null);

    [Fact]
    public async Task Locker_assign_release_and_codes_only_for_editors()
    {
        var number = $"L{Guid.NewGuid():N}"[..6];
        var id = await NewSpace("lockers", "Men's", number, "12-34-56");
        Assert.Equal(HttpStatusCode.BadRequest, (await app.Admin().PostAsJsonAsync("/api/lockers", new SpaceEndpoints.SpaceRequest(Sho, "Men's", number, null, null, null), J)).StatusCode);

        var desk = await Person("lockers", "Edit");
        (await desk.PostAsJsonAsync($"/api/lockers/{id}/assign", To("Made-up Houseman"), J)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await desk.PostAsJsonAsync($"/api/lockers/{id}/assign", To("Someone Else"), J)).StatusCode);

        var list = await desk.GetFromJsonAsync<List<SpaceEndpoints.SpaceDto>>($"/api/lockers?hotelId={Sho}", J);
        var locker = list!.Single(s => s.Id == id);
        Assert.Equal("12-34-56", locker.LockCode);
        Assert.Equal("Made-up Houseman", locker.Holder!.Name);
        Assert.Null(locker.Holder.Plate);                                  // vehicle fields are parking only

        var viewer = await Person("lockers", "View");
        Assert.Null((await viewer.GetFromJsonAsync<List<SpaceEndpoints.SpaceDto>>($"/api/lockers?hotelId={Sho}", J))!.Single(s => s.Id == id).LockCode);

        (await desk.PostAsJsonAsync($"/api/lockers/{id}/release", new SpaceEndpoints.ReleaseRequest(null), J)).EnsureSuccessStatusCode();
        var history = await desk.GetFromJsonAsync<List<SpaceEndpoints.HistoryRow>>($"/api/lockers/history?hotelId={Sho}&spaceId={id}", J);
        Assert.NotNull(Assert.Single(history!).ToDate);
    }

    [Fact]
    public async Task Parking_keeps_vehicle_and_modules_are_separate()
    {
        var id = await NewSpace("parking", "Employee", $"P{Guid.NewGuid():N}"[..6]);
        var lockersOnly = await Person("lockers", "Edit");
        Assert.Equal(HttpStatusCode.Forbidden, (await lockersOnly.GetAsync($"/api/parking?hotelId={Sho}")).StatusCode);

        var desk = await Person("parking", "Edit");
        (await desk.PostAsJsonAsync($"/api/parking/{id}/assign", To("Made-up Driver", "7abc123"), J)).EnsureSuccessStatusCode();
        var space = (await desk.GetFromJsonAsync<List<SpaceEndpoints.SpaceDto>>($"/api/parking?hotelId={Sho}", J))!.Single(s => s.Id == id);
        Assert.Equal("7ABC123", space.Holder!.Plate);
        Assert.Equal("2019 Blue Toyota Corolla", space.Holder.Vehicle);
        var found = await desk.GetFromJsonAsync<List<SpaceEndpoints.HistoryRow>>($"/api/parking/history?hotelId={Sho}&q=7ABC", J);
        Assert.Single(found!);

        // A locker section is not a parking section.
        Assert.Equal(HttpStatusCode.BadRequest, (await app.Admin().PostAsJsonAsync("/api/parking", new SpaceEndpoints.SpaceRequest(Sho, "Men's", "1", null, null, null), J)).StatusCode);
        // Only managers keep the list.
        Assert.Equal(HttpStatusCode.Forbidden, (await desk.PostAsJsonAsync("/api/parking", new SpaceEndpoints.SpaceRequest(Sho, "Employee", "99", null, null, null), J)).StatusCode);
    }
}
