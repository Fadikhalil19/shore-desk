using Desk.Api.Auth;
using Desk.Api.Services;
using Desk.Core.Access;
using Desk.Core.Data;
using Desk.Core.Spaces;
using Microsoft.EntityFrameworkCore;
using static Desk.Api.Endpoints.Shared;

namespace Desk.Api.Endpoints;

// Lockers (/api/lockers, module "lockers") and parking (/api/parking, module
// "parking"): the same endpoints over one table of spaces. View sees who has which
// space; Edit assigns and releases (and sees lock codes); Manage keeps the list of spaces.
public static class SpaceEndpoints
{
    public record SpaceRequest(int HotelId, string? Section, string? Number, string? LockCode, string? LockSerial, string? Notes, bool IsActive = true);
    public record AssignRequest(int? UserId, string? Name, int? DepartmentId, DateOnly? FromDate, string? VehicleMake, string? VehicleModel,
        string? VehicleYear, string? VehicleColor, string? Plate, string? PlateState, string? StickerNumber, string? Notes);
    public record ReleaseRequest(DateOnly? ToDate);

    public record HolderDto(int AssignmentId, int? UserId, string Name, int? DepartmentId, DateOnly FromDate, string? Vehicle, string? Plate,
        string? StickerNumber, string? Notes);
    public record SpaceDto(int Id, int HotelId, string Section, string Number, string? LockCode, string? LockSerial, string? Notes, bool IsActive,
        HolderDto? Holder);
    public record HistoryRow(int AssignmentId, int SpaceId, string Section, string Number, string Name, DateOnly FromDate, DateOnly? ToDate,
        string? Vehicle, string? Plate, string? StickerNumber, string AssignedBy, string? ReleasedBy);

    public static void MapSpaceEndpoints(this WebApplication app)
    {
        Map(app, "lockers", SpaceKind.Locker);
        Map(app, "parking", SpaceKind.Parking);
    }

    static void Map(WebApplication app, string route, SpaceKind kind)
    {
        var module = SpaceRules.Module(kind);
        var g = app.MapGroup($"/api/{route}").RequireModule(module, AccessLevel.View);
        g.MapGet("", (int hotelId, bool? all, [AsParameters] Ctx c, CancellationToken ct) => List(kind, hotelId, all == true, c, ct));
        g.MapPost("", (SpaceRequest r, [AsParameters] Ctx c, CancellationToken ct) => SaveSpace(kind, null, r, c, ct)).RequireModule(module, AccessLevel.Manage);
        g.MapPut("/{id:int}", (int id, SpaceRequest r, [AsParameters] Ctx c, CancellationToken ct) => SaveSpace(kind, id, r, c, ct)).RequireModule(module, AccessLevel.Manage);
        g.MapPost("/{id:int}/assign", (int id, AssignRequest r, [AsParameters] Ctx c, CancellationToken ct) => Assign(kind, id, r, c, ct)).RequireModule(module, AccessLevel.Edit);
        g.MapPost("/{id:int}/release", (int id, ReleaseRequest r, [AsParameters] Ctx c, CancellationToken ct) => Release(kind, id, r, c, ct)).RequireModule(module, AccessLevel.Edit);
        g.MapGet("/history", (int hotelId, int? spaceId, string? q, [AsParameters] Ctx c, CancellationToken ct) => History(kind, hotelId, spaceId, q, c, ct));
    }

    public record Ctx(DeskDbContext Db, CurrentUser Me, ActivityLogger Log);

    static async Task<IResult> List(SpaceKind kind, int hotelId, bool all, Ctx c, CancellationToken ct)
    {
        var level = await c.Me.LevelAtAsync(SpaceRules.Module(kind), hotelId, ct);
        if (level < AccessLevel.View) return Results.NotFound();
        var rows = await c.Db.Spaces.Where(s => s.HotelId == hotelId && s.Kind == kind && (all || s.IsActive))
            .OrderBy(s => s.Section).ThenBy(s => s.Number.Length).ThenBy(s => s.Number)
            .Select(s => new { S = s, A = c.Db.SpaceAssignments.FirstOrDefault(a => a.Id == s.CurrentAssignmentId) })
            .ToListAsync(ct);
        var showCodes = level >= AccessLevel.Edit;
        await c.Log.LogAsync("View", SpaceRules.Module(kind), "SpaceList", hotelId.ToString(), null, ct);
        return Results.Ok(rows.Select(r => new SpaceDto(r.S.Id, r.S.HotelId, r.S.Section, r.S.Number, showCodes ? r.S.LockCode : null,
            showCodes ? r.S.LockSerial : null, r.S.Notes, r.S.IsActive, r.A is null ? null : Holder(r.A))).ToList());
    }

