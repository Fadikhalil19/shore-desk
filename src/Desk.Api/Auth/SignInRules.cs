using System.Security.Claims;

namespace Desk.Api.Auth;

// User = may sign in; what they see is set on the access screen.
// Admin = sees and manages everything, including the access screen.
public enum AppRole { None = 0, User = 1, Admin = 2 }

// Decides who may sign in and with what role. Pure functions so they can be tested.
public static class SignInRules
{
    public const string RoleClaim = ClaimTypes.Role;
    public const string ObjectIdClaim = "http://schemas.microsoft.com/identity/claims/objectidentifier";

    static readonly string[] IdpClaimTypes = ["idp", "http://schemas.microsoft.com/identity/claims/identityprovider"];
    static readonly string[] TenantClaimTypes = ["tid", "http://schemas.microsoft.com/identity/claims/tenantid"];

    public static AppRole RoleFromGroups(IEnumerable<string> groupIds, AppOptions.GroupIds ids)
    {
        var set = new HashSet<string>(groupIds, StringComparer.OrdinalIgnoreCase);
        if (Has(set, ids.Admins)) return AppRole.Admin;
        if (Has(set, ids.Users)) return AppRole.User;
        return AppRole.None;

        static bool Has(HashSet<string> s, string id) => !string.IsNullOrWhiteSpace(id) && s.Contains(id);
    }

    public static Guid? ObjectId(ClaimsPrincipal? user)
    {
        var v = user?.FindFirst(ObjectIdClaim)?.Value ?? user?.FindFirst("oid")?.Value;
        return Guid.TryParse(v, out var g) ? g : null;
    }

    // Returns null when the person may use the app, otherwise the reason they may not.
    public static string? DenyReason(ClaimsPrincipal user, string tenantId, AppOptions options)
    {
        var tid = First(user, TenantClaimTypes);
        if (!string.Equals(tid, tenantId, StringComparison.OrdinalIgnoreCase))
            return "Account is not from the Shore Hotel directory.";

        // Guests (B2B) carry an "idp" claim naming their home directory.
        var idp = First(user, IdpClaimTypes);
        if (!string.IsNullOrEmpty(idp) && !idp.Contains(tenantId, StringComparison.OrdinalIgnoreCase))
            return "Guest accounts are not allowed.";

        var username = user.FindFirst("preferred_username")?.Value ?? user.FindFirst(ClaimTypes.Upn)?.Value ?? "";
        if (username.Contains("#EXT#", StringComparison.OrdinalIgnoreCase))
            return "Guest accounts are not allowed.";

        var at = username.LastIndexOf('@');
        var domain = at >= 0 ? username[(at + 1)..] : "";
        if (options.AllowedEmailDomains.Length > 0 &&
            !options.AllowedEmailDomains.Contains(domain, StringComparer.OrdinalIgnoreCase))
            return "Account domain is not allowed.";

        // Too many groups: Microsoft sends a pointer instead of the list. Fail closed.
        if (user.HasClaim(c => c.Type == "_claim_names" || c.Type == "hasgroups"))
            return "Group list too large to read; ask the administrator.";

        var groups = user.FindAll("groups").Select(c => c.Value);
        if (RoleFromGroups(groups, options.Groups) == AppRole.None)
            return "Not a member of DESK-Users or DESK-Admins.";

        return null;
    }

    static string? First(ClaimsPrincipal user, string[] types) =>
        types.Select(t => user.FindFirst(t)?.Value).FirstOrDefault(v => !string.IsNullOrEmpty(v));
}
