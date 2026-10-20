using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Desk.Api.Endpoints;
using Desk.Core.Incidents;

namespace Desk.Api.Tests;

// Safety reports through the real API. All data is made up.
public class ReportTests(DeskAppFactory app) : IClassFixture<DeskAppFactory>, IAsyncLifetime
{
    const int Safety = 18, Sho = 1, Smh = 2;
    static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    static readonly DateOnly Day = new(2026, 8, 20);

    public Task InitializeAsync() => app.EnsureDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    async Task<HttpClient> Person(params (string Module, string Level, int? Hotel)[] grants)
    {
        var oid = Guid.NewGuid();
        var r = await app.Admin().PostAsJsonAsync("/api/admin/users",
            new AdminEndpoints.NewUserRequest(oid, $"Person {oid:N}"[..15], $"{oid:N}@shorehotel.com", Safety, Sho));
        var id = (await r.Content.ReadFromJsonAsync<AdminEndpoints.UserDto>())!.Id;
        foreach (var (m, l, h) in grants)
            (await app.Admin().PutAsJsonAsync($"/api/admin/users/{id}/access/{m}", new AdminEndpoints.GrantDto(l, h, "All", null))).EnsureSuccessStatusCode();
        return app.As(oid);
    }

    // Each test uses its own day (they share one database).
    async Task Report(IncidentForm form, string heading, int hotel = Sho, int hour = 14, DateOnly? day = null) =>
        (await app.Admin().PostAsJsonAsync("/api/incidents", new IncidentEndpoints.SaveRequest(form, hotel, null,
            form == IncidentForm.GuestInjury ? "Accident" : null, null, heading, (day ?? Day).ToDateTime(new TimeOnly(hour, 0)), null, null,
            null, null, null, null, null, null, null, null, null), J)).EnsureSuccessStatusCode();

    const string Range = "from=2026-08-20&to=2026-08-20";

    [Fact]
    public async Task Statistics_only_count_what_the_person_may_see()
    {
        var day = new DateOnly(2026, 8, 21);
        const string Range = "from=2026-08-21&to=2026-08-21";
        await Report(IncidentForm.Incident, "Made-up A", day: day);
        await Report(IncidentForm.GuestInjury, "Made-up B", hour: 23, day: day);
        await Report(IncidentForm.HighLevel, "Made-up C", day: day);
        await Report(IncidentForm.Incident, "Made-up D", hotel: Smh, day: day);

        var everyone = await app.Admin().GetFromJsonAsync<ReportEndpoints.IncidentStats>($"/api/reports/incidents?{Range}", J);
        Assert.Equal(4, everyone!.Total);

        // Reports + ordinary incidents at SHO only: no High Level, no SMH.
        var limited = await Person(("safety-reports", "View", null), ("incidents", "View", Sho));
        var s = await limited.GetFromJsonAsync<ReportEndpoints.IncidentStats>($"/api/reports/incidents?{Range}", J);
        Assert.Equal(2, s!.Total);
        Assert.DoesNotContain(s.ByForm, x => x.Key == "High Level");
        Assert.Equal(["Afternoon", "Overnight"], s.ByShift.Select(x => x.Key));
        Assert.Single(s.ByMonth);

        // Reports access but no incident access: nothing counted.
        var reportsOnly = await Person(("safety-reports", "View", null));
        Assert.Equal(0, (await reportsOnly.GetFromJsonAsync<ReportEndpoints.IncidentStats>($"/api/reports/incidents?{Range}", J))!.Total);

        // No reports access at all: refused.
        var noReports = await Person(("incidents", "View", null));
        Assert.Equal(HttpStatusCode.Forbidden, (await noReports.GetAsync($"/api/reports/incidents?{Range}")).StatusCode);
    }

    [Fact]
    public async Task Export_is_csv_with_formulas_defused()
    {
        await Report(IncidentForm.Incident, "=HYPERLINK(\"http://example.invalid\")");
        var r = await app.Admin().GetAsync($"/api/reports/incidents.csv?{Range}");
        Assert.Equal("text/csv", r.Content.Headers.ContentType!.MediaType);
        var text = Encoding.UTF8.GetString(await r.Content.ReadAsByteArrayAsync());
        Assert.Contains("\"'=HYPERLINK(", text);
        Assert.DoesNotContain(",\"=HYPERLINK", text);
    }

    [Fact]
    public async Task Bad_ranges_are_refused()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await app.Admin().GetAsync("/api/reports/incidents?from=2026-09-01&to=2026-08-01")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await app.Admin().GetAsync("/api/reports/incidents?from=2020-01-01&to=2026-08-01")).StatusCode);
    }

    [Fact]
    public async Task Shift_summary_collects_the_shift()
    {
        await Report(IncidentForm.Incident, "Made-up shift item", hour: 15);
        (await app.Admin().PostAsJsonAsync("/api/onduty/assignments", new OnDutyEndpoints.AssignmentRequest(Sho, Day, Shift.Afternoon,
            null, "Made-up Guard", null, "R-1", "Lobby", Day.ToDateTime(new TimeOnly(13, 0)), Day.ToDateTime(new TimeOnly(21, 0))), J))
            .EnsureSuccessStatusCode();

        var s = await app.Admin().GetFromJsonAsync<ReportEndpoints.ShiftSummary>($"/api/reports/shift?hotelId={Sho}&date=2026-08-20&shift=Afternoon", J);
        Assert.Contains(s!.Incidents!, i => i.Heading == "Made-up shift item");
        Assert.Contains(s.OnDuty!, a => a.Name == "Made-up Guard");

        // Hotel limit on the reports module.
        var smh = await Person(("safety-reports", "View", Smh));
        Assert.Equal(HttpStatusCode.NotFound, (await smh.GetAsync($"/api/reports/shift?hotelId={Sho}")).StatusCode);
    }
}
