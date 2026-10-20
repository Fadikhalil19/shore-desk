using System.Text;
using Desk.Api.Auth;
using Desk.Api.Services;
using Desk.Core.Access;
using Desk.Core.Data;
using Desk.Core.Handover;
using Desk.Core.Incidents;
using Microsoft.EntityFrameworkCore;
using static Desk.Api.Endpoints.Shared;

namespace Desk.Api.Endpoints;

// Safety reports: shift summary, incident and event statistics, CSV export (opens
// in Excel). Needs the safety-reports module, AND every figure only counts what the
// person may see in the underlying module (incidents per form, event log, ...),
// so a report never shows more than the person could find by searching.
public static class ReportEndpoints
{
    public record Count(string Key, int Value);
    public record IncidentStats(DateOnly From, DateOnly To, int Total, List<Count> ByType, List<Count> ByForm, List<Count> ByShift,
        List<Count> ByOfficer, List<Count> ByMonth, List<Count> ByLocation, List<Count> ByStatus, List<Count> ByHotel);
    public record EventStats(DateOnly From, DateOnly To, int Total, int Open, double? AverageMinutesToComplete, List<Count> ByCategory,
        List<Count> ByShift, List<Count> ByOfficer, List<Count> ByPriority, List<Count> ByMonth);
    public record ShiftIncident(int Id, string Number, string Form, string Heading, string? Type, DateTime? OccurredLocal, string Status, string WrittenBy);
    public record ShiftSummary(int HotelId, DateOnly ShiftDate, Shift Shift, DateTime StartLocal, DateTime EndLocal,
        DateOnly PreviousDate, Shift PreviousShift, List<OnDutyEndpoints.AssignmentDto>? OnDuty, List<OnDutyEndpoints.CallOutDto>? CallOuts,
        List<ShiftIncident>? Incidents, int? EventsTotal, int? EventsOpen, List<Count>? EventsByCategory, List<string>? HandoverNotes);

    const int MaxDays = 3 * 366;

