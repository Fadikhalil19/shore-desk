using System.Security.Claims;
using Desk.Core.Access;
using Desk.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Desk.Api.Auth;

// The signed-in person for this request, with their role and module access.
public class CurrentUser(IHttpContextAccessor http, DeskDbContext db)
{
    AppUser? _user;
    Dictionary<string, EffectiveAccess>? _access;

    ClaimsPrincipal Principal => http.HttpContext?.User ?? new ClaimsPrincipal();

    public bool IsAuthenticated => Principal.Identity?.IsAuthenticated == true;

    public AppRole Role =>
        Enum.TryParse<AppRole>(Principal.FindFirst(SignInRules.RoleClaim)?.Value, out var r) ? r : AppRole.None;

    public bool IsAdmin => Role == AppRole.Admin;

    public string Name => Principal.FindFirst("name")?.Value ?? Principal.Identity?.Name ?? "unknown";

    public Guid? ObjectId => SignInRules.ObjectId(Principal);

    public async Task<AppUser?> GetUserAsync(CancellationToken ct = default)
    {
        if (_user is not null || ObjectId is not Guid oid) return _user;
        _user = await db.Users.FirstOrDefaultAsync(u => u.EntraObjectId == oid, ct);
        return _user;
    }

    // What this person may do in every module (default deny).
    public async Task<Dictionary<string, EffectiveAccess>> GetAccessAsync(CancellationToken ct = default)
    {
        if (_access is not null) return _access;
        var u = await GetUserAsync(ct);
        _access = Role == AppRole.None ? AccessLoader.NoAccess()
            : IsAdmin ? AccessLoader.AdminAccess()
            : u is null ? AccessLoader.NoAccess()
            : await AccessLoader.ForUserAsync(db, u, ct);
        return _access;
    }

    public async Task<EffectiveAccess> AccessToAsync(string module, CancellationToken ct = default) =>
        (await GetAccessAsync(ct)).TryGetValue(module, out var a) ? a : new EffectiveAccess(module, AccessLevel.None, null, null, "None");
}

// Reads the stored grants and applies the rules in Desk.Core.Access.AccessRules.
public static class AccessLoader
{
    public static Dictionary<string, EffectiveAccess> NoAccess() =>
        Modules.All.ToDictionary(m => m.Key, m => new EffectiveAccess(m.Key, AccessLevel.None, null, null, "None"));

    public static Dictionary<string, EffectiveAccess> AdminAccess() =>
        Modules.All.ToDictionary(m => m.Key, m => AccessRules.Resolve(m, true, null, null, null));

    public static async Task<Dictionary<string, EffectiveAccess>> ForUserAsync(DeskDbContext db, AppUser u, CancellationToken ct)
    {
        var personal = await db.UserAccess.Where(a => a.AppUserId == u.Id).ToDictionaryAsync(a => a.Module, ct);
        var defaults = u.DepartmentId is int d
            ? await db.DepartmentAccess.Where(a => a.DepartmentId == d).ToDictionaryAsync(a => a.Module, ct)
            : [];
        return Modules.All.ToDictionary(m => m.Key, m => AccessRules.Resolve(m, false, u.DepartmentId,
            personal.GetValueOrDefault(m.Key)?.ToSpec(), defaults.GetValueOrDefault(m.Key)?.ToSpec()));
    }
}
