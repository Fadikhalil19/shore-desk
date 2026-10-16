using System.Security.Claims;
using Desk.Api;
using Desk.Api.Auth;
using Desk.Api.Endpoints;

namespace Desk.Api.Tests;

public class SignInRulesTests
{
    const string Tenant = "c7319400-c050-406d-b027-2eb54f8b6f90";
    const string Users = "11111111-1111-1111-1111-111111111111";
    const string Admins = "22222222-2222-2222-2222-222222222222";

    static readonly AppOptions Options = new()
    {
        AllowedEmailDomains = ["shorehotel.com", "oceanviewsantamonica.com"],
        Groups = new AppOptions.GroupIds { Users = Users, Admins = Admins }
    };

    static ClaimsPrincipal Person(string username = "jane@shorehotel.com", string tenant = Tenant,
        string? idp = null, params string[] groups)
    {
        var claims = new List<Claim> { new("tid", tenant), new("preferred_username", username), new("name", "Jane") };
        if (idp is not null) claims.Add(new Claim("idp", idp));
        claims.AddRange(groups.Select(g => new Claim("groups", g)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    [Fact]
    public void Member_of_DESK_Users_is_allowed() =>
        Assert.Null(SignInRules.DenyReason(Person(groups: Users), Tenant, Options));

    [Fact]
    public void No_DESK_group_is_denied() =>
        Assert.NotNull(SignInRules.DenyReason(Person(groups: "some-other-group"), Tenant, Options));

    [Fact]
    public void Other_tenant_is_denied() =>
        Assert.NotNull(SignInRules.DenyReason(Person(tenant: Guid.NewGuid().ToString(), groups: Admins), Tenant, Options));

    [Fact]
    public void Guest_is_denied() =>
        Assert.NotNull(SignInRules.DenyReason(Person(idp: "https://sts.windows.net/other-tenant/", groups: Users), Tenant, Options));

    [Fact]
    public void Guest_username_is_denied() =>
        Assert.NotNull(SignInRules.DenyReason(Person(username: "bob_gmail.com#EXT#@sandandseainc.onmicrosoft.com", groups: Users), Tenant, Options));

    [Fact]
    public void Other_domain_is_denied() =>
        Assert.NotNull(SignInRules.DenyReason(Person(username: "bob@gmail.com", groups: Users), Tenant, Options));

    [Fact]
    public void Second_company_domain_is_allowed() =>
        Assert.Null(SignInRules.DenyReason(Person(username: "amy@oceanviewsantamonica.com", groups: Users), Tenant, Options));

    [Fact]
    public void Too_many_groups_fails_closed()
    {
        var p = Person(groups: Users);
        ((ClaimsIdentity)p.Identity!).AddClaim(new Claim("hasgroups", "true"));
        Assert.NotNull(SignInRules.DenyReason(p, Tenant, Options));
    }

    [Fact]
    public void Admin_group_wins_over_users_group() =>
        Assert.Equal(AppRole.Admin, SignInRules.RoleFromGroups([Users, Admins], Options.Groups));

    [Fact]
    public void Empty_group_ids_never_match() =>
        Assert.Equal(AppRole.None, SignInRules.RoleFromGroups([""], new AppOptions.GroupIds()));

    [Theory]
    [InlineData("/p/lobby-1", "/p/lobby-1")]
    [InlineData(null, "/")]
    [InlineData("https://evil.example", "/")]
    [InlineData("//evil.example", "/")]
    [InlineData("/\\evil.example", "/")]
    public void Return_address_after_sign_in_stays_on_this_site(string? input, string expected) =>
        Assert.Equal(expected, AuthEndpoints.SafeLocal(input));
}
