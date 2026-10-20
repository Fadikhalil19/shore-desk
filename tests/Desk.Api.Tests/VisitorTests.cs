using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Desk.Api.Endpoints;
using Desk.Core.Visitors;

namespace Desk.Api.Tests;

// Visitors through the real API. All data is made up.
public class VisitorTests(DeskAppFactory app) : IClassFixture<DeskAppFactory>, IAsyncLifetime
{
    const int Safety = 18, Sho = 1, Smh = 2, ShoArea = 1, SmhArea = 15;
    static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public Task InitializeAsync() => app.EnsureDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    async Task<HttpClient> Person(string level, int? hotel = null)
    {
        var oid = Guid.NewGuid();
        var r = await app.Admin().PostAsJsonAsync("/api/admin/users",
            new AdminEndpoints.NewUserRequest(oid, $"Person {oid:N}"[..15], $"{oid:N}@shorehotel.com", Safety, Sho));
        var id = (await r.Content.ReadFromJsonAsync<AdminEndpoints.UserDto>())!.Id;
        (await app.Admin().PutAsJsonAsync($"/api/admin/users/{id}/access/visitors", new AdminEndpoints.GrantDto(level, hotel, "All", null))).EnsureSuccessStatusCode();
        return app.As(oid);
    }

    static VisitorEndpoints.VisitRequest Visitor(string name, int hotel = Sho, int? area = ShoArea, List<Companion>? group = null,
        DateTime? inAt = null, int? scheduled = null) =>
        new(hotel, name, "Acme Elevators", "Contractor", null, null, area, "Elevator room", "Chief Engineer", null, "Elevator service",
            "Red", "R-12", inAt, null, null, group, scheduled);

    async Task<int> CheckIn(HttpClient c, VisitorEndpoints.VisitRequest req)
    {
        var r = await c.PostAsJsonAsync("/api/visitors", req, J);
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
        return (await r.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task Check_in_group_then_out()
    {
        var desk = await Person("Edit");
        var id = await CheckIn(desk, Visitor("Made-up Visitor", group: [new("Made-up Helper", "R-13")]));
        var onSite = await desk.GetFromJsonAsync<List<VisitorEndpoints.VisitDto>>($"/api/visitors?hotelId={Sho}", J);
        var v = onSite!.Single(x => x.Id == id);
        Assert.Equal("Made-up Helper", Assert.Single(v.Companions).Name);

        (await desk.PostAsJsonAsync($"/api/visitors/{id}/checkout", new VisitorEndpoints.CheckOutRequest(null), J)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await desk.PostAsJsonAsync($"/api/visitors/{id}/checkout", new VisitorEndpoints.CheckOutRequest(null), J)).StatusCode);
        onSite = await desk.GetFromJsonAsync<List<VisitorEndpoints.VisitDto>>($"/api/visitors?hotelId={Sho}", J);
        Assert.DoesNotContain(onSite!, x => x.Id == id);
        var found = await desk.GetFromJsonAsync<List<VisitorEndpoints.VisitDto>>($"/api/visitors?hotelId={Sho}&view=search&q=Helper", J);
        Assert.Contains(found!, x => x.Id == id);
    }

    [Fact]
    public async Task Long_stay_is_flagged()
    {
        var id = await CheckIn(app.Admin(), Visitor("Made-up Long Stay", inAt: DateTime.Now.AddHours(-9)));
        var v = await app.Admin().GetFromJsonAsync<VisitorEndpoints.VisitDto>($"/api/visitors/{id}", J);
        Assert.True(v!.Overdue);
    }

    [Fact]
    public async Task Booked_visit_links_once()
    {
        var desk = await Person("Edit");
        var r = await desk.PostAsJsonAsync("/api/visitors/scheduled", new VisitorEndpoints.ScheduleRequest(Sho, "Made-up Interviewee", null, "Interview",
            DateTime.Now.AddHours(1), "HR Manager", null, "Interview", null), J);
        var sid = (await r.Content.ReadFromJsonAsync<JsonElement>(J)).GetProperty("id").GetInt32();
        await CheckIn(desk, Visitor("Made-up Interviewee", scheduled: sid));
        Assert.Equal(HttpStatusCode.BadRequest, (await desk.PostAsJsonAsync("/api/visitors", Visitor("Again", scheduled: sid), J)).StatusCode);
        var list = await desk.GetFromJsonAsync<List<VisitorEndpoints.ScheduledDto>>($"/api/visitors/scheduled?hotelId={Sho}", J);
        Assert.NotNull(list!.Single(s => s.Id == sid).VisitId);
    }

    [Fact]
    public async Task Rules_and_access()
    {
        var admin = app.Admin();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/visitors", Visitor("X", hotel: Smh, area: ShoArea), J)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/visitors", Visitor(" "), J)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/visitors",
            Visitor("X", group: [new("a", null), new("b", null), new("c", null), new("d", null), new("e", null), new("f", null)]), J)).StatusCode);

        var viewer = await Person("View");
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync("/api/visitors", Visitor("X"), J)).StatusCode);
        var smh = await Person("Edit", Smh);
        var id = await CheckIn(admin, Visitor("Made-up Sho Visitor"));
        Assert.Equal(HttpStatusCode.NotFound, (await smh.GetAsync($"/api/visitors/{id}")).StatusCode);
        await CheckIn(smh, Visitor("Made-up Smh Visitor", hotel: Smh, area: SmhArea));

        var desk = await Person("Edit");
        Assert.Equal(HttpStatusCode.Forbidden, (await desk.PostAsJsonAsync($"/api/visitors/{id}/void", new VisitorEndpoints.VoidRequest("x"), J)).StatusCode);
    }

    [Fact]
    public async Task Photo_must_be_an_image()
    {
        var id = await CheckIn(app.Admin(), Visitor("Made-up Photo"));
        static MultipartFormDataContent File(string name)
        {
            var f = new ByteArrayContent([0xFF, 0xD8, 0xFF, 1]);
            f.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            return new MultipartFormDataContent { { f, "file", name } };
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await app.Admin().PostAsync($"/api/visitors/{id}/photo", File("id.pdf"))).StatusCode);
        (await app.Admin().PostAsync($"/api/visitors/{id}/photo", File("face.jpg"))).EnsureSuccessStatusCode();
        Assert.Equal("image/jpeg", (await app.Admin().GetAsync($"/api/visitors/{id}/photo")).Content.Headers.ContentType!.MediaType);
    }
}
