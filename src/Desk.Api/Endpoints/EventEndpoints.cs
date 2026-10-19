using Desk.Api.Auth;
using Desk.Api.Services;
using Desk.Core.Access;
using Desk.Core.Data;
using Desk.Core.Events;
using Desk.Core.Incidents;
using Microsoft.EntityFrameworkCore;
using static Desk.Api.Endpoints.Shared;

namespace Desk.Api.Endpoints;

// Event log (daily security log) and task reminders. Module "eventlog"; the
// person's hotel limit applies per entry, and entries outside it answer 404.
// Rules for editing / completing / voiding are in Desk.Core.Events.EventRules.
public static class EventEndpoints
{
    public record SaveRequest(int HotelId, int CategoryId, string? Detail, int? AreaId, int? LocationId, string? LocationDetail,
        string? Room, string? RequestedByType, string? RequestedByName, int? RequestedByUserId, int? RequestedByDepartmentId,
        DateTime? RequestedLocal, string? RelatesToType, string? RelatesToName, string? RelatesToRoom, Priority Priority,
        int? AssignedDepartmentId, int? AssignedToId, DateTime? StartedLocal, DateTime? CompletedLocal, string? Narrative);
    public record CompleteRequest(DateTime? CompletedLocal);
    public record VoidRequest(string? Reason);
    public record FlagRequest(bool Flag);

    public record EventPermissions(bool Edit, bool Complete, bool Void, bool Flag);
    public record EventDto(int Id, int HotelId, int CategoryId, string CategoryName, string? Detail, int? AreaId, string? AreaName,
        int? LocationId, string? LocationName, string? LocationDetail, string? Room, string? RequestedByType, string? RequestedByName,
        int? RequestedByUserId, int? RequestedByDepartmentId, DateTime RequestedLocal, string? RelatesToType, string? RelatesToName,
        string? RelatesToRoom, Priority Priority, int? AssignedDepartmentId, int? AssignedToId, string? AssignedToName,
        DateTime StartedLocal, DateTime? CompletedLocal, Shift Shift, string? Narrative, string CreatedByName, DateTime CreatedUtc,
        DateTime UpdatedUtc, DateTime? VoidedUtc, string? VoidReason, bool ForHandover, EventPermissions Can);
    public record EventRow(int Id, string HotelCode, string CategoryName, string? Detail, string? AreaName, string? LocationName,
        string? Room, Priority Priority, DateTime StartedLocal, DateTime? CompletedLocal, Shift Shift, string? AssignedToName,
        string CreatedByName, string? Narrative, bool Voided);
    public record EventPage(int Total, List<EventRow> Items);

    public record ReminderRequest(int HotelId, string? Title, string? Notes, DateTime? DueLocal, bool RepeatsDaily, int? AssignedDepartmentId);
    public record ReminderDto(int Id, int HotelId, string Title, string? Notes, DateTime DueLocal, bool RepeatsDaily,
        int? AssignedDepartmentId, string? LastDoneBy, DateTime? LastDoneUtc, bool Overdue);

    const int PageSize = 100;

    public static void MapEventEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/events").RequireModule(Modules.EventLog, AccessLevel.View);

        g.MapGet("", Search);
        g.MapGet("/{id:int}", Get);
        g.MapPost("", Create).RequireModule(Modules.EventLog, AccessLevel.Edit);
        g.MapPut("/{id:int}", Update).RequireModule(Modules.EventLog, AccessLevel.Edit);
        g.MapPost("/{id:int}/complete", Complete).RequireModule(Modules.EventLog, AccessLevel.Edit);
        g.MapPost("/{id:int}/void", Void).RequireModule(Modules.EventLog, AccessLevel.Manage);
        g.MapPost("/{id:int}/handover", Flag).RequireModule(Modules.EventLog, AccessLevel.Edit);