    static HolderDto Holder(SpaceAssignment a) => new(a.Id, a.UserId, a.Name, a.DepartmentId, a.FromDate, Vehicle(a), a.Plate, a.StickerNumber, a.Notes);

    static string? Vehicle(SpaceAssignment a)
    {
        var v = string.Join(" ", new[] { a.VehicleYear, a.VehicleColor, a.VehicleMake, a.VehicleModel }.Where(x => !string.IsNullOrWhiteSpace(x)));
        return v.Length == 0 ? null : v;
    }

    static async Task<IResult> SaveSpace(SpaceKind kind, int? id, SpaceRequest req, Ctx c, CancellationToken ct)
    {
        if (await c.Me.LevelAtAsync(SpaceRules.Module(kind), req.HotelId, ct) < AccessLevel.Manage) return Results.StatusCode(StatusCodes.Status403Forbidden);
        var number = Clean(req.Number, 40);
        if (number is null) return Bad("A number is required.");
        if (req.Section is null || !SpaceRules.Sections(kind).Contains(req.Section)) return Bad("Choose the section.");
        if (await c.Db.Spaces.AnyAsync(s => s.HotelId == req.HotelId && s.Kind == kind && s.Section == req.Section && s.Number == number && s.Id != id, ct))
            return Bad($"{req.Section} {number} already exists.");
        var s = id is int i ? await c.Db.Spaces.FirstOrDefaultAsync(x => x.Id == i && x.Kind == kind && x.HotelId == req.HotelId, ct)
            : c.Db.Spaces.Add(new Space { Kind = kind, HotelId = req.HotelId, Section = req.Section, Number = number }).Entity;
        if (s is null) return Results.NotFound();
        if (!req.IsActive && s.CurrentAssignmentId is not null) return Bad("Release it before taking it out of use.");
        s.Section = req.Section; s.Number = number; s.LockCode = Clean(req.LockCode, 40); s.LockSerial = Clean(req.LockSerial, 60);
        s.Notes = Clean(req.Notes, 400); s.IsActive = req.IsActive;
        await c.Db.SaveChangesAsync(ct);
        // The lock code itself is not copied into the log.
        await c.Log.LogAsync(id is null ? "AddSpace" : "EditSpace", SpaceRules.Module(kind), "Space", s.Id.ToString(), new { s.Section, s.Number, s.IsActive }, ct);
        return Results.Ok(new { s.Id });
    }

