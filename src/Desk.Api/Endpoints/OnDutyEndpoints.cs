using Desk.Api.Auth;
using Desk.Api.Services;
using Desk.Core.Access;
using Desk.Core.Data;
using Desk.Core.Handover;
using Desk.Core.Incidents;
using Desk.Core.OnDuty;
using Microsoft.EntityFrameworkCore;
using static Desk.Api.Endpoints.Shared;

namespace Desk.Api.Endpoints;

// On duty: the roster of one hotel + shift, and the call-out (sick call) log.
// Module "onduty": View sees them, Edit keeps them. Removing only hides.
public static class OnDutyEndpoints
{
    public record AssignmentRequest(int HotelId, DateOnly ShiftDate, Shift Shift, int? UserId, string? Name, int? DepartmentId,
        string? Radio, string? Post, DateTime StartLocal, DateTime EndLocal);
    public record CallOutRequest(int HotelId, DateOnly ShiftDate, Shift Shift, int? UserId, string? Name, int? DepartmentId,
        DateTime? CalledLocal, string? Reason, string? Notes, string? CoveredBy);
    public record CopyRequest(int HotelId, DateOnly ShiftDate, Shift Shift, DateOnly FromDate, Shift FromShift);

    public record AssignmentDto(int Id, int? UserId, string Name, int? DepartmentId, string? Radio, string? Post,
        DateTime StartLocal, DateTime EndLocal);
    public record CallOutDto(int Id, int HotelId, DateOnly ShiftDate, Shift Shift, int? UserId, string Name, int? DepartmentId,
        DateTime CalledLocal, string Reason, string? Notes, string? CoveredBy, string TakenBy);
    public record RosterDto(int HotelId, DateOnly ShiftDate, Shift Shift, DateTime StartLocal, DateTime EndLocal,
        DateOnly PreviousDate, Shift PreviousShift, DateOnly CurrentDate, Shift CurrentShift,
        List<AssignmentDto> Roster, List<CallOutDto> CallOuts, bool CanWrite);