        g.MapGet("/reminders", Reminders);
        g.MapPost("/reminders", (ReminderRequest r, [AsParameters] Ctx c, CancellationToken ct) => SaveReminder(null, r, c, ct))
            .RequireModule(Modules.EventLog, AccessLevel.Edit);
        g.MapPut("/reminders/{id:int}", (int id, ReminderRequest r, [AsParameters] Ctx c, CancellationToken ct) => SaveReminder(id, r, c, ct))
            .RequireModule(Modules.EventLog, AccessLevel.Edit);
        g.MapPost("/reminders/{id:int}/done", ReminderDone).RequireModule(Modules.EventLog, AccessLevel.Edit);
        g.MapPost("/reminders/{id:int}/cancel", CancelReminder).RequireModule(Modules.EventLog, AccessLevel.Edit);
    }

    public record Ctx(DeskDbContext Db, CurrentUser Me, ActivityLogger Log);

    // --- Events ------------------------------------------------------------------

    // A day (default today), or a date range; open = still running, whatever the day.
    static async Task<IResult> Search(DateOnly? day, DateOnly? from, DateOnly? to, bool? open, string? q, int? hotelId,
        int? categoryId, Priority? priority, bool? mine, bool? voided, int? page, [AsParameters] Ctx c, CancellationToken ct)
    {
        var access = await c.Me.AccessToAsync(Modules.EventLog, ct);
        var uid = (await c.Me.GetUserAsync(ct))?.Id ?? -1;
        var query = c.Db.Events.Where(e => access.HotelId == null || e.HotelId == access.HotelId);

        query = voided == true ? query.Where(e => e.VoidedUtc != null) : query.Where(e => e.VoidedUtc == null);
        if (open == true) query = query.Where(e => e.CompletedLocal == null);
        else if (from is null && to is null && string.IsNullOrWhiteSpace(q))
        {
            var d = day ?? DateOnly.FromDateTime(PacificNow());
            from = to = d;
        }
        if (from is { } f) query = query.Where(e => e.StartedLocal >= f.ToDateTime(TimeOnly.MinValue));
        if (to is { } t) query = query.Where(e => e.StartedLocal < t.AddDays(1).ToDateTime(TimeOnly.MinValue));
        if (hotelId is int h) query = query.Where(e => e.HotelId == h);
        if (categoryId is int cat) query = query.Where(e => e.CategoryId == cat);
        if (priority is { } p) query = query.Where(e => e.Priority == p);
        if (mine == true) query = query.Where(e => e.AssignedToId == uid || e.CreatedById == uid);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var text = q.Trim();
            query = query.Where(e => e.Narrative!.Contains(text) || e.Detail!.Contains(text) || e.RequestedByName!.Contains(text)
                || e.RelatesToName!.Contains(text) || e.Room == text || e.RelatesToRoom == text || e.LocationDetail!.Contains(text));
        }

        var total = await query.CountAsync(ct);
        var n = Math.Max(1, page ?? 1);
        var items = await query.OrderByDescending(e => e.StartedLocal).ThenByDescending(e => e.Id)
            .Skip((n - 1) * PageSize).Take(PageSize)
            .Select(e => new EventRow(e.Id, e.Hotel.Code, e.Category.Name, e.Detail, e.Area!.Name, e.Location!.Name, e.Room,
                e.Priority, e.StartedLocal, e.CompletedLocal, e.Shift, e.AssignedTo!.DisplayName, e.CreatedBy.DisplayName,
                e.Narrative!.Length > 200 ? e.Narrative.Substring(0, 200) + "..." : e.Narrative, e.VoidedUtc != null))
            .ToListAsync(ct);

        await c.Log.LogAsync("Search", Modules.EventLog, "Event", null,
            new { day, from, to, open, q, hotelId, categoryId, priority, mine, voided, page = n, total }, ct);
        return Results.Ok(new EventPage(total, items));
    }

    static async Task<IResult> Get(int id, [AsParameters] Ctx c, CancellationToken ct)
    {
        var (e, level) = await Load(id, c, ct);
        if (e is null) return Results.NotFound();
        await c.Log.LogAsync("View", Modules.EventLog, "Event", e.Id.ToString(), null, ct);
        return Results.Ok(await ToDto(e, level, c, ct));
    }

    static async Task<IResult> Create(SaveRequest req, [AsParameters] Ctx c, CancellationToken ct)
    {
        if (await c.Me.LevelAtAsync(Modules.EventLog, req.HotelId, ct) < AccessLevel.Edit)
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        var me = await c.Me.GetUserAsync(ct);
        if (me is null) return Bad("Your account is not set up yet. Sign out and in again.");

        var now = DateTime.UtcNow;
        var e = new SecurityEvent { CreatedById = me.Id, CreatedUtc = now, UpdatedUtc = now };
        if (await Apply(e, req, null, c.Db, ct) is { } error) return Bad(error);
        c.Db.Events.Add(e);
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync("Create", Modules.EventLog, "Event", e.Id.ToString(), new { hotel = e.HotelId, category = e.CategoryId }, ct);
        return Results.Ok(new { e.Id });
    }

    static async Task<IResult> Update(int id, SaveRequest req, [AsParameters] Ctx c, CancellationToken ct)
    {
        var (e, level) = await Load(id, c, ct);
        if (e is null) return Results.NotFound();
        var uid = (await c.Me.GetUserAsync(ct))?.Id;
        if (!EventRules.CanEdit(e.VoidedUtc is not null, level, e.CreatedById == uid || e.AssignedToId == uid))
            return Bad("Only the person who logged it, the person assigned, or a manager can change this entry.");
        if (req.HotelId != e.HotelId && await c.Me.LevelAtAsync(Modules.EventLog, req.HotelId, ct) < AccessLevel.Edit)
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        var before = Snapshot(e);
        if (await Apply(e, req, e, c.Db, ct) is { } error) return Bad(error);
        var changed = Snapshot(e).Where(kv => before[kv.Key] != kv.Value).Select(kv => kv.Key).ToList();
        if (changed.Count == 0) return Results.Ok(new { e.Id });
        e.UpdatedUtc = DateTime.UtcNow;
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync("Edit", Modules.EventLog, "Event", e.Id.ToString(), new { changed }, ct);
        return Results.Ok(new { e.Id });
    }

    static async Task<IResult> Complete(int id, CompleteRequest req, [AsParameters] Ctx c, CancellationToken ct)
    {
        var (e, level) = await Load(id, c, ct);
        if (e is null) return Results.NotFound();
        if (!EventRules.CanComplete(e.VoidedUtc is not null, e.CompletedLocal is null, level)) return Bad("This entry is already complete.");
        var done = req.CompletedLocal ?? PacificNow();
        if (done < e.StartedLocal.AddMinutes(-1)) return Bad("It cannot be completed before it started.");
        if (done > PacificNow().AddHours(1)) return Bad("The completed time is in the future.");
        e.CompletedLocal = new DateTime(done.Year, done.Month, done.Day, done.Hour, done.Minute, 0);
        e.UpdatedUtc = DateTime.UtcNow;
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync("Complete", Modules.EventLog, "Event", e.Id.ToString(), null, ct);
        return Results.Ok();
    }

    static async Task<IResult> Void(int id, VoidRequest req, [AsParameters] Ctx c, CancellationToken ct)
    {
        var (e, level) = await Load(id, c, ct);
        if (e is null) return Results.NotFound();
        if (!EventRules.CanVoid(e.VoidedUtc is not null, level)) return Bad("This entry cannot be voided.");
        var reason = Clean(req.Reason, 1000);
        if (reason is null) return Bad("Give the reason for voiding.");
        e.VoidedUtc = e.UpdatedUtc = DateTime.UtcNow;
        e.VoidReason = reason;
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync("Void", Modules.EventLog, "Event", e.Id.ToString(), new { reason }, ct);
        return Results.Ok();
    }

    // Show (or stop showing) the entry on the handover of the shift it started in.
    static async Task<IResult> Flag(int id, FlagRequest req, [AsParameters] Ctx c, CancellationToken ct)
    {
        var (e, level) = await Load(id, c, ct);
        if (e is null) return Results.NotFound();
        if (level < AccessLevel.Edit || e.VoidedUtc is not null) return Bad("You cannot flag this entry.");
        if (e.ForHandover == req.Flag) return Results.Ok();
        e.ForHandover = req.Flag;
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync(req.Flag ? "FlagHandover" : "UnflagHandover", Modules.EventLog, "Event", e.Id.ToString(), null, ct);
        return Results.Ok();
    }

    // --- Reminders ---------------------------------------------------------------

    // Open reminders due by the end of today (plus the next 24 hours for planning).
    static async Task<IResult> Reminders(int? hotelId, bool? all, [AsParameters] Ctx c, CancellationToken ct)
    {
        var access = await c.Me.AccessToAsync(Modules.EventLog, ct);
        var now = PacificNow();
        var horizon = now.Date.AddDays(2);
        var list = await c.Db.EventReminders
            .Where(r => r.ClosedUtc == null && (access.HotelId == null || r.HotelId == access.HotelId)
                && (hotelId == null || r.HotelId == hotelId) && (all == true || r.DueLocal < horizon))
            .OrderBy(r => r.DueLocal)
            .Select(r => new ReminderDto(r.Id, r.HotelId, r.Title, r.Notes, r.DueLocal, r.RepeatsDaily, r.AssignedDepartmentId,
                c.Db.Users.Where(u => u.Id == r.LastDoneById).Select(u => u.DisplayName).FirstOrDefault(), r.LastDoneUtc, r.DueLocal < now))
            .ToListAsync(ct);
        return Results.Ok(list);
    }

    static async Task<IResult> SaveReminder(int? id, ReminderRequest req, Ctx c, CancellationToken ct)
    {
        if (await c.Me.LevelAtAsync(Modules.EventLog, req.HotelId, ct) < AccessLevel.Edit)
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        var title = Clean(req.Title, 200);
        if (title is null || req.DueLocal is null) return Bad("A title and a due time are required.");
        if (req.AssignedDepartmentId is int d && !await c.Db.Departments.AnyAsync(x => x.Id == d, ct)) return Bad("Unknown department.");
        var me = await c.Me.GetUserAsync(ct);
        if (me is null) return Bad("Your account is not set up yet.");

        var r = id is int i ? await c.Db.EventReminders.FirstOrDefaultAsync(x => x.Id == i && x.ClosedUtc == null, ct)
            : c.Db.EventReminders.Add(new EventReminder { Title = title, CreatedById = me.Id, CreatedUtc = DateTime.UtcNow }).Entity;
        if (r is null || (id is not null && await c.Me.LevelAtAsync(Modules.EventLog, r.HotelId, ct) < AccessLevel.Edit))
            return Results.NotFound();
        r.HotelId = req.HotelId; r.Title = title; r.Notes = Clean(req.Notes, 1000); r.DueLocal = req.DueLocal.Value;
        r.RepeatsDaily = req.RepeatsDaily; r.AssignedDepartmentId = req.AssignedDepartmentId;
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync(id is null ? "AddReminder" : "EditReminder", Modules.EventLog, "EventReminder", r.Id.ToString(), new { title, r.DueLocal, r.RepeatsDaily }, ct);
        return Results.Ok(new { r.Id });
    }

    // Done: a one-off reminder closes; a daily one moves to its next time.
    static async Task<IResult> ReminderDone(int id, [AsParameters] Ctx c, CancellationToken ct)
    {
        var r = await c.Db.EventReminders.FirstOrDefaultAsync(x => x.Id == id && x.ClosedUtc == null, ct);
        if (r is null || await c.Me.LevelAtAsync(Modules.EventLog, r.HotelId, ct) < AccessLevel.Edit) return Results.NotFound();
        var me = await c.Me.GetUserAsync(ct);
        r.LastDoneUtc = DateTime.UtcNow;
        r.LastDoneById = me?.Id;
        if (r.RepeatsDaily) r.DueLocal = EventRules.NextDue(r.DueLocal, PacificNow());
        else r.ClosedUtc = DateTime.UtcNow;
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync("ReminderDone", Modules.EventLog, "EventReminder", r.Id.ToString(), new { r.Title }, ct);
        return Results.Ok();
    }

    static async Task<IResult> CancelReminder(int id, [AsParameters] Ctx c, CancellationToken ct)
    {
        var r = await c.Db.EventReminders.FirstOrDefaultAsync(x => x.Id == id && x.ClosedUtc == null, ct);
        if (r is null) return Results.NotFound();
        var level = await c.Me.LevelAtAsync(Modules.EventLog, r.HotelId, ct);
        if (level < AccessLevel.Edit) return Results.NotFound();
        var uid = (await c.Me.GetUserAsync(ct))?.Id;
        if (level < AccessLevel.Manage && r.CreatedById != uid) return Bad("Only the person who set it, or a manager, can cancel it.");
        r.ClosedUtc = DateTime.UtcNow;
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync("CancelReminder", Modules.EventLog, "EventReminder", r.Id.ToString(), new { r.Title }, ct);
        return Results.Ok();
    }

    // --- Helpers -----------------------------------------------------------------

    static async Task<(SecurityEvent? Event, AccessLevel Level)> Load(int id, Ctx c, CancellationToken ct)
    {
        var e = await c.Db.Events.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return (null, AccessLevel.None);
        var level = await c.Me.LevelAtAsync(Modules.EventLog, e.HotelId, ct);
        return level >= AccessLevel.View ? (e, level) : (null, AccessLevel.None);
    }

    static async Task<EventDto> ToDto(SecurityEvent e, AccessLevel level, Ctx c, CancellationToken ct)
    {
        var uid = (await c.Me.GetUserAsync(ct))?.Id;
        var names = await c.Db.Users.Where(u => u.Id == e.CreatedById || u.Id == e.AssignedToId)
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        var category = await c.Db.ListItems.Where(i => i.Id == e.CategoryId).Select(i => i.Name).FirstAsync(ct);
        var area = e.AreaId is int a ? await c.Db.LocationAreas.Where(x => x.Id == a).Select(x => x.Name).FirstOrDefaultAsync(ct) : null;
        var spot = e.LocationId is int l ? await c.Db.Locations.Where(x => x.Id == l).Select(x => x.Name).FirstOrDefaultAsync(ct) : null;
        var voided = e.VoidedUtc is not null;
        return new EventDto(e.Id, e.HotelId, e.CategoryId, category, e.Detail, e.AreaId, area, e.LocationId, spot, e.LocationDetail,
            e.Room, e.RequestedByType, e.RequestedByName, e.RequestedByUserId, e.RequestedByDepartmentId, e.RequestedLocal,
            e.RelatesToType, e.RelatesToName, e.RelatesToRoom, e.Priority, e.AssignedDepartmentId, e.AssignedToId,
            e.AssignedToId is int at ? names.GetValueOrDefault(at) : null, e.StartedLocal, e.CompletedLocal, e.Shift, e.Narrative,
            names.GetValueOrDefault(e.CreatedById, "?"), e.CreatedUtc, e.UpdatedUtc, e.VoidedUtc, e.VoidReason, e.ForHandover,
            new EventPermissions(
                EventRules.CanEdit(voided, level, e.CreatedById == uid || e.AssignedToId == uid),
                EventRules.CanComplete(voided, e.CompletedLocal is null, level),
                EventRules.CanVoid(voided, level),
                !voided && level >= AccessLevel.Edit));
    }

    // Copies the request onto the entry after checking every id it points at.
    static async Task<string?> Apply(SecurityEvent e, SaveRequest q, SecurityEvent? existing, DeskDbContext db, CancellationToken ct)
    {
        if (!await db.Hotels.AnyAsync(h => h.Id == q.HotelId, ct)) return "Choose the hotel.";
        if (!Enum.IsDefined(q.Priority)) return "Choose the priority.";
        var cat = await db.ListItems.FirstOrDefaultAsync(i => i.Id == q.CategoryId, ct);
        if (cat is null || cat.ListKey != ListKeys.EventCategory || (!cat.IsActive && existing?.CategoryId != q.CategoryId))
            return "Choose a category from the list.";
        if (q.AreaId is int areaId)
        {
            var area = await db.LocationAreas.FirstOrDefaultAsync(a => a.Id == areaId, ct);
            if (area is null || (!area.IsActive && existing?.AreaId != areaId)) return "Choose a location from the list.";
            if (area.HotelId is int ah && ah != q.HotelId) return "That location belongs to another hotel.";
            if (q.LocationId is int locId && !await db.Locations.AnyAsync(l => l.Id == locId && l.AreaId == areaId, ct))
                return "Choose a spot inside the chosen location.";
        }
        var userIds = new[] { q.RequestedByUserId, q.AssignedToId }.Where(x => x is not null).Select(x => x!.Value).Distinct().ToList();
        if (userIds.Count > 0 && await db.Users.CountAsync(u => userIds.Contains(u.Id), ct) != userIds.Count)
            return "A person chosen is not in Shore Desk.";
        var deptIds = new[] { q.RequestedByDepartmentId, q.AssignedDepartmentId }.Where(x => x is not null).Select(x => x!.Value).Distinct().ToList();
        if (deptIds.Count > 0 && await db.Departments.CountAsync(d => deptIds.Contains(d.Id), ct) != deptIds.Count)
            return "A department chosen is not in the list.";
        if (q.Narrative?.Length > 20_000) return "The narrative is too long (20,000 characters at most).";

        var now = PacificNow();
        var requested = q.RequestedLocal ?? q.StartedLocal ?? now;
        var started = q.StartedLocal ?? requested;
        if (started > now.AddHours(1) || requested > now.AddHours(1) || q.CompletedLocal > now.AddHours(1))
            return "A date and time is in the future.";

        e.HotelId = q.HotelId;
        e.CategoryId = q.CategoryId;
        e.Detail = Clean(q.Detail, 200);
        e.AreaId = q.AreaId;
        e.LocationId = q.AreaId is null ? null : q.LocationId;
        e.LocationDetail = Clean(q.LocationDetail, 200);
        e.Room = Clean(q.Room, 40);
        e.RequestedByType = Clean(q.RequestedByType, 40);
        e.RequestedByName = Clean(q.RequestedByName, 200);
        e.RequestedByUserId = q.RequestedByUserId;
        e.RequestedByDepartmentId = q.RequestedByDepartmentId;
        e.RequestedLocal = requested;
        e.RelatesToType = Clean(q.RelatesToType, 40);
        e.RelatesToName = Clean(q.RelatesToName, 200);
        e.RelatesToRoom = Clean(q.RelatesToRoom, 40);
        e.Priority = q.Priority;
        e.AssignedDepartmentId = q.AssignedDepartmentId;
        e.AssignedToId = q.AssignedToId;
        e.StartedLocal = started;
        e.CompletedLocal = q.CompletedLocal;
        e.Shift = IncidentRules.ShiftFor(started);
        e.Narrative = Clean(q.Narrative, 20_000);
        return EventRules.Check(e);
    }

    static Dictionary<string, string?> Snapshot(SecurityEvent e) => new()
    {
        ["hotel"] = e.HotelId.ToString(), ["category"] = $"{e.CategoryId}/{e.Detail}",
        ["location"] = $"{e.AreaId}/{e.LocationId}/{e.LocationDetail}/{e.Room}",
        ["requestedBy"] = $"{e.RequestedByType}/{e.RequestedByName}/{e.RequestedByUserId}/{e.RequestedByDepartmentId}/{e.RequestedLocal:O}",
        ["relatesTo"] = $"{e.RelatesToType}/{e.RelatesToName}/{e.RelatesToRoom}", ["priority"] = e.Priority.ToString(),
        ["assigned"] = $"{e.AssignedDepartmentId}/{e.AssignedToId}", ["started"] = e.StartedLocal.ToString("O"),
        ["completed"] = e.CompletedLocal?.ToString("O"), ["narrative"] = e.Narrative,
    };
}