    static async Task<IResult> Assign(SpaceKind kind, int id, AssignRequest req, Ctx c, CancellationToken ct)
    {
        var s = await c.Db.Spaces.FirstOrDefaultAsync(x => x.Id == id && x.Kind == kind, ct);
        if (s is null || await c.Me.LevelAtAsync(SpaceRules.Module(kind), s.HotelId, ct) < AccessLevel.Edit) return Results.NotFound();
        if (!s.IsActive) return Bad("This space is out of use.");
        if (s.CurrentAssignmentId is not null) return Bad("This space is already assigned. Release it first.");
        string? name = Clean(req.Name, 200);
        if (req.UserId is int uid)
        {
            name = await c.Db.Users.Where(u => u.Id == uid).Select(u => u.DisplayName).FirstOrDefaultAsync(ct);
            if (name is null) return Bad("That person is not in Shore Desk.");
        }
        if (name is null) return Bad("Choose the person or type their name.");
        if (req.DepartmentId is int d && !await c.Db.Departments.AnyAsync(x => x.Id == d, ct)) return Bad("Unknown department.");
        var me = await c.Me.GetUserAsync(ct);
        if (me is null) return Bad("Your account is not set up yet.");
        var parking = kind == SpaceKind.Parking;
        var a = new SpaceAssignment
        {
            SpaceId = s.Id, HotelId = s.HotelId, UserId = req.UserId, Name = name, DepartmentId = req.DepartmentId,
            FromDate = req.FromDate ?? DateOnly.FromDateTime(PacificNow()),
            VehicleMake = parking ? Clean(req.VehicleMake, 60) : null, VehicleModel = parking ? Clean(req.VehicleModel, 60) : null,
            VehicleYear = parking ? Clean(req.VehicleYear, 10) : null, VehicleColor = parking ? Clean(req.VehicleColor, 60) : null,
            Plate = parking ? Clean(req.Plate, 20)?.ToUpperInvariant() : null, PlateState = parking ? Clean(req.PlateState, 60) : null,
            StickerNumber = parking ? Clean(req.StickerNumber, 40) : null, Notes = Clean(req.Notes, 1000),
            AssignedById = me.Id, CreatedUtc = DateTime.UtcNow,
        };
        c.Db.SpaceAssignments.Add(a);
        await c.Db.SaveChangesAsync(ct);
        s.CurrentAssignmentId = a.Id;
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync("Assign", SpaceRules.Module(kind), "Space", s.Id.ToString(), new { assignment = a.Id, a.Name }, ct);
        return Results.Ok(new { a.Id });
    }

    static async Task<IResult> Release(SpaceKind kind, int id, ReleaseRequest req, Ctx c, CancellationToken ct)
    {
        var s = await c.Db.Spaces.FirstOrDefaultAsync(x => x.Id == id && x.Kind == kind, ct);
        if (s is null || await c.Me.LevelAtAsync(SpaceRules.Module(kind), s.HotelId, ct) < AccessLevel.Edit) return Results.NotFound();
        if (s.CurrentAssignmentId is not int aid) return Bad("This space is not assigned.");
        var a = await c.Db.SpaceAssignments.FirstAsync(x => x.Id == aid, ct);
        var to = req.ToDate ?? DateOnly.FromDateTime(PacificNow());
        if (to < a.FromDate) return Bad("It cannot be released before it was assigned.");
        a.ToDate = to;
        a.ReleasedById = (await c.Me.GetUserAsync(ct))?.Id;
        s.CurrentAssignmentId = null;
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync("Release", SpaceRules.Module(kind), "Space", s.Id.ToString(), new { assignment = a.Id }, ct);
        return Results.Ok();
    }

    static async Task<IResult> History(SpaceKind kind, int hotelId, int? spaceId, string? q, Ctx c, CancellationToken ct)
    {
        if (await c.Me.LevelAtAsync(SpaceRules.Module(kind), hotelId, ct) < AccessLevel.View) return Results.NotFound();
        var query = c.Db.SpaceAssignments.Where(a => a.HotelId == hotelId && a.Space.Kind == kind);
        if (spaceId is int sid) query = query.Where(a => a.SpaceId == sid);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var text = q.Trim();
            query = query.Where(a => a.Name.Contains(text) || a.Plate!.Contains(text) || a.StickerNumber == text || a.Space.Number == text);
        }
        var rows = await query.OrderByDescending(a => a.FromDate).ThenByDescending(a => a.Id).Take(500)
            .Select(a => new { A = a, a.Space.Section, a.Space.Number,
                By = c.Db.Users.Where(u => u.Id == a.AssignedById).Select(u => u.DisplayName).First(),
                Rel = c.Db.Users.Where(u => u.Id == a.ReleasedById).Select(u => u.DisplayName).FirstOrDefault() })
            .ToListAsync(ct);
        await c.Log.LogAsync("Search", SpaceRules.Module(kind), "SpaceHistory", null, new { hotelId, spaceId, q }, ct);
        return Results.Ok(rows.Select(r => new HistoryRow(r.A.Id, r.A.SpaceId, r.Section, r.Number, r.A.Name, r.A.FromDate, r.A.ToDate,
            Vehicle(r.A), r.A.Plate, r.A.StickerNumber, r.By, r.Rel)).ToList());
    }
}
