using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Desk.Api.Endpoints;
using Desk.Core.Access;
using Desk.Core.Keys;

namespace Desk.Api.Tests;

// Keys through the real API. All data is made up.
public class KeyTests(DeskAppFactory app) : IClassFixture<DeskAppFactory>, IAsyncLifetime
{
    const int Safety = 18, Sho = 1, Smh = 2;
    static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    // A 1x1 PNG, standing in for a finger signature.
    const string Sig = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=";

    public Task InitializeAsync() => app.EnsureDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    async Task<(int Id, HttpClient Client)> Person(string level, int? hotel = null)
    {
        var oid = Guid.NewGuid();
        var r = await app.Admin().PostAsJsonAsync("/api/admin/users",
            new AdminEndpoints.NewUserRequest(oid, $"Person {oid:N}"[..15], $"{oid:N}@shorehotel.com", Safety, Sho));
        var id = (await r.Content.ReadFromJsonAsync<AdminEndpoints.UserDto>())!.Id;
        (await app.Admin().PutAsJsonAsync($"/api/admin/users/{id}/access/keys", new AdminEndpoints.GrantDto(level, hotel, "All", null))).EnsureSuccessStatusCode();
        return (id, app.As(oid));
    }

    async Task<int> NewKey(string name, string? barcode = null)
    {
        var r = await app.Admin().PostAsJsonAsync("/api/keys", new KeyEndpoints.KeyRequest(Sho, name, barcode, "Made-up key", 10), J);
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
        return (await r.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("id").GetInt32();
    }

    static KeyEndpoints.SignOutRequest Out(string name = "Made-up Contractor", string? sig = Sig) =>
        new("Contractor", null, name, "Acme Locks", null, null, null, null, sig);

    async Task<KeyEndpoints.KeyDto> Key(HttpClient c, int id) =>
        (await c.GetFromJsonAsync<List<KeyEndpoints.KeyDto>>($"/api/keys?hotelId={Sho}", J))!.Single(k => k.Id == id);

    [Fact]
    public async Task Sign_out_and_in_with_signatures()
    {
        var (_, officer) = await Person("Edit");
        var id = await NewKey($"Ring {Guid.NewGuid():N}"[..12], $"BC{Guid.NewGuid():N}"[..10]);

        Assert.Equal(HttpStatusCode.BadRequest, (await officer.PostAsJsonAsync($"/api/keys/{id}/signout", Out(sig: null), J)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await officer.PostAsJsonAsync($"/api/keys/{id}/signout", Out(sig: "data:image/png;base64,AAAA"), J)).StatusCode);
        (await officer.PostAsJsonAsync($"/api/keys/{id}/signout", Out(), J)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await officer.PostAsJsonAsync($"/api/keys/{id}/signout", Out("Someone else"), J)).StatusCode);

        var k = await Key(officer, id);
        Assert.Equal(KeyStatus.SignedOut, k.Status);
        Assert.Equal("Made-up Contractor", k.Holder!.Name);
        var sig = await officer.GetAsync($"/api/keys/checkouts/{k.Holder.CheckoutId}/signature/out");
        Assert.Equal("image/png", sig.Content.Headers.ContentType!.MediaType);

        (await officer.PostAsJsonAsync($"/api/keys/{id}/signin", new KeyEndpoints.SignInRequest(null, null, Sig), J)).EnsureSuccessStatusCode();
        k = await Key(officer, id);
        Assert.Equal(KeyStatus.In, k.Status);
        Assert.Null(k.Holder);

        var history = await officer.GetFromJsonAsync<List<KeyEndpoints.HistoryRow>>($"/api/keys/history?hotelId={Sho}&keyId={id}", J);
        var row = Assert.Single(history!);
        Assert.True(row.HasSignature && row.HasBackSignature);
        Assert.NotNull(row.BackLocal);
    }

