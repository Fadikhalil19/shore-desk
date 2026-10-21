using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Desk.Api.Endpoints;
using Desk.Core.Data;
using Desk.Core.LostFound;

namespace Desk.Api.Tests;

// Lost & found through the real API. All data is made up.
public class LostFoundTests(DeskAppFactory app) : IClassFixture<DeskAppFactory>, IAsyncLifetime
{
    const int Safety = 18, Sho = 1, Smh = 2, ShoArea = 1;
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
        (await app.Admin().PutAsJsonAsync($"/api/admin/users/{id}/access/lostfound", new AdminEndpoints.GrantDto(level, hotel, "All", null))).EnsureSuccessStatusCode();
        return app.As(oid);
    }

    async Task<int> Item(string list, string name) =>
        (await app.Admin().GetFromJsonAsync<MeEndpoints.LookupsDto>("/api/lookups"))!.ListItems.Single(i => i.ListKey == list && i.Name == name).Id;

    async Task<int> LogFound(HttpClient c, string brand = "Apple", DateOnly? purge = null, string room = "214")
    {
        var phone = await Item(ListKeys.PropertyCategory, "Cell Phone");
        var black = await Item(ListKeys.Color, "Black");
        var r = await c.PostAsJsonAsync("/api/lostfound/found", new LostFoundEndpoints.FoundRequest(Sho, phone, "Phone", "Made-up phone in a cracked case",
            black, brand, null, true, DateTime.Now.AddHours(-2), ShoArea, "Lobby sofa", room, "Employee", "Made-up Houseman", null, null, "Safe",
            purge, null, null, null), J);
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
        return (await r.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("id").GetInt32();
    }

    async Task<int> LogLost(HttpClient c, string brand = "Apple", string room = "214")
    {
        var phone = await Item(ListKeys.PropertyCategory, "Cell Phone");
        var black = await Item(ListKeys.Color, "Black");
        var today = DateOnly.FromDateTime(DateTime.Now);
        var r = await c.PostAsJsonAsync("/api/lostfound/lost", new LostFoundEndpoints.LostRequest(Sho, "Made-up Guest", "555-0100", "guest@example.com",
            room, today.AddDays(-2), today, phone, "black phone cracked case", black, brand, "Lobby", today.AddDays(-1), today, null), J);
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
        return (await r.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task Lost_report_finds_the_match_and_item_is_returned_with_signature()
    {
        var desk = await Person("Edit");
        var brand = $"Brand{Guid.NewGuid():N}"[..12];
        var found = await LogFound(desk, brand);
        var lost = await LogLost(desk, brand);

        var l = await desk.GetFromJsonAsync<LostFoundEndpoints.LostDto>($"/api/lostfound/lost/{lost}", J);
        Assert.Equal(found, l!.Matches.First().Id);
        (await desk.PostAsJsonAsync($"/api/lostfound/lost/{lost}/match", new LostFoundEndpoints.MatchRequest(found), J)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.BadRequest, (await desk.PostAsJsonAsync($"/api/lostfound/found/{found}/return",
            new LostFoundEndpoints.ReturnRequest("Made-up Guest", null, null, lost), J)).StatusCode);
        (await desk.PostAsJsonAsync($"/api/lostfound/found/{found}/return", new LostFoundEndpoints.ReturnRequest("Made-up Guest", Sig, "ID checked", lost), J))
            .EnsureSuccessStatusCode();
        var f = await desk.GetFromJsonAsync<LostFoundEndpoints.FoundDto>($"/api/lostfound/found/{found}", J);
        Assert.Equal(FoundStatus.ReturnedToOwner, f!.Item.Status);
        Assert.True(f.HasReturnSignature);
        Assert.Equal(LostStatus.Closed, (await desk.GetFromJsonAsync<LostFoundEndpoints.LostDto>($"/api/lostfound/lost/{lost}", J))!.Report.Status);
    }

    [Fact]
    public async Task Shipping_needs_payment_settled_and_never_takes_card_numbers()
    {
        var desk = await Person("Edit");
        var found = await LogFound(desk);
        var ship = new LostFoundEndpoints.ShipmentRequest("Made-up Guest", "1 Test St", "Austin", "TX", "78701", "USA", null, "guest@example.com",
            "UPS", null, 25m, PaymentStatus.Waiting, null);
        Assert.Equal(HttpStatusCode.BadRequest, (await desk.PostAsJsonAsync($"/api/lostfound/found/{found}/shipments",
            ship with { PaymentNote = "card 4111 1111 1111 1111 exp 12/29" }, J)).StatusCode);
        var r = await desk.PostAsJsonAsync($"/api/lostfound/found/{found}/shipments", ship, J);
        var sid = (await r.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("id").GetInt32();

        Assert.Equal(HttpStatusCode.BadRequest, (await desk.PostAsJsonAsync($"/api/lostfound/shipments/{sid}/shipped",
            new LostFoundEndpoints.ShippedRequest(null, "UPS", "1Z999"), J)).StatusCode);   // still waiting for payment
        (await desk.PutAsJsonAsync($"/api/lostfound/shipments/{sid}", ship with { Payment = PaymentStatus.Paid, PaymentNote = "Paid by link 10/21" }, J))
            .EnsureSuccessStatusCode();
        (await desk.PostAsJsonAsync($"/api/lostfound/shipments/{sid}/shipped", new LostFoundEndpoints.ShippedRequest(null, "USPS", "9400111899223817762843"), J))
            .EnsureSuccessStatusCode();
        Assert.Equal(FoundStatus.Shipped, (await desk.GetFromJsonAsync<LostFoundEndpoints.FoundDto>($"/api/lostfound/found/{found}", J))!.Item.Status);
    }

    [Fact]
    public async Task Disposal_only_after_purge_date_and_by_a_manager()
    {
        var desk = await Person("Edit");
        var manager = await Person("Manage");
        var fresh = await LogFound(desk);
        var old = await LogFound(desk, purge: DateOnly.FromDateTime(DateTime.Now).AddDays(-1));
        Assert.Equal(HttpStatusCode.Forbidden, (await desk.PostAsJsonAsync($"/api/lostfound/found/{old}/dispose", new LostFoundEndpoints.DisposeRequest("Donated", null), J)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.PostAsJsonAsync($"/api/lostfound/found/{fresh}/dispose", new LostFoundEndpoints.DisposeRequest("Donated", null), J)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.PostAsJsonAsync($"/api/lostfound/found/{old}/dispose", new LostFoundEndpoints.DisposeRequest("Sold", null), J)).StatusCode);
        (await manager.PostAsJsonAsync($"/api/lostfound/found/{old}/dispose", new LostFoundEndpoints.DisposeRequest("Donated", "Goodwill"), J)).EnsureSuccessStatusCode();
        var due = await manager.GetFromJsonAsync<List<LostFoundEndpoints.FoundRow>>($"/api/lostfound/found?hotelId={Sho}&view=due", J);
        Assert.DoesNotContain(due!, f => f.Id == old);
    }

    [Fact]
    public async Task Hotel_limit_applies()
    {
        var id = await LogFound(app.Admin());
        var smh = await Person("Edit", Smh);
        Assert.Equal(HttpStatusCode.NotFound, (await smh.GetAsync($"/api/lostfound/found/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await smh.GetAsync($"/api/lostfound/found?hotelId={Sho}")).StatusCode);
    }

    [Theory]
    [InlineData("4111 1111 1111 1111", true)]
    [InlineData("4111-1111-1111-1111", true)]
    [InlineData("call 555-0100 after 5", false)]
    [InlineData("Paid by link, ref 12345", false)]
    public void Card_numbers_are_spotted(string text, bool expected) => Assert.Equal(expected, LostFoundRules.LooksLikeCardNumber(text));
}
