using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Desk.Api.Auth;
using Desk.Api.Endpoints;
using Desk.Core.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Desk.Api.Tests;

// Runs the real web app against a temporary SQL Server database (DeskTest_...),
// with Microsoft sign-in swapped for a test sign-in that reads who you are from headers.
public class DeskAppFactory : WebApplicationFactory<Program>
{
    public string ConnectionString { get; } =
        $@"Server=localhost\SQLEXPRESS;Database=DeskTest_{Guid.NewGuid():N};Trusted_Connection=True;TrustServerCertificate=True";

    public Guid AdminOid { get; } = Guid.NewGuid();

    // Uploaded files go to a temporary folder, removed with the test database.
    public string StorageRoot { get; } = Path.Combine(Path.GetTempPath(), $"DeskTestFiles_{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Desk", ConnectionString);
        builder.UseSetting("AzureAd:ClientId", "00000000-0000-0000-0000-000000000001");
        builder.UseSetting("App:StorageRoot", StorageRoot);
        builder.ConfigureTestServices(s =>
        {
            s.AddAuthentication(TestAuth.Scheme).AddScheme<AuthenticationSchemeOptions, TestAuth>(TestAuth.Scheme, _ => { });
            s.Configure<AuthenticationOptions>(o =>
            {
                o.DefaultScheme = TestAuth.Scheme;
                o.DefaultAuthenticateScheme = TestAuth.Scheme;
                o.DefaultChallengeScheme = TestAuth.Scheme;
                o.DefaultForbidScheme = TestAuth.Scheme;
            });
        });
    }

    public DeskDbContext Db() => new(new DbContextOptionsBuilder<DeskDbContext>().UseSqlServer(ConnectionString).Options);

    public async Task EnsureDatabaseAsync()
    {
        await using var db = Db();
        if (await db.Database.EnsureCreatedAsync())
        {
            db.Users.Add(new AppUser { EntraObjectId = AdminOid, DisplayName = "Ada Admin", Email = "ada@shorehotel.com", FirstSeenUtc = DateTime.UtcNow, LastSeenUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
    }

    public HttpClient As(Guid? oid, AppRole role = AppRole.User, bool csrf = true)
    {
        var c = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        if (oid is Guid g)
        {
            c.DefaultRequestHeaders.Add("X-Test-Oid", g.ToString());
            c.DefaultRequestHeaders.Add("X-Test-Role", role.ToString());
        }
        if (csrf) c.DefaultRequestHeaders.Add("X-CSRF", "1");
        return c;
    }

    public HttpClient Admin() => As(AdminOid, AppRole.Admin);

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await using var db = Db();
        await db.Database.EnsureDeletedAsync();
        if (Directory.Exists(StorageRoot)) Directory.Delete(StorageRoot, recursive: true);
    }
}

public class TestAuth(IOptionsMonitor<AuthenticationSchemeOptions> o, ILoggerFactory l, UrlEncoder e)
    : AuthenticationHandler<AuthenticationSchemeOptions>(o, l, e)
{
    public new const string Scheme = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var oid = Request.Headers["X-Test-Oid"].ToString();
        if (string.IsNullOrEmpty(oid)) return Task.FromResult(AuthenticateResult.NoResult());
        var claims = new[]
        {
            new Claim("oid", oid), new Claim("name", "Test person"),
            new Claim(SignInRules.RoleClaim, Request.Headers["X-Test-Role"].ToString()),
        };
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme)), Scheme)));
    }
}

public class ApiTests(DeskAppFactory app) : IClassFixture<DeskAppFactory>, IAsyncLifetime
{
    const int Safety = 18, Engineering = 4;

    public Task InitializeAsync() => app.EnsureDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    async Task<(int Id, Guid Oid)> NewUser(string name, int? department = null)
    {
        var oid = Guid.NewGuid();
        var r = await app.Admin().PostAsJsonAsync("/api/admin/users",
            new AdminEndpoints.NewUserRequest(oid, name, $"{name.Replace(" ", "")}@shorehotel.com", department, 1));
        r.EnsureSuccessStatusCode();
        var u = await r.Content.ReadFromJsonAsync<AdminEndpoints.UserDto>();
        return (u!.Id, oid);
    }

    static async Task<Dictionary<string, string>> Levels(HttpClient c) =>
        (await c.GetFromJsonAsync<MeEndpoints.MeDto>("/api/me"))!.Modules.ToDictionary(m => m.Key, m => m.Level);

