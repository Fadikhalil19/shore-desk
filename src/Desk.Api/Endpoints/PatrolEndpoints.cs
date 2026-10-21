using Desk.Api.Auth;
using Desk.Api.Services;
using Desk.Core.Access;
using Desk.Core.Data;
using Desk.Core.Patrol;
using Microsoft.EntityFrameworkCore;
using static Desk.Api.Endpoints.Shared;

namespace Desk.Api.Endpoints;

// Patrol: checkpoints (NFC tags with a link), routes, rounds, scans, missed checkpoints
// and problems. Module "patrol": View sees rounds; Edit walks rounds and scans; Manage
// keeps checkpoints and routes and is the only one who sees the tag codes.
public static class PatrolEndpoints
{
    public record CheckpointRequest(int HotelId, string? Name, int? AreaId, string? Instructions, bool IsActive = true);
    public record RouteRequest(int HotelId, string? Name, int ExpectedMinutes, List<int> CheckpointIds, bool IsActive = true);
    public record StartRequest(int RouteId);
    public record EndRequest(string? Notes);
    public record ScanRequest(string? Code, int? CheckpointId, bool Manual, bool Problem, string? Note);

    public record CheckpointDto(int Id, int HotelId, string Name, string? Code, int? AreaId, string? AreaName, string? Instructions, bool IsActive);
    public record StopDto(int CheckpointId, string Name, int Order, string? Instructions);
    public record RouteDto(int Id, int HotelId, string Name, int ExpectedMinutes, bool IsActive, List<StopDto> Stops);
    public record ScanDto(int Id, int CheckpointId, string CheckpointName, DateTime ScannedLocal, string Officer, bool Manual, bool Problem, string? Note, int? RoundId);
    public record RoundDto(int Id, int HotelId, int RouteId, string RouteName, string Officer, DateTime StartedLocal, DateTime? EndedLocal,
        RoundStatus Status, int Stops, int Scanned, int Missed, string? Notes, List<ScanDto>? Scans, List<StopDto>? MissedStops, List<StopDto>? Route);
    public record ScanResult(string Checkpoint, string? Instructions, DateTime ScannedLocal, RoundDto? Round, string? Next);