    [Fact]
    public async Task Scanner_lookup_finds_by_barcode_or_name()
    {
        var code = $"SC{Guid.NewGuid():N}"[..10];
        var id = await NewKey($"Cab {Guid.NewGuid():N}"[..10], code);
        var found = await app.Admin().GetFromJsonAsync<JsonElement>($"/api/keys/lookup?hotelId={Sho}&code={code}", J);
        Assert.Equal(id, found.GetProperty("id").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await app.Admin().GetAsync($"/api/keys/lookup?hotelId={Sho}&code=nope")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await app.Admin().PostAsJsonAsync("/api/keys", new KeyEndpoints.KeyRequest(Sho, "Other", code, null, 1), J)).StatusCode);
    }

    [Fact]
    public async Task Missing_by_officer_back_in_service_only_by_manager()
    {
        var (_, officer) = await Person("Edit");
        var (_, manager) = await Person("Manage");
        var id = await NewKey($"Master {Guid.NewGuid():N}"[..12]);
        (await officer.PostAsJsonAsync($"/api/keys/{id}/signout", Out(), J)).EnsureSuccessStatusCode();
        // Lost while signed out: the sign-out closes, the key is Missing.
        (await officer.PostAsJsonAsync($"/api/keys/{id}/status", new KeyEndpoints.StatusRequest(KeyStatus.Missing, "Contractor lost it"), J)).EnsureSuccessStatusCode();
        var k = await Key(officer, id);
        Assert.Equal(KeyStatus.Missing, k.Status);
        Assert.Null(k.Holder);
        Assert.Equal(HttpStatusCode.BadRequest, (await officer.PostAsJsonAsync($"/api/keys/{id}/signout", Out(), J)).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await officer.PostAsJsonAsync($"/api/keys/{id}/status", new KeyEndpoints.StatusRequest(KeyStatus.In, "Found it"), J)).StatusCode);
        (await manager.PostAsJsonAsync($"/api/keys/{id}/status", new KeyEndpoints.StatusRequest(KeyStatus.In, "Found in the truck"), J)).EnsureSuccessStatusCode();
        Assert.Equal(KeyStatus.In, (await Key(officer, id)).Status);
    }

    [Fact]
    public async Task Key_list_is_for_managers_and_hotel_limit_applies()
    {
        var (_, officer) = await Person("Edit");
        Assert.Equal(HttpStatusCode.Forbidden, (await officer.PostAsJsonAsync("/api/keys", new KeyEndpoints.KeyRequest(Sho, "X", null, null, 1), J)).StatusCode);
        var (_, smh) = await Person("Edit", Smh);
        var id = await NewKey($"Sho {Guid.NewGuid():N}"[..10]);
        Assert.Equal(HttpStatusCode.NotFound, (await smh.GetAsync($"/api/keys?hotelId={Sho}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await smh.PostAsJsonAsync($"/api/keys/{id}/signout", Out(), J)).StatusCode);
    }

    [Fact]
    public async Task Shift_count_must_cover_every_key()
    {
        var (_, officer) = await Person("Edit");
        await NewKey($"Count {Guid.NewGuid():N}"[..12]);
        await NewKey($"Count {Guid.NewGuid():N}"[..12]);
        var keys = await officer.GetFromJsonAsync<List<KeyEndpoints.KeyDto>>($"/api/keys?hotelId={Sho}", J);
        var day = new DateOnly(2026, 9, 1);
        Assert.Equal(HttpStatusCode.BadRequest, (await officer.PostAsJsonAsync("/api/keys/counts",
            new KeyEndpoints.CountRequest(Sho, day, Desk.Core.Incidents.Shift.Morning, null, [new(keys![0].Id, true, null)]), J)).StatusCode);

        var lines = keys.Select((k, i) => new KeyEndpoints.CountLineRequest(k.Id, i != 0, i == 0 ? "Not in the cabinet" : null)).ToList();
        var r = await officer.PostAsJsonAsync("/api/keys/counts", new KeyEndpoints.CountRequest(Sho, day, Desk.Core.Incidents.Shift.Morning, null, lines), J);
        r.EnsureSuccessStatusCode();
        var countId = (await r.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("id").GetInt32();
        var detail = await officer.GetFromJsonAsync<KeyEndpoints.CountDto>($"/api/keys/counts/{countId}", J);
        Assert.Equal(keys.Count, detail!.Keys);
        Assert.Equal(keys.Count - 1, detail.OnHand);
    }

    [Fact]
    public void Rules()
    {
        var now = new DateTime(2026, 9, 1, 20, 0, 0);
        Assert.True(KeyRules.Overdue(now.AddHours(-11), null, now));
        Assert.False(KeyRules.Overdue(now.AddHours(-9), null, now));
        Assert.True(KeyRules.Overdue(now.AddHours(-2), now.AddHours(-1), now));
        Assert.NotNull(KeyRules.CheckStatusChange(KeyStatus.SignedOut, KeyStatus.In, AccessLevel.Manage));
        Assert.Null(KeyRules.CheckStatusChange(KeyStatus.In, KeyStatus.Broken, AccessLevel.Edit));
        Assert.False(KeyRules.IsPng([1, 2, 3, 4, 5, 6, 7, 8, 9]));
    }
}