    [Fact]
    public async Task Not_signed_in_gets_401_from_the_api() =>
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.As(null).GetAsync("/api/me")).StatusCode);

    [Fact]
    public async Task Signed_in_without_a_DESK_role_is_refused() =>
        Assert.Equal(HttpStatusCode.Forbidden, (await app.As(Guid.NewGuid(), AppRole.None).GetAsync("/api/me")).StatusCode);

    [Fact]
    public async Task Health_check_needs_no_sign_in() =>
        Assert.Equal(HttpStatusCode.OK, (await app.As(null).GetAsync("/health")).StatusCode);

    [Fact]
    public async Task New_person_sees_no_modules()
    {
        var (_, oid) = await NewUser("Nora New");
        Assert.All((await Levels(app.As(oid))).Values, l => Assert.Equal("None", l));
    }

    [Fact]
    public async Task Ordinary_user_cannot_use_admin_screens()
    {
        var (_, oid) = await NewUser("Uma User", Safety);
        Assert.Equal(HttpStatusCode.Forbidden, (await app.As(oid).GetAsync("/api/admin/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await app.As(oid).GetAsync("/api/admin/activity")).StatusCode);
    }

    [Fact]
    public async Task Writes_without_the_csrf_header_are_refused()
    {
        var c = app.As(app.AdminOid, AppRole.Admin, csrf: false);
        var r = await c.PostAsJsonAsync("/api/admin/lists/departments", new ListEndpoints.NameRequest("Spa"));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Department_default_then_personal_setting_then_back()
    {
        var (id, oid) = await NewUser("Owen Officer", Safety);
        var admin = app.Admin();

        (await admin.PutAsJsonAsync($"/api/admin/departments/{Safety}/access/keys",
            new AdminEndpoints.GrantDto("Edit", null, "All", null))).EnsureSuccessStatusCode();
        Assert.Equal("Edit", (await Levels(app.As(oid)))["keys"]);

        (await admin.PutAsJsonAsync($"/api/admin/users/{id}/access/keys",
            new AdminEndpoints.GrantDto("None", null, "All", null))).EnsureSuccessStatusCode();
        Assert.Equal("None", (await Levels(app.As(oid)))["keys"]);

        (await admin.DeleteAsync($"/api/admin/users/{id}/access/keys")).EnsureSuccessStatusCode();
        Assert.Equal("Edit", (await Levels(app.As(oid)))["keys"]);

        await using var db = app.Db();
        Assert.True(await db.ActivityLog.AnyAsync(a => a.Action == "EditAccess" && a.Module == "keys" && a.EntityId == id.ToString()));
    }

    [Fact]
    public async Task Inventory_limited_to_own_department()
    {
        var (_, oid) = await NewUser("Ed Engineer", Engineering);
        (await app.Admin().PutAsJsonAsync($"/api/admin/departments/{Engineering}/access/inventory",
            new AdminEndpoints.GrantDto("Edit", null, "Own", null))).EnsureSuccessStatusCode();
        var inv = (await app.As(oid).GetFromJsonAsync<MeEndpoints.MeDto>("/api/me"))!.Modules.Single(m => m.Key == "inventory");
        Assert.Equal(("Edit", (int?)Engineering), (inv.Level, inv.DepartmentId));
    }

    [Fact]
    public async Task Department_limit_on_keys_is_refused()
    {
        var r = await app.Admin().PutAsJsonAsync($"/api/admin/departments/{Safety}/access/keys",
            new AdminEndpoints.GrantDto("Edit", null, "Own", null));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Remove_needs_the_name_typed_and_ends_access()
    {
        var (id, oid) = await NewUser("Rita Leaving", Safety);
        var admin = app.Admin();
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PostAsJsonAsync($"/api/admin/users/{id}/remove", new AdminEndpoints.RemoveRequest("Rita"))).StatusCode);
        (await admin.PostAsJsonAsync($"/api/admin/users/{id}/remove", new AdminEndpoints.RemoveRequest("rita leaving"))).EnsureSuccessStatusCode();

        await using var db = app.Db();
        var u = await db.Users.SingleAsync(x => x.EntraObjectId == oid);
        Assert.NotNull(u.BlockedUtc);
        Assert.NotNull(u.RemovedUtc);
        var list = await admin.GetFromJsonAsync<List<AdminEndpoints.UserDto>>("/api/admin/users");
        Assert.DoesNotContain(list!, x => x.Id == id);
    }

    [Fact]
    public async Task Admin_cannot_disable_themselves()
    {
        await using var db = app.Db();
        var me = await db.Users.SingleAsync(u => u.EntraObjectId == app.AdminOid);
        Assert.Equal(HttpStatusCode.BadRequest, (await app.Admin().PostAsync($"/api/admin/users/{me.Id}/disable", null)).StatusCode);
    }

    [Fact]
    public async Task Lists_switch_off_instead_of_delete()
    {
        var admin = app.Admin();
        var r = await admin.PostAsJsonAsync("/api/admin/lists/departments", new ListEndpoints.NameRequest("Spa"));
        r.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PostAsJsonAsync("/api/admin/lists/departments", new ListEndpoints.NameRequest("Spa"))).StatusCode);
        Assert.False((await admin.DeleteAsync("/api/admin/lists/departments/1")).IsSuccessStatusCode);
    }

    [Fact]
    public async Task Seeded_lists_are_there()
    {
        var l = await app.Admin().GetFromJsonAsync<MeEndpoints.LookupsDto>("/api/lookups");
        Assert.Equal(["SHO", "SMH", "OVH"], l!.Hotels.Select(h => h.Code));
        Assert.Contains(l.Departments, d => d.Name == "Safety" && d.Id == Safety);
        Assert.Contains(l.Areas, a => a.Name == "Guest Room" && a.HotelId == null);
    }

    [Fact]
    public async Task Unknown_api_address_is_404_not_the_web_page() =>
        Assert.Equal(HttpStatusCode.NotFound, (await app.Admin().GetAsync("/api/nothing-here")).StatusCode);
}
