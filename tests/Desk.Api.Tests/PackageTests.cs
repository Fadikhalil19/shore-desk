using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Desk.Api.Endpoints;
using Desk.Core.Packages;

namespace Desk.Api.Tests;

// Packages through the real API. All data is made up.
public class PackageTests(DeskAppFactory app) : IClassFixture<DeskAppFactory>, IAsyncLifetime
{
    const int Safety = 18, Sho = 1, Smh = 2;
    static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    const string Sig = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=";

    public Task InitializeAsync() => app.EnsureDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    async Task<HttpClient> Person(string level, int? hotel = null)
    {
        var oid = Guid.NewGuid();
        var r = await app.Admin().PostAsJsonAsync("/api/admin/users",
            new AdminEndpoints.NewUserRequest(oid, $"Person {oid:N}"[..15], $"{oid:N}@shorehotel.com", Safety, Sho));
        var id = (await r.Content.ReadFromJsonAsync<AdminEndpoints.UserDto>())!.Id;
        (await app.Admin().PutAsJsonAsync($"/api/admin/users/{id}/access/packages", new AdminEndpoints.GrantDto(level, hotel, "All", null))).EnsureSuccessStatusCode();
        return app.As(oid);
    }

    static PackageEndpoints.PackageRequest Incoming(string tracking, PackageDirection d = PackageDirection.Incoming, int hotel = Sho) =>
        new(hotel, d, null, "UPS", "Box", tracking, 1, "2 lb", null, "Guest", "Made-up Guest", null, "514", null, null, null, null,
            "Made-up Sender", "Shelf B", null);

    async Task<int> Log(HttpClient c, PackageEndpoints.PackageRequest r)
    {
        var res = await c.PostAsJsonAsync("/api/packages", r, J);
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        return (await res.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task Incoming_scanned_notified_and_picked_up_with_signature()
    {
        var desk = await Person("Edit");
        var tracking = $"1Z{Guid.NewGuid():N}"[..18];
        var id = await Log(desk, Incoming(tracking));

        var found = await desk.GetFromJsonAsync<JsonElement>($"/api/packages/lookup?hotelId={Sho}&code={tracking}", J);
        Assert.Equal(id, found.GetProperty("id").GetInt32());

        (await desk.PostAsJsonAsync($"/api/packages/{id}/notify", new PackageEndpoints.NotifyRequest("Phone", null), J)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await desk.PostAsJsonAsync($"/api/packages/{id}/close",
            new PackageEndpoints.CloseRequest(PackageStatus.PickedUp, "Made-up Guest", null, null), J)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await desk.PostAsJsonAsync($"/api/packages/{id}/close",
            new PackageEndpoints.CloseRequest(PackageStatus.Shipped, "x", null, null), J)).StatusCode);   // incoming cannot be "shipped"
        (await desk.PostAsJsonAsync($"/api/packages/{id}/close", new PackageEndpoints.CloseRequest(PackageStatus.PickedUp, "Made-up Guest", Sig, null), J))
            .EnsureSuccessStatusCode();

        var p = await desk.GetFromJsonAsync<PackageEndpoints.PackageDto>($"/api/packages/{id}", J);
        Assert.Equal(PackageStatus.PickedUp, p!.Status);
        Assert.True(p.HasSignature);
        Assert.Equal("Phone", p.NotifiedHow);
        var waiting = await desk.GetFromJsonAsync<List<PackageEndpoints.PackageDto>>($"/api/packages?hotelId={Sho}", J);
        Assert.DoesNotContain(waiting!, x => x.Id == id);
    }

    [Fact]
    public async Task Outgoing_is_shipped_and_rules_hold()
    {
        var desk = await Person("Edit");
        var id = await Log(desk, Incoming("OUT1", PackageDirection.Outgoing));
        Assert.Equal(HttpStatusCode.BadRequest, (await desk.PostAsJsonAsync($"/api/packages/{id}/notify", new PackageEndpoints.NotifyRequest("Phone", null), J)).StatusCode);
        (await desk.PostAsJsonAsync($"/api/packages/{id}/close", new PackageEndpoints.CloseRequest(PackageStatus.Shipped, null, null, "UPS pickup"), J)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await desk.PostAsJsonAsync("/api/packages", Incoming("X") with { Carrier = "Pigeon" }, J)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await desk.PostAsJsonAsync("/api/packages", Incoming("X") with { Count = 0 }, J)).StatusCode);
    }

    [Fact]
    public async Task Access_and_hotel_limits()
    {
        var viewer = await Person("View");
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync("/api/packages", Incoming("V1"), J)).StatusCode);
        var id = await Log(app.Admin(), Incoming("SHO1"));
        var smh = await Person("Edit", Smh);
        Assert.Equal(HttpStatusCode.NotFound, (await smh.GetAsync($"/api/packages/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await smh.GetAsync($"/api/packages/lookup?hotelId={Sho}&code=SHO1")).StatusCode);
    }
}
