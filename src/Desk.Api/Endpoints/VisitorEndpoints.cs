using System.Text.Json;
using Desk.Api.Auth;
using Desk.Api.Services;
using Desk.Core.Access;
using Desk.Core.Data;
using Desk.Core.Visitors;
using Microsoft.EntityFrameworkCore;
using static Desk.Api.Endpoints.Shared;

namespace Desk.Api.Endpoints;

// Visitors: check in / out, who is on property, visits booked ahead, badge photo.
// Module "visitors": View sees, Edit checks people in and out and books visits,
// Manage voids. The person's hotel limit applies to every visit.
public static class VisitorEndpoints
{
    public record VisitRequest(int HotelId, string? VisitorName, string? Company, string? VisitorType, string? Phone, string? Email,
        int? AreaId, string? AreaDetail, string? HostName, int? HostUserId, string? Purpose, string? BadgeColor, string? BadgeNumber,
        DateTime? InLocal, DateTime? ExpectedOutLocal, string? Notes, List<Companion>? Companions, int? ScheduledVisitId);
    public record CheckOutRequest(DateTime? OutLocal);
    public record VoidRequest(string? Reason);
    public record ScheduleRequest(int HotelId, string? VisitorName, string? Company, string? VisitorType, DateTime? ExpectedLocal,
        string? HostName, int? HostUserId, string? Purpose, string? Notes);

    public record VisitDto(int Id, int HotelId, string VisitorName, string? Company, string VisitorType, string? Phone, string? Email,
        int? AreaId, string? AreaName, string? AreaDetail, string? HostName, int? HostUserId, string? Purpose, string? BadgeColor,
        string? BadgeNumber, DateTime InLocal, DateTime? ExpectedOutLocal, DateTime? OutLocal, string? Notes, List<Companion> Companions,
        bool HasPhoto, string CheckedInBy, string? CheckedOutBy, bool Overdue, double HoursIn, DateTime? VoidedUtc, string? VoidReason);
    public record ScheduledDto(int Id, int HotelId, string VisitorName, string? Company, string VisitorType, DateTime ExpectedLocal,
        string? HostName, string? Purpose, string? Notes, string BookedBy, int? VisitId);