    public static void MapReportEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/reports").RequireModule(Modules.SafetyReports, AccessLevel.View);
        g.MapGet("/shift", Shift_);
        g.MapGet("/incidents", Incidents);
        g.MapGet("/incidents.csv", IncidentsCsv);
        g.MapGet("/events", Events);
        g.MapGet("/events.csv", EventsCsv);
    }

    public record Ctx(DeskDbContext Db, CurrentUser Me, ActivityLogger Log);

    // --- Shift summary -----------------------------------------------------------

    static async Task<IResult> Shift_(int hotelId, DateOnly? date, Shift? shift, [AsParameters] Ctx c, CancellationToken ct)
    {
        if (await c.Me.LevelAtAsync(Modules.SafetyReports, hotelId, ct) < AccessLevel.View) return Results.NotFound();
        var (curDate, curShift) = HandoverRules.ShiftOf(PacificNow());
        // Default: the shift that just ended (a summary of the current one is not complete yet).
        var (d, s) = date is { } dd && shift is { } ss ? (dd, ss) : HandoverRules.Previous(curDate, curShift);
        var (start, end) = HandoverRules.Window(d, s);
        var (prevDate, prevShift) = HandoverRules.Previous(d, s);
        var can = async (string module) => await c.Me.LevelAtAsync(module, hotelId, ct) >= AccessLevel.View;

        List<OnDutyEndpoints.AssignmentDto>? onDuty = null;
        List<OnDutyEndpoints.CallOutDto>? callOuts = null;
        if (await can(Modules.OnDuty))
        {
            onDuty = await OnDutyEndpoints.Roster(c.Db, hotelId, d, s, ct);
            callOuts = await OnDutyEndpoints.CallOutsFor(c.Db, hotelId, d, s, ct);
        }

        var access = await c.Me.GetAccessAsync(ct);
        var incidents = (await IncidentEndpoints.VisibleTo(c.Db.Incidents, access)
            .Where(r => r.HotelId == hotelId && r.Status != IncidentStatus.Voided && r.OccurredLocal >= start && r.OccurredLocal < end)
            .OrderBy(r => r.OccurredLocal)
            .Select(r => new { r.Id, r.Number, r.Form, r.Heading, Type = r.Type != null ? r.Type.Name : r.Kind, r.OccurredLocal, r.Status, r.WrittenBy.DisplayName })
            .ToListAsync(ct))
            .Select(r => new ShiftIncident(r.Id, r.Number, IncidentRules.FormName(r.Form), r.Heading, r.Type, r.OccurredLocal, r.Status.ToString(), r.DisplayName))
            .ToList();

        int? evTotal = null, evOpen = null;
        List<Count>? byCategory = null;
        if (await can(Modules.EventLog))
        {
            var ev = await c.Db.Events.Where(e => e.HotelId == hotelId && e.VoidedUtc == null && e.StartedLocal >= start && e.StartedLocal < end)
                .Select(e => new { e.Category.Name, Open = e.CompletedLocal == null }).ToListAsync(ct);
            evTotal = ev.Count;
            evOpen = ev.Count(e => e.Open);
            byCategory = Group(ev.Select(e => e.Name));
        }

        List<string>? notes = null;
        if (await can(Modules.Handover))
            notes = await c.Db.HandoverNotes.Where(n => n.HotelId == hotelId && n.ShiftDate == d && n.Shift == s && n.RemovedUtc == null)
                .OrderBy(n => n.Id).Select(n => n.Text).ToListAsync(ct);

        await c.Log.LogAsync("View", Modules.SafetyReports, "ShiftSummary", $"{hotelId}/{d:yyyy-MM-dd}/{s}", null, ct);
        return Results.Ok(new ShiftSummary(hotelId, d, s, start, end, prevDate, prevShift, onDuty, callOuts,
            incidents, evTotal, evOpen, byCategory, notes));
    }

    // --- Incidents ---------------------------------------------------------------

    record IncidentLine(string Number, IncidentForm Form, IncidentStatus Status, string Hotel, string? Type, DateTime? OccurredLocal,
        Shift? Shift, string? Area, string? Spot, string Heading, string WrittenBy, string? Supervisor);

    static async Task<(List<IncidentLine>? Rows, DateOnly From, DateOnly To, IResult? Error)> IncidentRows(
        DateOnly? from, DateOnly? to, int? hotelId, Ctx c, CancellationToken ct)
    {
        var (f, t, error) = await Range(from, to, hotelId, c, ct);
        if (error is not null) return (null, f, t, error);
        var access = await c.Me.GetAccessAsync(ct);
        var reportHotel = access[Modules.SafetyReports].HotelId;
        var q = IncidentEndpoints.VisibleTo(c.Db.Incidents, access)
            .Where(r => r.Status != IncidentStatus.Voided && r.OccurredLocal >= f.ToDateTime(TimeOnly.MinValue)
                && r.OccurredLocal < t.AddDays(1).ToDateTime(TimeOnly.MinValue));
        if (hotelId is int h) q = q.Where(r => r.HotelId == h);
        if (reportHotel is int rh) q = q.Where(r => r.HotelId == rh);
        var rows = await q.OrderBy(r => r.OccurredLocal)
            .Select(r => new IncidentLine(r.Number, r.Form, r.Status, r.Hotel.Code, r.Type != null ? r.Type.Name : r.Kind, r.OccurredLocal,
                r.Shift, r.Area != null ? r.Area.Name : null, r.Location != null ? r.Location.Name : null, r.Heading,
                r.WrittenBy.DisplayName, r.Supervisor != null ? r.Supervisor.DisplayName : null))
            .ToListAsync(ct);
        return (rows, f, t, null);
    }

    static async Task<IResult> Incidents(DateOnly? from, DateOnly? to, int? hotelId, [AsParameters] Ctx c, CancellationToken ct)
    {
        var (rows, f, t, error) = await IncidentRows(from, to, hotelId, c, ct);
        if (error is not null) return error;
        await c.Log.LogAsync("View", Modules.SafetyReports, "IncidentStats", null, new { from = f, to = t, hotelId, count = rows!.Count }, ct);
        return Results.Ok(new IncidentStats(f, t, rows.Count,
            Group(rows.Select(r => r.Type ?? "(no type)")),
            Group(rows.Select(r => IncidentRules.FormName(r.Form))),
            Ordered(rows.Select(r => r.Shift?.ToString() ?? "(not set)"), ["Morning", "Afternoon", "Overnight", "(not set)"]),
            Group(rows.Select(r => r.WrittenBy)),
            Months(rows.Select(r => r.OccurredLocal), f, t),
            Group(rows.Select(r => r.Area ?? "(no location)")),
            Ordered(rows.Select(r => r.Status.ToString()), ["Draft", "AwaitingReview", "Approved"]),
            Group(rows.Select(r => r.Hotel))));
    }

    static async Task<IResult> IncidentsCsv(DateOnly? from, DateOnly? to, int? hotelId, [AsParameters] Ctx c, CancellationToken ct)
    {
        var (rows, f, t, error) = await IncidentRows(from, to, hotelId, c, ct);
        if (error is not null) return error;
        // Headings and names only: narratives and personal details stay in the app.
        var csv = Csv(["Number", "Form", "Status", "Hotel", "Type", "Happened", "Shift", "Location", "Spot", "Heading", "Written by", "Supervisor"],
            rows!.Select(r => new[] { r.Number, IncidentRules.FormName(r.Form), r.Status.ToString(), r.Hotel, r.Type,
                r.OccurredLocal?.ToString("yyyy-MM-dd HH:mm"), r.Shift?.ToString(), r.Area, r.Spot, r.Heading, r.WrittenBy, r.Supervisor }));
        await c.Log.LogAsync("Download", Modules.SafetyReports, "IncidentExport", null, new { from = f, to = t, hotelId, count = rows!.Count }, ct);
        return Results.File(csv, "text/csv", $"incidents {f:yyyy-MM-dd} to {t:yyyy-MM-dd}.csv");
    }

    // --- Events ------------------------------------------------------------------

    record EventLine(int Id, string Hotel, string Category, string? Detail, string Priority, DateTime Started, DateTime? Completed,
        Shift Shift, string CreatedBy, string? AssignedTo, string? Area);

    static async Task<(List<EventLine>? Rows, DateOnly From, DateOnly To, IResult? Error)> EventRows(
        DateOnly? from, DateOnly? to, int? hotelId, Ctx c, CancellationToken ct)
    {
        var (f, t, error) = await Range(from, to, hotelId, c, ct);
        if (error is not null) return (null, f, t, error);
        var events = await c.Me.AccessToAsync(Modules.EventLog, ct);
        if (events.Level < AccessLevel.View) return ([], f, t, null);
        var reportHotel = (await c.Me.AccessToAsync(Modules.SafetyReports, ct)).HotelId;
        var q = c.Db.Events.Where(e => e.VoidedUtc == null && (events.HotelId == null || e.HotelId == events.HotelId)
            && e.StartedLocal >= f.ToDateTime(TimeOnly.MinValue) && e.StartedLocal < t.AddDays(1).ToDateTime(TimeOnly.MinValue));
        if (hotelId is int h) q = q.Where(e => e.HotelId == h);
        if (reportHotel is int rh) q = q.Where(e => e.HotelId == rh);
        var rows = await q.OrderBy(e => e.StartedLocal)
            .Select(e => new EventLine(e.Id, e.Hotel.Code, e.Category.Name, e.Detail, e.Priority.ToString(), e.StartedLocal, e.CompletedLocal,
                e.Shift, e.CreatedBy.DisplayName, e.AssignedTo != null ? e.AssignedTo.DisplayName : null, e.Area != null ? e.Area.Name : null))
            .ToListAsync(ct);
        return (rows, f, t, null);
    }

    static async Task<IResult> Events(DateOnly? from, DateOnly? to, int? hotelId, [AsParameters] Ctx c, CancellationToken ct)
    {
        var (rows, f, t, error) = await EventRows(from, to, hotelId, c, ct);
        if (error is not null) return error;
        var done = rows!.Where(r => r.Completed is not null).Select(r => (r.Completed!.Value - r.Started).TotalMinutes).Where(m => m >= 0).ToList();
        await c.Log.LogAsync("View", Modules.SafetyReports, "EventStats", null, new { from = f, to = t, hotelId, count = rows!.Count }, ct);
        return Results.Ok(new EventStats(f, t, rows.Count, rows.Count(r => r.Completed is null),
            done.Count > 0 ? Math.Round(done.Average()) : null,
            Group(rows.Select(r => r.Category)),
            Ordered(rows.Select(r => r.Shift.ToString()), ["Morning", "Afternoon", "Overnight"]),
            Group(rows.Select(r => r.CreatedBy)),
            Ordered(rows.Select(r => r.Priority), ["Highest", "High", "Medium", "Low"]),
            Months(rows.Select(r => (DateTime?)r.Started), f, t)));
    }

    static async Task<IResult> EventsCsv(DateOnly? from, DateOnly? to, int? hotelId, [AsParameters] Ctx c, CancellationToken ct)
    {
        var (rows, f, t, error) = await EventRows(from, to, hotelId, c, ct);
        if (error is not null) return error;
        var csv = Csv(["Id", "Hotel", "Category", "Detail", "Priority", "Started", "Completed", "Shift", "Location", "Logged by", "Assigned to"],
            rows!.Select(r => new[] { r.Id.ToString(), r.Hotel, r.Category, r.Detail, r.Priority, r.Started.ToString("yyyy-MM-dd HH:mm"),
                r.Completed?.ToString("yyyy-MM-dd HH:mm"), r.Shift.ToString(), r.Area, r.CreatedBy, r.AssignedTo }));
        await c.Log.LogAsync("Download", Modules.SafetyReports, "EventExport", null, new { from = f, to = t, hotelId, count = rows!.Count }, ct);
        return Results.File(csv, "text/csv", $"events {f:yyyy-MM-dd} to {t:yyyy-MM-dd}.csv");
    }

    // --- Helpers -----------------------------------------------------------------

    // Default: the last 30 days. At most about three years at once.
    static async Task<(DateOnly From, DateOnly To, IResult? Error)> Range(DateOnly? from, DateOnly? to, int? hotelId, Ctx c, CancellationToken ct)
    {
        var t = to ?? DateOnly.FromDateTime(PacificNow());
        var f = from ?? t.AddDays(-29);
        if (f > t) return (f, t, Bad("The start date is after the end date."));
        if (t.DayNumber - f.DayNumber > MaxDays) return (f, t, Bad("Choose at most three years."));
        if (hotelId is int h && await c.Me.LevelAtAsync(Modules.SafetyReports, h, ct) < AccessLevel.View) return (f, t, Results.NotFound());
        return (f, t, null);
    }

    static List<Count> Group(IEnumerable<string> keys) =>
        keys.GroupBy(k => k).Select(g => new Count(g.Key, g.Count())).OrderByDescending(x => x.Value).ThenBy(x => x.Key).ToList();

    // Fixed order (e.g. shifts), leaving out keys with nothing.
    static List<Count> Ordered(IEnumerable<string> keys, string[] order)
    {
        var counts = keys.GroupBy(k => k).ToDictionary(g => g.Key, g => g.Count());
        return order.Where(counts.ContainsKey).Select(k => new Count(k, counts[k]))
            .Concat(counts.Keys.Except(order).Order().Select(k => new Count(k, counts[k]))).ToList();
    }

    // Every month in the range, including empty ones, as "2026-10".
    static List<Count> Months(IEnumerable<DateTime?> times, DateOnly from, DateOnly to)
    {
        var counts = times.Where(x => x is not null).GroupBy(x => x!.Value.ToString("yyyy-MM")).ToDictionary(g => g.Key, g => g.Count());
        var list = new List<Count>();
        for (var m = new DateOnly(from.Year, from.Month, 1); m <= to; m = m.AddMonths(1))
            list.Add(new Count(m.ToString("yyyy-MM"), counts.GetValueOrDefault(m.ToString("yyyy-MM"))));
        return list;
    }

    // CSV for Excel: UTF-8 with BOM, quoted. A cell starting with = + - @ is prefixed
    // with ' so Excel never runs it as a formula.
    public static byte[] Csv(string[] header, IEnumerable<string?[]> rows)
    {
        static string Cell(string? v)
        {
            v ??= "";
            if (v.Length > 0 && "=+-@\t\r".Contains(v[0])) v = "'" + v;
            return "\"" + v.Replace("\"", "\"\"") + "\"";
        }
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", header.Select(Cell)));
        foreach (var r in rows) sb.AppendLine(string.Join(",", r.Select(Cell)));
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(sb.ToString())];
    }
}
