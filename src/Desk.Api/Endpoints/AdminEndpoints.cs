using Desk.Api.Auth;
using Desk.Api.Services;
using Desk.Core.Access;
using Desk.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Desk.Api.Endpoints;

// Admin-only: people (department, hotel, Disable / Remove), each person's access per
// module, and the default access of each department. Entra only decides who may
// sign in at all (DESK-Users / DESK-Admins).
public static class AdminEndpoints
{
    public record UserDto(int Id, Guid EntraObjectId, string Name, string? Email, string? JobTitle, int? DepartmentId, int? HotelId,
        DateTime LastSeenUtc, DateTime? FirstSignInUtc, DateTime? BlockedUtc, DateTime? RemovedUtc, int PersonalSettings);
    public record NewUserRequest(Guid EntraObjectId, string Name, string? Email, int? DepartmentId, int? HotelId);
    public record UpdateUserRequest(int? DepartmentId, int? HotelId, string? JobTitle);
    public record RemoveRequest(string ConfirmName);

    public record GrantDto(string Level, int? HotelId, string DepartmentLimit, int? DepartmentId);
    public record ModuleRow(string Module, GrantDto? Personal, GrantDto? DepartmentDefault, MeEndpoints.ModuleAccessDto Effective);
    public record PersonAccess(UserDto Person, string? DepartmentName, List<ModuleRow> Modules);
    public record DepartmentRow(string Module, GrantDto? Grant);

    public static void MapAdminEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/admin").RequireAuthorization(Policies.Admin);

        // --- People ---
        g.MapGet("/users", async (bool? includeRemoved, DeskDbContext db, CancellationToken ct) =>
            await db.Users.Where(u => includeRemoved == true || u.RemovedUtc == null).OrderBy(u => u.DisplayName)
                .Select(u => ToDto(u, db.UserAccess.Count(a => a.AppUserId == u.Id)))
                .ToListAsync(ct));