    public static void MapOnDutyEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/onduty").RequireModule(Modules.OnDuty, AccessLevel.View);
        g.MapGet("", Get);
        g.MapPost("/assignments", (AssignmentRequest r, [AsParameters] Ctx c, CancellationToken ct) => SaveAssignment(null, r, c, ct))
            .RequireModule(Modules.OnDuty, AccessLevel.Edit);
        g.MapPut("/assignments/{id:int}", (int id, AssignmentRequest r, [AsParameters] Ctx c, CancellationToken ct) => SaveAssignment(id, r, c, ct))
            .RequireModule(Modules.OnDuty, AccessLevel.Edit);
        g.MapPost("/assignments/{id:int}/remove", RemoveAssignment).RequireModule(Modules.OnDuty, AccessLevel.Edit);
        g.MapPost("/copy", Copy).RequireModule(Modules.OnDuty, AccessLevel.Edit);
        g.MapGet("/callouts", SearchCallOuts);
        g.MapPost("/callouts", (CallOutRequest r, [AsParameters] Ctx c, CancellationToken ct) => SaveCallOut(null, r, c, ct))
            .RequireModule(Modules.OnDuty, AccessLevel.Edit);
        g.MapPut("/callouts/{id:int}", (int id, CallOutRequest r, [AsParameters] Ctx c, CancellationToken ct) => SaveCallOut(id, r, c, ct))
            .RequireModule(Modules.OnDuty, AccessLevel.Edit);
        g.MapPost("/callouts/{id:int}/remove", RemoveCallOut).RequireModule(Modules.OnDuty, AccessLevel.Edit);
    }

    public record Ctx(DeskDbContext Db, CurrentUser Me, ActivityLogger Log);

    static async Task<IResult> Get(int hotelId, DateOnly? date, Shift? shift, [AsParameters] Ctx c, CancellationToken ct)
    {
        var level = await c.Me.LevelAtAsync(Modules.OnDuty, hotelId, ct);
        if (level < AccessLevel.View) return Results.NotFound();
        var (curDate, curShift) = HandoverRules.ShiftOf(PacificNow());
        var d = date ?? curDate;
        var s = shift ?? curShift;
        var (start, end) = HandoverRules.Window(d, s);
        var (prevDate, prevShift) = HandoverRules.Previous(d, s);

        var roster = await Roster(c.Db, hotelId, d, s, ct);
        var callOuts = await CallOutsFor(c.Db, hotelId, d, s, ct);
        await c.Log.LogAsync("View", Modules.OnDuty, "Roster", $"{hotelId}/{d:yyyy-MM-dd}/{s}", null, ct);
        return Results.Ok(new RosterDto(hotelId, d, s, start, end, prevDate, prevShift, curDate, curShift, roster, callOuts,
            level >= AccessLevel.Edit));
    }

    public static Task<List<AssignmentDto>> Roster(DeskDbContext db, int hotelId, DateOnly d, Shift s, CancellationToken ct) =>
        db.DutyAssignments.Where(a => a.HotelId == hotelId && a.ShiftDate == d && a.Shift == s && a.RemovedUtc == null)
            .OrderBy(a => a.StartLocal).ThenBy(a => a.Id)
            .Select(a => new AssignmentDto(a.Id, a.UserId,
                a.UserId != null ? db.Users.Where(u => u.Id == a.UserId).Select(u => u.DisplayName).First() : a.Name!,
                a.DepartmentId, a.Radio, a.Post, a.StartLocal, a.EndLocal))
            .ToListAsync(ct);

    // Also used by the handover page.
    public static Task<List<CallOutDto>> CallOutsFor(DeskDbContext db, int hotelId, DateOnly d, Shift s, CancellationToken ct) =>
        Project(db, db.CallOuts.Where(x => x.HotelId == hotelId && x.ShiftDate == d && x.Shift == s && x.RemovedUtc == null))
            .ToListAsync(ct);

    static IQueryable<CallOutDto> Project(DeskDbContext db, IQueryable<CallOut> q) =>
        q.OrderBy(x => x.CalledLocal).Select(x => new CallOutDto(x.Id, x.HotelId, x.ShiftDate, x.Shift, x.UserId,
            x.UserId != null ? db.Users.Where(u => u.Id == x.UserId).Select(u => u.DisplayName).First() : x.Name!,
            x.DepartmentId, x.CalledLocal, x.Reason, x.Notes, x.CoveredBy,
            db.Users.Where(u => u.Id == x.CreatedById).Select(u => u.DisplayName).First()));

    static async Task<IResult> SaveAssignment(int? id, AssignmentRequest req, Ctx c, CancellationToken ct)
    {
        if (await c.Me.LevelAtAsync(Modules.OnDuty, req.HotelId, ct) < AccessLevel.Edit) return Results.StatusCode(StatusCodes.Status403Forbidden);
        if (await CheckPerson(req.HotelId, req.UserId, req.Name, req.DepartmentId, c.Db, ct) is { } bad) return Bad(bad);
        if (!Enum.IsDefined(req.Shift)) return Bad("Choose the shift.");
        if (OnDutyRules.CheckTimes(req.ShiftDate, req.Shift, req.StartLocal, req.EndLocal) is { } timeError) return Bad(timeError);
        var me = await c.Me.GetUserAsync(ct);
        if (me is null) return Bad("Your account is not set up yet.");

        var now = DateTime.UtcNow;
        var a = id is int i ? await c.Db.DutyAssignments.FirstOrDefaultAsync(x => x.Id == i && x.RemovedUtc == null, ct)
            : c.Db.DutyAssignments.Add(new DutyAssignment { CreatedById = me.Id, CreatedUtc = now }).Entity;
        if (a is null || (id is not null && await c.Me.LevelAtAsync(Modules.OnDuty, a.HotelId, ct) < AccessLevel.Edit)) return Results.NotFound();
        if (req.UserId is int uid && await c.Db.DutyAssignments.AnyAsync(x => x.Id != a.Id && x.RemovedUtc == null && x.UserId == uid
                && x.HotelId == req.HotelId && x.ShiftDate == req.ShiftDate && x.Shift == req.Shift, ct))
            return Bad("That person is already on this shift's roster.");

        a.HotelId = req.HotelId; a.ShiftDate = req.ShiftDate; a.Shift = req.Shift; a.UserId = req.UserId;
        a.Name = req.UserId is null ? Clean(req.Name, 200) : null; a.DepartmentId = req.DepartmentId;
        a.Radio = Clean(req.Radio, 40); a.Post = Clean(req.Post, 100);
        a.StartLocal = Minute(req.StartLocal); a.EndLocal = Minute(req.EndLocal); a.UpdatedUtc = now;
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync(id is null ? "AddToRoster" : "EditRoster", Modules.OnDuty, "DutyAssignment", a.Id.ToString(),
            new { a.HotelId, a.ShiftDate, a.Shift, a.UserId, a.Name }, ct);
        return Results.Ok(new { a.Id });
    }

    static async Task<IResult> RemoveAssignment(int id, [AsParameters] Ctx c, CancellationToken ct)
    {
        var a = await c.Db.DutyAssignments.FirstOrDefaultAsync(x => x.Id == id && x.RemovedUtc == null, ct);
        if (a is null || await c.Me.LevelAtAsync(Modules.OnDuty, a.HotelId, ct) < AccessLevel.Edit) return Results.NotFound();
        a.RemovedUtc = a.UpdatedUtc = DateTime.UtcNow;
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync("RemoveFromRoster", Modules.OnDuty, "DutyAssignment", a.Id.ToString(), null, ct);
        return Results.Ok();
    }

    // Copies another shift's roster (e.g. the same shift yesterday), moving the times
    // by the same number of days. People already on the target roster are skipped.
    static async Task<IResult> Copy(CopyRequest req, [AsParameters] Ctx c, CancellationToken ct)
    {
        if (await c.Me.LevelAtAsync(Modules.OnDuty, req.HotelId, ct) < AccessLevel.Edit) return Results.StatusCode(StatusCodes.Status403Forbidden);
        var me = await c.Me.GetUserAsync(ct);
        if (me is null) return Bad("Your account is not set up yet.");
        var source = await c.Db.DutyAssignments.Where(a => a.HotelId == req.HotelId && a.ShiftDate == req.FromDate
            && a.Shift == req.FromShift && a.RemovedUtc == null).ToListAsync(ct);
        if (source.Count == 0) return Bad("That shift has nobody on its roster.");
        var existing = await c.Db.DutyAssignments.Where(a => a.HotelId == req.HotelId && a.ShiftDate == req.ShiftDate
            && a.Shift == req.Shift && a.RemovedUtc == null).ToListAsync(ct);

        // Times keep their clock time; the day moves to the target shift's date.
        var (fromStart, _) = HandoverRules.Window(req.FromDate, req.FromShift);
        var (toStart, _) = HandoverRules.Window(req.ShiftDate, req.Shift);
        var offset = toStart.Date - fromStart.Date;
        var now = DateTime.UtcNow;
        var added = 0;
        foreach (var s in source)
        {
            if (existing.Any(e => (s.UserId != null && e.UserId == s.UserId) || (s.UserId == null && e.Name == s.Name))) continue;
            var start = s.StartLocal + offset;
            var end = s.EndLocal + offset;
            if (OnDutyRules.CheckTimes(req.ShiftDate, req.Shift, start, end) is not null) (start, end) = HandoverRules.Window(req.ShiftDate, req.Shift);
            c.Db.DutyAssignments.Add(new DutyAssignment
            {
                HotelId = req.HotelId, ShiftDate = req.ShiftDate, Shift = req.Shift, UserId = s.UserId, Name = s.Name,
                DepartmentId = s.DepartmentId, Radio = s.Radio, Post = s.Post, StartLocal = start, EndLocal = end,
                CreatedById = me.Id, CreatedUtc = now, UpdatedUtc = now,
            });
            added++;
        }
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync("CopyRoster", Modules.OnDuty, "Roster", $"{req.HotelId}/{req.ShiftDate:yyyy-MM-dd}/{req.Shift}",
            new { from = $"{req.FromDate:yyyy-MM-dd}/{req.FromShift}", added }, ct);
        return Results.Ok(new { added });
    }

    static async Task<IResult> SearchCallOuts(int? hotelId, DateOnly? from, DateOnly? to, int? userId, string? q,
        [AsParameters] Ctx c, CancellationToken ct)
    {
        var access = await c.Me.AccessToAsync(Modules.OnDuty, ct);
        var query = c.Db.CallOuts.Where(x => x.RemovedUtc == null && (access.HotelId == null || x.HotelId == access.HotelId));
        if (hotelId is int h) query = query.Where(x => x.HotelId == h);
        var f = from ?? DateOnly.FromDateTime(PacificNow()).AddDays(-30);
        query = query.Where(x => x.ShiftDate >= f);
        if (to is { } t) query = query.Where(x => x.ShiftDate <= t);
        if (userId is int u) query = query.Where(x => x.UserId == u);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var text = q.Trim();
            query = query.Where(x => x.Name!.Contains(text) || c.Db.Users.Any(us => us.Id == x.UserId && us.DisplayName.Contains(text)));
        }
        var list = await Project(c.Db, query).Take(500).ToListAsync(ct);
        await c.Log.LogAsync("Search", Modules.OnDuty, "CallOut", null, new { hotelId, from = f, to, userId, q, count = list.Count }, ct);
        return Results.Ok(list.OrderByDescending(x => x.CalledLocal));
    }

    static async Task<IResult> SaveCallOut(int? id, CallOutRequest req, Ctx c, CancellationToken ct)
    {
        if (await c.Me.LevelAtAsync(Modules.OnDuty, req.HotelId, ct) < AccessLevel.Edit) return Results.StatusCode(StatusCodes.Status403Forbidden);
        if (await CheckPerson(req.HotelId, req.UserId, req.Name, req.DepartmentId, c.Db, ct) is { } bad) return Bad(bad);
        if (!Enum.IsDefined(req.Shift)) return Bad("Choose the shift.");
        if (req.Reason is null || !OnDutyRules.Reasons.Contains(req.Reason)) return Bad("Choose the reason.");
        var called = req.CalledLocal ?? PacificNow();
        if (called > PacificNow().AddHours(1)) return Bad("The call time is in the future.");
        var me = await c.Me.GetUserAsync(ct);
        if (me is null) return Bad("Your account is not set up yet.");

        var now = DateTime.UtcNow;
        var x = id is int i ? await c.Db.CallOuts.FirstOrDefaultAsync(o => o.Id == i && o.RemovedUtc == null, ct)
            : c.Db.CallOuts.Add(new CallOut { Reason = req.Reason, CreatedById = me.Id, CreatedUtc = now }).Entity;
        if (x is null || (id is not null && await c.Me.LevelAtAsync(Modules.OnDuty, x.HotelId, ct) < AccessLevel.Edit)) return Results.NotFound();
        x.HotelId = req.HotelId; x.ShiftDate = req.ShiftDate; x.Shift = req.Shift; x.UserId = req.UserId;
        x.Name = req.UserId is null ? Clean(req.Name, 200) : null; x.DepartmentId = req.DepartmentId;
        x.CalledLocal = Minute(called); x.Reason = req.Reason; x.Notes = Clean(req.Notes, 1000); x.CoveredBy = Clean(req.CoveredBy, 200);
        x.UpdatedUtc = now;
        await c.Db.SaveChangesAsync(ct);
        // The reason and notes are not copied into the log.
        await c.Log.LogAsync(id is null ? "AddCallOut" : "EditCallOut", Modules.OnDuty, "CallOut", x.Id.ToString(),
            new { x.HotelId, x.ShiftDate, x.Shift, x.UserId, x.Name }, ct);
        return Results.Ok(new { x.Id });
    }

    static async Task<IResult> RemoveCallOut(int id, [AsParameters] Ctx c, CancellationToken ct)
    {
        var x = await c.Db.CallOuts.FirstOrDefaultAsync(o => o.Id == id && o.RemovedUtc == null, ct);
        if (x is null || await c.Me.LevelAtAsync(Modules.OnDuty, x.HotelId, ct) < AccessLevel.Edit) return Results.NotFound();
        x.RemovedUtc = x.UpdatedUtc = DateTime.UtcNow;
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync("RemoveCallOut", Modules.OnDuty, "CallOut", x.Id.ToString(), null, ct);
        return Results.Ok();
    }

    static async Task<string?> CheckPerson(int hotelId, int? userId, string? name, int? departmentId, DeskDbContext db, CancellationToken ct)
    {
        if (!await db.Hotels.AnyAsync(h => h.Id == hotelId, ct)) return "Choose the hotel.";
        if (userId is null && string.IsNullOrWhiteSpace(name)) return "Choose the person or type their name.";
        if (userId is int u && !await db.Users.AnyAsync(x => x.Id == u, ct)) return "That person is not in Shore Desk.";
        if (departmentId is int d && !await db.Departments.AnyAsync(x => x.Id == d, ct)) return "Unknown department.";
        return null;
    }

    static DateTime Minute(DateTime t) => new(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0);
}