    public static void MapPatrolEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/patrol").RequireModule(Modules.Patrol, AccessLevel.View);
        var manage = (RouteHandlerBuilder b) => b.RequireModule(Modules.Patrol, AccessLevel.Manage);
        var edit = (RouteHandlerBuilder b) => b.RequireModule(Modules.Patrol, AccessLevel.Edit);
        g.MapGet("/checkpoints", Checkpoints);
        manage(g.MapPost("/checkpoints", (CheckpointRequest r, [AsParameters] Ctx c, CancellationToken ct) => SaveCheckpoint(null, r, c, ct)));
        manage(g.MapPut("/checkpoints/{id:int}", (int id, CheckpointRequest r, [AsParameters] Ctx c, CancellationToken ct) => SaveCheckpoint(id, r, c, ct)));
        manage(g.MapPost("/checkpoints/{id:int}/newcode", NewCode));
        g.MapGet("/routes", Routes);
        manage(g.MapPost("/routes", (RouteRequest r, [AsParameters] Ctx c, CancellationToken ct) => SaveRoute(null, r, c, ct)));
        manage(g.MapPut("/routes/{id:int}", (int id, RouteRequest r, [AsParameters] Ctx c, CancellationToken ct) => SaveRoute(id, r, c, ct)));
        g.MapGet("/rounds", Rounds);
        g.MapGet("/rounds/mine", MyRound);
        g.MapGet("/rounds/{id:int}", Round);
        edit(g.MapPost("/rounds", Start));
        edit(g.MapPost("/rounds/{id:int}/end", (int id, EndRequest r, [AsParameters] Ctx c, CancellationToken ct) => End(id, r, RoundStatus.Completed, c, ct)));
        edit(g.MapPost("/rounds/{id:int}/abandon", (int id, EndRequest r, [AsParameters] Ctx c, CancellationToken ct) => End(id, r, RoundStatus.Abandoned, c, ct)));
        edit(g.MapPost("/scan", Scan));
        g.MapGet("/problems", Problems);
    }

    public record Ctx(DeskDbContext Db, CurrentUser Me, ActivityLogger Log);
    const string M = Modules.Patrol;

    // --- Checkpoints and routes ----------------------------------------------------

    static async Task<IResult> Checkpoints(int hotelId, bool? all, [AsParameters] Ctx c, CancellationToken ct)
    {
        var level = await c.Me.LevelAtAsync(M, hotelId, ct);
        if (level < AccessLevel.View) return Results.NotFound();
        var manager = level >= AccessLevel.Manage;
        var list = await c.Db.Checkpoints.Where(x => x.HotelId == hotelId && (all == true || x.IsActive)).OrderBy(x => x.Name)
            .Select(x => new CheckpointDto(x.Id, x.HotelId, x.Name, manager ? x.Code : null, x.AreaId, x.Area != null ? x.Area.Name : null, x.Instructions, x.IsActive))
            .ToListAsync(ct);
        return Results.Ok(list);
    }

    static async Task<IResult> SaveCheckpoint(int? id, CheckpointRequest req, Ctx c, CancellationToken ct)
    {
        if (await c.Me.LevelAtAsync(M, req.HotelId, ct) < AccessLevel.Manage) return Results.StatusCode(StatusCodes.Status403Forbidden);
        var name = Clean(req.Name, 100);
        if (name is null) return Bad("A name is required.");
        if (await c.Db.Checkpoints.AnyAsync(x => x.HotelId == req.HotelId && x.Name == name && x.Id != id, ct)) return Bad($"There is already a checkpoint called {name}.");
        if (req.AreaId is int a && !await c.Db.LocationAreas.AnyAsync(x => x.Id == a && (x.HotelId == null || x.HotelId == req.HotelId), ct))
            return Bad("Choose a location from this hotel.");
        var cp = id is int i ? await c.Db.Checkpoints.FirstOrDefaultAsync(x => x.Id == i && x.HotelId == req.HotelId, ct)
            : c.Db.Checkpoints.Add(new Checkpoint { HotelId = req.HotelId, Name = name, Code = PatrolRules.NewCode() }).Entity;
        if (cp is null) return Results.NotFound();
        cp.Name = name; cp.AreaId = req.AreaId; cp.Instructions = Clean(req.Instructions, 400); cp.IsActive = req.IsActive;
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync(id is null ? "AddCheckpoint" : "EditCheckpoint", M, "Checkpoint", cp.Id.ToString(), new { cp.Name, cp.IsActive }, ct);
        return Results.Ok(new { cp.Id, cp.Code });
    }

    // A new code makes the old tag link stop working (e.g. a tag was stolen or copied).
    static async Task<IResult> NewCode(int id, [AsParameters] Ctx c, CancellationToken ct)
    {
        var cp = await c.Db.Checkpoints.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (cp is null || await c.Me.LevelAtAsync(M, cp.HotelId, ct) < AccessLevel.Manage) return Results.NotFound();
        cp.Code = PatrolRules.NewCode();
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync("NewCheckpointCode", M, "Checkpoint", cp.Id.ToString(), null, ct);
        return Results.Ok(new { cp.Code });
    }

    static async Task<IResult> Routes(int hotelId, bool? all, [AsParameters] Ctx c, CancellationToken ct)
    {
        if (await c.Me.LevelAtAsync(M, hotelId, ct) < AccessLevel.View) return Results.NotFound();
        var routes = await c.Db.PatrolRoutes.Where(r => r.HotelId == hotelId && (all == true || r.IsActive)).OrderBy(r => r.Name)
            .Include(r => r.Stops).ThenInclude(s => s.Checkpoint).ToListAsync(ct);
        return Results.Ok(routes.Select(ToRoute).ToList());
    }

    static RouteDto ToRoute(PatrolRoute r) => new(r.Id, r.HotelId, r.Name, r.ExpectedMinutes, r.IsActive,
        r.Stops.OrderBy(s => s.Order).Select(s => new StopDto(s.CheckpointId, s.Checkpoint.Name, s.Order, s.Checkpoint.Instructions)).ToList());

    static async Task<IResult> SaveRoute(int? id, RouteRequest req, Ctx c, CancellationToken ct)
    {
        if (await c.Me.LevelAtAsync(M, req.HotelId, ct) < AccessLevel.Manage) return Results.StatusCode(StatusCodes.Status403Forbidden);
        var name = Clean(req.Name, 100);
        if (name is null) return Bad("A name is required.");
        if (req.ExpectedMinutes is < 0 or > 600) return Bad("Check the expected minutes.");
        var ids = req.CheckpointIds ?? [];
        if (ids.Count == 0) return Bad("Add at least one checkpoint.");
        if (ids.Distinct().Count() != ids.Count) return Bad("A checkpoint is on the route twice.");
        var valid = await c.Db.Checkpoints.CountAsync(x => ids.Contains(x.Id) && x.HotelId == req.HotelId && x.IsActive, ct);
        if (valid != ids.Count) return Bad("Every checkpoint must be in use and at this hotel.");
        if (await c.Db.PatrolRoutes.AnyAsync(r => r.HotelId == req.HotelId && r.Name == name && r.Id != id, ct)) return Bad($"There is already a route called {name}.");
        var route = id is int i ? await c.Db.PatrolRoutes.Include(r => r.Stops).FirstOrDefaultAsync(r => r.Id == i && r.HotelId == req.HotelId, ct)
            : c.Db.PatrolRoutes.Add(new PatrolRoute { HotelId = req.HotelId, Name = name }).Entity;
        if (route is null) return Results.NotFound();
        route.Name = name; route.ExpectedMinutes = req.ExpectedMinutes; route.IsActive = req.IsActive;
        c.Db.RouteStops.RemoveRange(route.Stops);   // stops are configuration, not history: replaced as a whole
        route.Stops = ids.Select((cid, n) => new RouteStop { CheckpointId = cid, Order = n + 1 }).ToList();
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync(id is null ? "AddRoute" : "EditRoute", M, "PatrolRoute", route.Id.ToString(), new { route.Name, stops = ids.Count }, ct);
        return Results.Ok(new { route.Id });
    }

    // --- Rounds and scans --------------------------------------------------------------

    static async Task<IResult> Start(StartRequest req, [AsParameters] Ctx c, CancellationToken ct)
    {
        var route = await c.Db.PatrolRoutes.FirstOrDefaultAsync(r => r.Id == req.RouteId && r.IsActive, ct);
        if (route is null || await c.Me.LevelAtAsync(M, route.HotelId, ct) < AccessLevel.Edit) return Results.NotFound();
        var me = await c.Me.GetUserAsync(ct);
        if (me is null) return Bad("Your account is not set up yet.");
        if (await c.Db.PatrolRounds.AnyAsync(r => r.OfficerId == me.Id && r.Status == RoundStatus.InProgress, ct))
            return Bad("You already have a round in progress. Finish or abandon it first.");
        var round = new PatrolRound { HotelId = route.HotelId, RouteId = route.Id, OfficerId = me.Id, StartedLocal = Minute(PacificNow()), Status = RoundStatus.InProgress };
        c.Db.PatrolRounds.Add(round);
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync("StartRound", M, "PatrolRound", round.Id.ToString(), new { route = route.Name }, ct);
        return Results.Ok(new { round.Id });
    }

    static async Task<IResult> MyRound([AsParameters] Ctx c, CancellationToken ct)
    {
        var me = await c.Me.GetUserAsync(ct);
        var id = me is null ? null : await c.Db.PatrolRounds.Where(r => r.OfficerId == me.Id && r.Status == RoundStatus.InProgress).Select(r => (int?)r.Id).FirstOrDefaultAsync(ct);
        return id is int i ? Results.Ok(await LoadRound(c.Db, i, true, ct)) : Results.NoContent();
    }

    static async Task<IResult> Round(int id, [AsParameters] Ctx c, CancellationToken ct)
    {
        var r = await c.Db.PatrolRounds.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null || await c.Me.LevelAtAsync(M, r.HotelId, ct) < AccessLevel.View) return Results.NotFound();
        await c.Log.LogAsync("View", M, "PatrolRound", r.Id.ToString(), null, ct);
        return Results.Ok(await LoadRound(c.Db, id, true, ct));
    }

    static async Task<RoundDto> LoadRound(DeskDbContext db, int id, bool detail, CancellationToken ct)
    {
        var r = await db.PatrolRounds.Include(x => x.Route).ThenInclude(x => x.Stops).ThenInclude(s => s.Checkpoint).FirstAsync(x => x.Id == id, ct);
        var officer = await db.Users.Where(u => u.Id == r.OfficerId).Select(u => u.DisplayName).FirstAsync(ct);
        var scans = await db.CheckpointScans.Where(s => s.RoundId == id).OrderBy(s => s.ScannedLocal)
            .Select(s => new ScanDto(s.Id, s.CheckpointId, s.Checkpoint.Name, s.ScannedLocal, officer, s.Manual, s.Problem, s.Note, s.RoundId)).ToListAsync(ct);
        var route = ToRoute(r.Route).Stops;
        var missedIds = PatrolRules.Missed(route.Select(s => s.CheckpointId), scans.Select(s => s.CheckpointId));
        return new RoundDto(r.Id, r.HotelId, r.RouteId, r.Route.Name, officer, r.StartedLocal, r.EndedLocal, r.Status, route.Count,
            route.Count - missedIds.Count, r.Status == RoundStatus.InProgress ? missedIds.Count : r.Missed, r.Notes,
            detail ? scans : null, detail ? route.Where(s => missedIds.Contains(s.CheckpointId)).ToList() : null, detail ? route : null);
    }

    static async Task<IResult> End(int id, EndRequest req, RoundStatus status, Ctx c, CancellationToken ct)
    {
        var r = await c.Db.PatrolRounds.Include(x => x.Route).ThenInclude(x => x.Stops).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return Results.NotFound();
        var level = await c.Me.LevelAtAsync(M, r.HotelId, ct);
        var uid = (await c.Me.GetUserAsync(ct))?.Id;
        if (level < AccessLevel.Edit || (r.OfficerId != uid && level < AccessLevel.Manage)) return Results.NotFound();
        if (r.Status != RoundStatus.InProgress) return Bad("This round has already ended.");
        var scanned = await c.Db.CheckpointScans.Where(s => s.RoundId == r.Id).Select(s => s.CheckpointId).ToListAsync(ct);
        r.Missed = PatrolRules.Missed(r.Route.Stops.Select(s => s.CheckpointId), scanned).Count;
        r.Status = status;
        r.EndedLocal = Minute(PacificNow());
        r.Notes = Clean(req.Notes, 1000);
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync(status == RoundStatus.Completed ? "EndRound" : "AbandonRound", M, "PatrolRound", r.Id.ToString(), new { r.Missed }, ct);
        return Results.Ok(await LoadRound(c.Db, r.Id, true, ct));
    }

    // A tag tapped: the link carries the checkpoint code. Without NFC an officer may
    // confirm a checkpoint by hand (CheckpointId + Manual); that is marked as manual.
    static async Task<IResult> Scan(ScanRequest req, [AsParameters] Ctx c, CancellationToken ct)
    {
        var code = req.Code?.Trim().ToLowerInvariant();
        var cp = code is { Length: > 0 } ? await c.Db.Checkpoints.FirstOrDefaultAsync(x => x.Code == code && x.IsActive, ct)
            : req.Manual && req.CheckpointId is int cid ? await c.Db.Checkpoints.FirstOrDefaultAsync(x => x.Id == cid && x.IsActive, ct) : null;
        if (cp is null || await c.Me.LevelAtAsync(M, cp.HotelId, ct) < AccessLevel.Edit)
            return Results.NotFound(new { message = "This tag is not a checkpoint you can patrol. Tell a manager if it should be." });
        var me = await c.Me.GetUserAsync(ct);
        if (me is null) return Bad("Your account is not set up yet.");
        var note = Clean(req.Note, 1000);
        if (req.Problem && note is null) return Bad("Describe the problem.");

        var round = await c.Db.PatrolRounds.Include(r => r.Route).ThenInclude(r => r.Stops)
            .FirstOrDefaultAsync(r => r.OfficerId == me.Id && r.Status == RoundStatus.InProgress && r.HotelId == cp.HotelId, ct);
        var now = PacificNow();
        var scan = new CheckpointScan
        {
            HotelId = cp.HotelId, CheckpointId = cp.Id, RoundId = round?.Route.Stops.Any(s => s.CheckpointId == cp.Id) == true ? round.Id : null,
            OfficerId = me.Id, ScannedLocal = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second),
            Manual = code is not { Length: > 0 }, Problem = req.Problem, Note = note,
        };
        c.Db.CheckpointScans.Add(scan);
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync(scan.Manual ? "ManualCheckpoint" : "ScanCheckpoint", M, "Checkpoint", cp.Id.ToString(), new { round = scan.RoundId, scan.Problem }, ct);

        RoundDto? dto = round is null ? null : await LoadRound(c.Db, round.Id, true, ct);
        var next = dto?.Route?.FirstOrDefault(s => dto.MissedStops!.Any(m => m.CheckpointId == s.CheckpointId))?.Name;
        return Results.Ok(new ScanResult(cp.Name, cp.Instructions, scan.ScannedLocal, dto, next));
    }

    static async Task<IResult> Rounds(int hotelId, DateOnly? from, DateOnly? to, [AsParameters] Ctx c, CancellationToken ct)
    {
        if (await c.Me.LevelAtAsync(M, hotelId, ct) < AccessLevel.View) return Results.NotFound();
        var f = (from ?? DateOnly.FromDateTime(PacificNow()).AddDays(-7)).ToDateTime(TimeOnly.MinValue);
        var t = (to ?? DateOnly.FromDateTime(PacificNow())).AddDays(1).ToDateTime(TimeOnly.MinValue);
        var ids = await c.Db.PatrolRounds.Where(r => r.HotelId == hotelId && r.StartedLocal >= f && r.StartedLocal < t)
            .OrderByDescending(r => r.StartedLocal).Take(300).Select(r => r.Id).ToListAsync(ct);
        var list = new List<RoundDto>();
        foreach (var id in ids) list.Add(await LoadRound(c.Db, id, false, ct));
        await c.Log.LogAsync("View", M, "PatrolRounds", hotelId.ToString(), new { from = f, to = t, count = list.Count }, ct);
        return Results.Ok(list);
    }

    static async Task<IResult> Problems(int hotelId, DateOnly? from, DateOnly? to, [AsParameters] Ctx c, CancellationToken ct)
    {
        if (await c.Me.LevelAtAsync(M, hotelId, ct) < AccessLevel.View) return Results.NotFound();
        var f = (from ?? DateOnly.FromDateTime(PacificNow()).AddDays(-30)).ToDateTime(TimeOnly.MinValue);
        var t = (to ?? DateOnly.FromDateTime(PacificNow())).AddDays(1).ToDateTime(TimeOnly.MinValue);
        var list = await c.Db.CheckpointScans.Where(s => s.HotelId == hotelId && s.Problem && s.ScannedLocal >= f && s.ScannedLocal < t)
            .OrderByDescending(s => s.ScannedLocal).Take(500)
            .Select(s => new ScanDto(s.Id, s.CheckpointId, s.Checkpoint.Name, s.ScannedLocal,
                c.Db.Users.Where(u => u.Id == s.OfficerId).Select(u => u.DisplayName).First(), s.Manual, s.Problem, s.Note, s.RoundId))
            .ToListAsync(ct);
        await c.Log.LogAsync("Search", M, "PatrolProblems", hotelId.ToString(), new { from = f, to = t, count = list.Count }, ct);
        return Results.Ok(list);
    }

    static DateTime Minute(DateTime t) => new(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0);
}