    public static void MapVisitorEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/visitors").RequireModule(Modules.Visitors, AccessLevel.View);
        g.MapGet("", List);
        g.MapGet("/{id:int}", Get);
        g.MapPost("", (VisitRequest r, [AsParameters] Ctx c, CancellationToken ct) => Save(null, r, c, ct)).RequireModule(Modules.Visitors, AccessLevel.Edit);
        g.MapPut("/{id:int}", (int id, VisitRequest r, [AsParameters] Ctx c, CancellationToken ct) => Save(id, r, c, ct)).RequireModule(Modules.Visitors, AccessLevel.Edit);
        g.MapPost("/{id:int}/checkout", CheckOut).RequireModule(Modules.Visitors, AccessLevel.Edit);
        g.MapPost("/{id:int}/void", Void).RequireModule(Modules.Visitors, AccessLevel.Manage);
        g.MapPost("/{id:int}/photo", UploadPhoto).RequireModule(Modules.Visitors, AccessLevel.Edit);
        g.MapGet("/{id:int}/photo", Photo);
        g.MapGet("/scheduled", Scheduled);
        g.MapPost("/scheduled", (ScheduleRequest r, [AsParameters] Ctx c, CancellationToken ct) => SaveScheduled(null, r, c, ct)).RequireModule(Modules.Visitors, AccessLevel.Edit);
        g.MapPut("/scheduled/{id:int}", (int id, ScheduleRequest r, [AsParameters] Ctx c, CancellationToken ct) => SaveScheduled(id, r, c, ct)).RequireModule(Modules.Visitors, AccessLevel.Edit);
        g.MapPost("/scheduled/{id:int}/cancel", CancelScheduled).RequireModule(Modules.Visitors, AccessLevel.Edit);
    }

    public record Ctx(DeskDbContext Db, CurrentUser Me, ActivityLogger Log);

    // view: "in" (on property now, default), "today", or a search (q / from / to).
    static async Task<IResult> List(int hotelId, string? view, string? q, DateOnly? from, DateOnly? to, [AsParameters] Ctx c, CancellationToken ct)
    {
        if (await c.Me.LevelAtAsync(Modules.Visitors, hotelId, ct) < AccessLevel.View) return Results.NotFound();
        var query = c.Db.Visits.Where(v => v.HotelId == hotelId && v.VoidedUtc == null);
        var today = DateOnly.FromDateTime(PacificNow());
        switch (view)
        {
            case "today":
                query = query.Where(v => v.InLocal >= today.ToDateTime(TimeOnly.MinValue) || v.OutLocal == null);
                break;
            case "search":
                var f = (from ?? today.AddDays(-30)).ToDateTime(TimeOnly.MinValue);
                var t = (to ?? today).AddDays(1).ToDateTime(TimeOnly.MinValue);
                query = query.Where(v => v.InLocal >= f && v.InLocal < t);
                if (!string.IsNullOrWhiteSpace(q))
                {
                    var text = q.Trim();
                    query = query.Where(v => v.VisitorName.Contains(text) || v.Company!.Contains(text) || v.HostName!.Contains(text)
                        || v.BadgeNumber == text || v.CompanionsJson!.Contains(text));
                }
                break;
            default:
                query = query.Where(v => v.OutLocal == null);
                break;
        }
        var list = await Load(c.Db, query.OrderByDescending(v => v.InLocal).Take(500), ct);
        await c.Log.LogAsync(view == "search" ? "Search" : "View", Modules.Visitors, "VisitList", hotelId.ToString(), new { view, q, from, to, count = list.Count }, ct);
        return Results.Ok(list);
    }

    // Also used by the handover page ("visitors still in").
    public static async Task<List<VisitDto>> StillIn(DeskDbContext db, int hotelId, CancellationToken ct) =>
        await Load(db, db.Visits.Where(v => v.HotelId == hotelId && v.VoidedUtc == null && v.OutLocal == null).OrderBy(v => v.InLocal), ct);

    static async Task<List<VisitDto>> Load(DeskDbContext db, IQueryable<Visit> query, CancellationToken ct)
    {
        var now = PacificNow();
        var rows = await query.Select(v => new
        {
            V = v,
            Area = v.Area != null ? v.Area.Name : null,
            In = db.Users.Where(u => u.Id == v.CreatedById).Select(u => u.DisplayName).First(),
            Out = db.Users.Where(u => u.Id == v.CheckedOutById).Select(u => u.DisplayName).FirstOrDefault(),
        }).ToListAsync(ct);
        return rows.Select(r => ToDto(r.V, r.Area, r.In, r.Out, now)).ToList();
    }

    static VisitDto ToDto(Visit v, string? area, string inBy, string? outBy, DateTime now) =>
        new(v.Id, v.HotelId, v.VisitorName, v.Company, v.VisitorType, v.Phone, v.Email, v.AreaId, area, v.AreaDetail, v.HostName,
            v.HostUserId, v.Purpose, v.BadgeColor, v.BadgeNumber, v.InLocal, v.ExpectedOutLocal, v.OutLocal, v.Notes, Companions(v.CompanionsJson),
            v.PhotoStoredName is not null, inBy, outBy, v.OutLocal is null && VisitorRules.Overdue(v.InLocal, v.ExpectedOutLocal, now),
            Math.Round(((v.OutLocal ?? now) - v.InLocal).TotalHours, 1), v.VoidedUtc, v.VoidReason);

    static List<Companion> Companions(string? json) =>
        string.IsNullOrEmpty(json) ? [] : JsonSerializer.Deserialize<List<Companion>>(json, JsonSerializerOptions.Web) ?? [];

    static async Task<IResult> Get(int id, [AsParameters] Ctx c, CancellationToken ct)
    {
        var v = await Find(id, AccessLevel.View, c, ct);
        if (v is null) return Results.NotFound();
        await c.Log.LogAsync("View", Modules.Visitors, "Visit", v.Id.ToString(), null, ct);
        return Results.Ok((await Load(c.Db, c.Db.Visits.Where(x => x.Id == v.Id), ct)).Single());
    }

    static async Task<IResult> Save(int? id, VisitRequest req, Ctx c, CancellationToken ct)
    {
        if (await c.Me.LevelAtAsync(Modules.Visitors, req.HotelId, ct) < AccessLevel.Edit) return Results.StatusCode(StatusCodes.Status403Forbidden);
        var name = Clean(req.VisitorName, 200);
        if (name is null) return Bad("The visitor's name is required.");
        if (req.VisitorType is null || !VisitorRules.Types.Contains(req.VisitorType)) return Bad("Choose the visitor type.");
        var companions = (req.Companions ?? []).Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .Select(x => new Companion(x.Name.Trim()[..Math.Min(x.Name.Trim().Length, 200)], Clean(x.BadgeNumber, 40))).ToList();
        if (companions.Count > VisitorRules.MaxCompanions) return Bad($"At most {VisitorRules.MaxCompanions} people in the same group; check them in separately.");
        if (req.AreaId is int a)
        {
            var area = await c.Db.LocationAreas.FirstOrDefaultAsync(x => x.Id == a, ct);
            if (area is null || (area.HotelId is int ah && ah != req.HotelId)) return Bad("Choose a location from this hotel.");
        }
        if (req.HostUserId is int h && !await c.Db.Users.AnyAsync(u => u.Id == h, ct)) return Bad("That host is not in Shore Desk.");
        var now = PacificNow();
        var inAt = req.InLocal ?? now;
        if (inAt > now.AddMinutes(10)) return Bad("The check-in time is in the future.");
        if (req.ExpectedOutLocal is { } exp && exp <= inAt) return Bad("The expected leaving time must be after check-in.");
        var me = await c.Me.GetUserAsync(ct);
        if (me is null) return Bad("Your account is not set up yet.");

        ScheduledVisit? booked = null;
        if (id is null && req.ScheduledVisitId is int sid)
        {
            booked = await c.Db.ScheduledVisits.FirstOrDefaultAsync(s => s.Id == sid && s.HotelId == req.HotelId && s.VisitId == null && s.CancelledUtc == null, ct);
            if (booked is null) return Bad("That booked visit is no longer open.");
        }

        var v = id is int i ? await Find(i, AccessLevel.Edit, c, ct)
            : c.Db.Visits.Add(new Visit { VisitorName = name, VisitorType = req.VisitorType, CreatedById = me.Id, CreatedUtc = DateTime.UtcNow }).Entity;
        if (v is null) return Results.NotFound();
        if (v.VoidedUtc is not null) return Bad("This visit was voided.");
        v.HotelId = req.HotelId; v.VisitorName = name; v.Company = Clean(req.Company, 200); v.VisitorType = req.VisitorType;
        v.Phone = Clean(req.Phone, 40); v.Email = Clean(req.Email, 200); v.AreaId = req.AreaId; v.AreaDetail = Clean(req.AreaDetail, 200);
        v.HostUserId = req.HostUserId;
        v.HostName = req.HostUserId is int hu ? await c.Db.Users.Where(u => u.Id == hu).Select(u => u.DisplayName).FirstAsync(ct) : Clean(req.HostName, 200);
        v.Purpose = Clean(req.Purpose, 400); v.BadgeColor = Clean(req.BadgeColor, 40); v.BadgeNumber = Clean(req.BadgeNumber, 40);
        v.InLocal = Minute(inAt); v.ExpectedOutLocal = req.ExpectedOutLocal; v.Notes = Clean(req.Notes, 2000);
        v.CompanionsJson = companions.Count == 0 ? null : JsonSerializer.Serialize(companions, JsonSerializerOptions.Web);
        v.UpdatedUtc = DateTime.UtcNow;
        await c.Db.SaveChangesAsync(ct);
        if (booked is not null)
        {
            v.ScheduledVisitId = booked.Id;
            booked.VisitId = v.Id;
            await c.Db.SaveChangesAsync(ct);
        }
        await c.Log.LogAsync(id is null ? "CheckIn" : "EditVisit", Modules.Visitors, "Visit", v.Id.ToString(), new { v.VisitorName, v.Company, people = 1 + companions.Count }, ct);
        return Results.Ok(new { v.Id });
    }

    static async Task<IResult> CheckOut(int id, CheckOutRequest req, [AsParameters] Ctx c, CancellationToken ct)
    {
        var v = await Find(id, AccessLevel.Edit, c, ct);
        if (v is null) return Results.NotFound();
        if (v.OutLocal is not null || v.VoidedUtc is not null) return Bad("This visitor has already left.");
        var outAt = req.OutLocal ?? PacificNow();
        if (outAt < v.InLocal) return Bad("They cannot leave before they arrived.");
        if (outAt > PacificNow().AddMinutes(10)) return Bad("The time is in the future.");
        v.OutLocal = Minute(outAt);
        v.CheckedOutById = (await c.Me.GetUserAsync(ct))?.Id;
        v.UpdatedUtc = DateTime.UtcNow;
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync("CheckOut", Modules.Visitors, "Visit", v.Id.ToString(), null, ct);
        return Results.Ok();
    }

    static async Task<IResult> Void(int id, VoidRequest req, [AsParameters] Ctx c, CancellationToken ct)
    {
        var v = await Find(id, AccessLevel.Manage, c, ct);
        if (v is null) return Results.NotFound();
        if (v.VoidedUtc is not null) return Bad("Already voided.");
        var reason = Clean(req.Reason, 1000);
        if (reason is null) return Bad("Give the reason for voiding.");
        v.VoidedUtc = v.UpdatedUtc = DateTime.UtcNow;
        v.VoidReason = reason;
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync("Void", Modules.Visitors, "Visit", v.Id.ToString(), new { reason }, ct);
        return Results.Ok();
    }

    // Badge photo from the phone or laptop camera. A new photo replaces the old one
    // on the visit; the old file stays on disk (and in the backup).
    static async Task<IResult> UploadPhoto(int id, HttpRequest http, FileStore files, [AsParameters] Ctx c, CancellationToken ct)
    {
        var v = await Find(id, AccessLevel.Edit, c, ct);
        if (v is null) return Results.NotFound();
        if (!http.HasFormContentType) return Bad("No photo was sent.");
        var file = (await http.ReadFormAsync(ct)).Files.FirstOrDefault();
        if (file is null || file.Length == 0) return Bad("No photo was sent.");
        if (FileStore.ContentTypeFor(file.FileName) is not { } type || !type.StartsWith("image/")) return Bad("The photo must be a picture (JPG, PNG, HEIC).");
        if (file.Length > 15 * 1024 * 1024) return Bad("The photo is too large (15 MB at most).");
        v.PhotoStoredName = await files.SaveAsync(Path.Combine("visitors", v.Id.ToString()), file, ct);
        v.UpdatedUtc = DateTime.UtcNow;
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync("AddPhoto", Modules.Visitors, "Visit", v.Id.ToString(), null, ct);
        return Results.Ok();
    }

    static async Task<IResult> Photo(int id, FileStore files, [AsParameters] Ctx c, HttpResponse response, CancellationToken ct)
    {
        var v = await Find(id, AccessLevel.View, c, ct);
        if (v?.PhotoStoredName is null) return Results.NotFound();
        var path = files.PathFor(Path.Combine("visitors", v.Id.ToString()), v.PhotoStoredName);
        if (!File.Exists(path)) return Results.NotFound();
        await c.Log.LogAsync("ViewPhoto", Modules.Visitors, "Visit", v.Id.ToString(), null, ct);
        response.Headers.CacheControl = "private, no-store";
        return Results.File(path, FileStore.ContentTypeFor(v.PhotoStoredName) ?? "image/jpeg");
    }

    // --- Booked visits -------------------------------------------------------------

    static async Task<IResult> Scheduled(int hotelId, DateOnly? from, DateOnly? to, [AsParameters] Ctx c, CancellationToken ct)
    {
        if (await c.Me.LevelAtAsync(Modules.Visitors, hotelId, ct) < AccessLevel.View) return Results.NotFound();
        var f = (from ?? DateOnly.FromDateTime(PacificNow())).ToDateTime(TimeOnly.MinValue);
        var t = (to ?? DateOnly.FromDateTime(PacificNow()).AddDays(7)).AddDays(1).ToDateTime(TimeOnly.MinValue);
        var list = await c.Db.ScheduledVisits.Where(s => s.HotelId == hotelId && s.CancelledUtc == null && s.ExpectedLocal >= f && s.ExpectedLocal < t)
            .OrderBy(s => s.ExpectedLocal)
            .Select(s => new ScheduledDto(s.Id, s.HotelId, s.VisitorName, s.Company, s.VisitorType, s.ExpectedLocal, s.HostName, s.Purpose, s.Notes,
                c.Db.Users.Where(u => u.Id == s.CreatedById).Select(u => u.DisplayName).First(), s.VisitId))
            .ToListAsync(ct);
        return Results.Ok(list);
    }

    static async Task<IResult> SaveScheduled(int? id, ScheduleRequest req, Ctx c, CancellationToken ct)
    {
        if (await c.Me.LevelAtAsync(Modules.Visitors, req.HotelId, ct) < AccessLevel.Edit) return Results.StatusCode(StatusCodes.Status403Forbidden);
        var name = Clean(req.VisitorName, 200);
        if (name is null || req.ExpectedLocal is null) return Bad("The visitor's name and the expected time are required.");
        if (req.VisitorType is null || !VisitorRules.Types.Contains(req.VisitorType)) return Bad("Choose the visitor type.");
        if (req.HostUserId is int h && !await c.Db.Users.AnyAsync(u => u.Id == h, ct)) return Bad("That host is not in Shore Desk.");
        var me = await c.Me.GetUserAsync(ct);
        if (me is null) return Bad("Your account is not set up yet.");
        var s = id is int i ? await c.Db.ScheduledVisits.FirstOrDefaultAsync(x => x.Id == i && x.CancelledUtc == null && x.VisitId == null, ct)
            : c.Db.ScheduledVisits.Add(new ScheduledVisit { VisitorName = name, VisitorType = req.VisitorType, CreatedById = me.Id, CreatedUtc = DateTime.UtcNow }).Entity;
        if (s is null || await c.Me.LevelAtAsync(Modules.Visitors, s.HotelId == 0 ? req.HotelId : s.HotelId, ct) < AccessLevel.Edit) return Results.NotFound();
        s.HotelId = req.HotelId; s.VisitorName = name; s.Company = Clean(req.Company, 200); s.VisitorType = req.VisitorType;
        s.ExpectedLocal = req.ExpectedLocal.Value; s.HostUserId = req.HostUserId;
        s.HostName = req.HostUserId is int hu ? await c.Db.Users.Where(u => u.Id == hu).Select(u => u.DisplayName).FirstAsync(ct) : Clean(req.HostName, 200);
        s.Purpose = Clean(req.Purpose, 400); s.Notes = Clean(req.Notes, 1000);
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync(id is null ? "BookVisit" : "EditBooking", Modules.Visitors, "ScheduledVisit", s.Id.ToString(), new { s.VisitorName, s.ExpectedLocal }, ct);
        return Results.Ok(new { s.Id });
    }

    static async Task<IResult> CancelScheduled(int id, [AsParameters] Ctx c, CancellationToken ct)
    {
        var s = await c.Db.ScheduledVisits.FirstOrDefaultAsync(x => x.Id == id && x.CancelledUtc == null, ct);
        if (s is null || await c.Me.LevelAtAsync(Modules.Visitors, s.HotelId, ct) < AccessLevel.Edit) return Results.NotFound();
        if (s.VisitId is not null) return Bad("The visitor has already arrived.");
        s.CancelledUtc = DateTime.UtcNow;
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync("CancelBooking", Modules.Visitors, "ScheduledVisit", s.Id.ToString(), null, ct);
        return Results.Ok();
    }

    // --- Helpers -------------------------------------------------------------------

    static async Task<Visit?> Find(int id, AccessLevel needed, Ctx c, CancellationToken ct)
    {
        var v = await c.Db.Visits.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (v is null) return null;
        var level = await c.Me.LevelAtAsync(Modules.Visitors, v.HotelId, ct);
        return level >= needed ? v : null;
    }

    static DateTime Minute(DateTime t) => new(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0);
}