        // Add someone before their first sign-in, from their Entra Object ID
        // (Entra admin center > Users > the person > Object ID).
        g.MapPost("/users", async (NewUserRequest req, DeskDbContext db, ActivityLogger log, CancellationToken ct) =>
        {
            if (req.EntraObjectId == Guid.Empty || string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest(new { message = "Object ID and name are required." });
            if (await db.Users.AnyAsync(u => u.EntraObjectId == req.EntraObjectId, ct))
                return Results.BadRequest(new { message = "That person is already in the list (show removed people to find them)." });
            if (await CheckListsAsync(db, req.DepartmentId, req.HotelId, ct) is string bad) return Results.BadRequest(new { message = bad });
            var now = DateTime.UtcNow;
            var u = new AppUser
            {
                EntraObjectId = req.EntraObjectId, DisplayName = req.Name.Trim(), Email = req.Email?.Trim(),
                DepartmentId = req.DepartmentId, HotelId = req.HotelId, FirstSeenUtc = now, LastSeenUtc = now
            };
            db.Users.Add(u);
            await db.SaveChangesAsync(ct);
            await log.LogAsync("AddUser", null, "AppUser", u.Id.ToString(), req, ct);
            return Results.Ok(ToDto(u, 0));
        });

        g.MapPut("/users/{id:int}", async (int id, UpdateUserRequest req, DeskDbContext db, ActivityLogger log, CancellationToken ct) =>
        {
            var u = await db.Users.FindAsync([id], ct);
            if (u is null) return Results.NotFound();
            if (await CheckListsAsync(db, req.DepartmentId, req.HotelId, ct) is string bad) return Results.BadRequest(new { message = bad });
            var before = new { u.DepartmentId, u.HotelId, u.JobTitle };
            u.DepartmentId = req.DepartmentId;
            u.HotelId = req.HotelId;
            u.JobTitle = string.IsNullOrWhiteSpace(req.JobTitle) ? null : req.JobTitle.Trim();
            await db.SaveChangesAsync(ct);
            await log.LogAsync("EditUser", null, "AppUser", id.ToString(), new { u.DisplayName, before, after = req }, ct);
            return Results.NoContent();
        });

        // Disable: refused at sign-in, open session ends. Reversible.
        g.MapPost("/users/{id:int}/disable", (int id, CurrentUser me, DeskDbContext db, ActivityLogger log, CancellationToken ct) =>
            Change(id, me, db, log, "DisableUser", u => u.BlockedUtc ??= DateTime.UtcNow, ct));

        g.MapPost("/users/{id:int}/enable", (int id, CurrentUser me, DeskDbContext db, ActivityLogger log, CancellationToken ct) =>
            Change(id, me, db, log, "EnableUser", u => { u.BlockedUtc = null; u.RemovedUtc = null; }, ct));

        // Remove: disable, clear their personal access and hide them from the list.
        // History stays in the activity log. The admin must type the person's name to confirm.
        g.MapPost("/users/{id:int}/remove", async (int id, RemoveRequest req, CurrentUser me, DeskDbContext db,
            ActivityLogger log, CancellationToken ct) =>
        {
            var u = await db.Users.FindAsync([id], ct);
            if (u is null) return Results.NotFound();
            if (!string.Equals(req.ConfirmName?.Trim(), u.DisplayName.Trim(), StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest(new { message = $"Type the name exactly as shown (\"{u.DisplayName}\") to confirm." });
            var personal = await db.UserAccess.Where(a => a.AppUserId == id).ToListAsync(ct);
            return await Change(id, me, db, log, "RemoveUser", x =>
            {
                x.BlockedUtc ??= DateTime.UtcNow;
                x.RemovedUtc = DateTime.UtcNow;
                db.UserAccess.RemoveRange(personal);
            }, ct, new { clearedAccess = personal.Select(a => new { a.Module, a.Level }) });
        });

        // --- One person's access ---
        g.MapGet("/users/{id:int}/access", async (int id, DeskDbContext db, ActivityLogger log, CancellationToken ct) =>
        {
            var u = await db.Users.FindAsync([id], ct);
            if (u is null) return Results.NotFound();
            var result = await PersonAccessAsync(db, u, ct);
            await log.LogAsync("ViewAccess", null, "AppUser", id.ToString(), ct: ct);
            return Results.Ok(result);
        });

        g.MapPut("/users/{id:int}/access/{module}", async (int id, string module, GrantDto req, DeskDbContext db,
            ActivityLogger log, CancellationToken ct) =>
        {
            var m = Modules.Find(module);
            var u = await db.Users.FindAsync([id], ct);
            if (m is null || u is null) return Results.NotFound();
            if (u.RemovedUtc is not null) return Results.BadRequest(new { message = "This person was removed. Restore them first." });
            var parsed = await ParseAsync(db, m, req, ct);
            if (parsed.Error is not null) return Results.BadRequest(new { message = parsed.Error });
            var row = await db.UserAccess.FirstOrDefaultAsync(a => a.AppUserId == id && a.Module == m.Key, ct);
            var before = row is null ? null : ToGrantDto(row);
            row ??= db.UserAccess.Add(new UserAccess { AppUserId = id, Module = m.Key }).Entity;
            Apply(row, parsed.Spec!);
            await db.SaveChangesAsync(ct);
            await log.LogAsync("EditAccess", m.Key, "AppUser", id.ToString(), new { u.DisplayName, before, after = req }, ct);
            return Results.Ok(await PersonAccessAsync(db, u, ct));
        });

        // Back to the department default for this module.
        g.MapDelete("/users/{id:int}/access/{module}", async (int id, string module, DeskDbContext db,
            ActivityLogger log, CancellationToken ct) =>
        {
            var m = Modules.Find(module);
            var u = await db.Users.FindAsync([id], ct);
            if (m is null || u is null) return Results.NotFound();
            var row = await db.UserAccess.FirstOrDefaultAsync(a => a.AppUserId == id && a.Module == m.Key, ct);
            if (row is not null)
            {
                db.UserAccess.Remove(row);
                await db.SaveChangesAsync(ct);
                await log.LogAsync("EditAccess", m.Key, "AppUser", id.ToString(),
                    new { u.DisplayName, before = ToGrantDto(row), after = "department default" }, ct);
            }
            return Results.Ok(await PersonAccessAsync(db, u, ct));
        });

        // --- Department defaults ---
        g.MapGet("/departments/{id:int}/access", async (int id, DeskDbContext db, CancellationToken ct) =>
        {
            if (!await db.Departments.AnyAsync(d => d.Id == id, ct)) return Results.NotFound();
            return Results.Ok(await DepartmentRowsAsync(db, id, ct));
        });

        g.MapPut("/departments/{id:int}/access/{module}", async (int id, string module, GrantDto req, DeskDbContext db,
            ActivityLogger log, CancellationToken ct) =>
        {
            var m = Modules.Find(module);
            var d = await db.Departments.FindAsync([id], ct);
            if (m is null || d is null) return Results.NotFound();
            var parsed = await ParseAsync(db, m, req, ct);
            if (parsed.Error is not null) return Results.BadRequest(new { message = parsed.Error });
            var row = await db.DepartmentAccess.FirstOrDefaultAsync(a => a.DepartmentId == id && a.Module == m.Key, ct);
            var before = row is null ? null : ToGrantDto(row);
            row ??= db.DepartmentAccess.Add(new DepartmentAccess { DepartmentId = id, Module = m.Key }).Entity;
            Apply(row, parsed.Spec!);
            await db.SaveChangesAsync(ct);
            await log.LogAsync("EditDepartmentAccess", m.Key, "Department", id.ToString(), new { department = d.Name, before, after = req }, ct);
            return Results.Ok(await DepartmentRowsAsync(db, id, ct));
        });

        g.MapDelete("/departments/{id:int}/access/{module}", async (int id, string module, DeskDbContext db,
            ActivityLogger log, CancellationToken ct) =>
        {
            var m = Modules.Find(module);
            var d = await db.Departments.FindAsync([id], ct);
            if (m is null || d is null) return Results.NotFound();
            var row = await db.DepartmentAccess.FirstOrDefaultAsync(a => a.DepartmentId == id && a.Module == m.Key, ct);
            if (row is not null)
            {
                db.DepartmentAccess.Remove(row);
                await db.SaveChangesAsync(ct);
                await log.LogAsync("EditDepartmentAccess", m.Key, "Department", id.ToString(),
                    new { department = d.Name, before = ToGrantDto(row), after = "none" }, ct);
            }
            return Results.Ok(await DepartmentRowsAsync(db, id, ct));
        });
    }

    static UserDto ToDto(AppUser u, int personal) => new(u.Id, u.EntraObjectId, u.DisplayName, u.Email, u.JobTitle,
        u.DepartmentId, u.HotelId, u.LastSeenUtc, u.FirstSignInUtc, u.BlockedUtc, u.RemovedUtc, personal);

    static GrantDto ToGrantDto(AccessGrantBase a) =>
        new(a.Level.ToString(), a.LimitHotelId, a.DepartmentLimit.ToString(), a.LimitDepartmentId);

    static void Apply(AccessGrantBase row, GrantSpec s)
    {
        row.Level = s.Level;
        row.LimitHotelId = s.HotelId;
        row.DepartmentLimit = s.DepartmentLimit;
        row.LimitDepartmentId = s.DepartmentId;
        row.UpdatedUtc = DateTime.UtcNow;
    }

    record Parsed(GrantSpec? Spec, string? Error);

    static async Task<Parsed> ParseAsync(DeskDbContext db, ModuleInfo m, GrantDto req, CancellationToken ct)
    {
        if (!Enum.TryParse<AccessLevel>(req.Level, true, out var level) || !Enum.IsDefined(level))
            return new(null, "Unknown access level.");
        if (!Enum.TryParse<DepartmentLimit>(req.DepartmentLimit ?? "All", true, out var limit) || !Enum.IsDefined(limit))
            return new(null, "Unknown department limit.");
        var spec = new GrantSpec(level, req.HotelId, limit, req.DepartmentId);
        if (AccessRules.Validate(m, spec) is string problem) return new(null, problem);
        if (req.HotelId is int h && !await db.Hotels.AnyAsync(x => x.Id == h, ct)) return new(null, "Unknown hotel.");
        if (req.DepartmentId is int d && !await db.Departments.AnyAsync(x => x.Id == d, ct)) return new(null, "Unknown department.");
        return new(spec, null);
    }

    static async Task<string?> CheckListsAsync(DeskDbContext db, int? departmentId, int? hotelId, CancellationToken ct)
    {
        if (departmentId is int d && !await db.Departments.AnyAsync(x => x.Id == d, ct)) return "Unknown department.";
        if (hotelId is int h && !await db.Hotels.AnyAsync(x => x.Id == h, ct)) return "Unknown hotel.";
        return null;
    }

    static async Task<PersonAccess> PersonAccessAsync(DeskDbContext db, AppUser u, CancellationToken ct)
    {
        var personal = await db.UserAccess.Where(a => a.AppUserId == u.Id).ToDictionaryAsync(a => a.Module, ct);
        var defaults = u.DepartmentId is int d
            ? await db.DepartmentAccess.Where(a => a.DepartmentId == d).ToDictionaryAsync(a => a.Module, ct)
            : [];
        var effective = await AccessLoader.ForUserAsync(db, u, ct);
        var deptName = u.DepartmentId is int di ? await db.Departments.Where(x => x.Id == di).Select(x => x.Name).FirstOrDefaultAsync(ct) : null;
        return new PersonAccess(ToDto(u, personal.Count), deptName, Modules.All.Select(m => new ModuleRow(m.Key,
            personal.TryGetValue(m.Key, out var p) ? ToGrantDto(p) : null,
            defaults.TryGetValue(m.Key, out var dd) ? ToGrantDto(dd) : null,
            MeEndpoints.ToDto(m, effective[m.Key]))).ToList());
    }

    static async Task<List<DepartmentRow>> DepartmentRowsAsync(DeskDbContext db, int departmentId, CancellationToken ct)
    {
        var rows = await db.DepartmentAccess.Where(a => a.DepartmentId == departmentId).ToDictionaryAsync(a => a.Module, ct);
        return Modules.All.Select(m => new DepartmentRow(m.Key, rows.TryGetValue(m.Key, out var r) ? ToGrantDto(r) : null)).ToList();
    }

    static async Task<IResult> Change(int id, CurrentUser me, DeskDbContext db, ActivityLogger log, string action,
        Action<AppUser> apply, CancellationToken ct, object? extra = null)
    {
        var u = await db.Users.FindAsync([id], ct);
        if (u is null) return Results.NotFound();
        if ((await me.GetUserAsync(ct))?.Id == id)
            return Results.BadRequest(new { message = "You cannot disable or remove your own account." });
        apply(u);
        await db.SaveChangesAsync(ct);
        await log.LogAsync(action, null, "AppUser", id.ToString(), new { u.DisplayName, u.Email, extra }, ct);
        return Results.NoContent();
    }
}
