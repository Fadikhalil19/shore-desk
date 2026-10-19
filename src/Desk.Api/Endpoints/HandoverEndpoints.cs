using Desk.Api.Auth;
using Desk.Api.Services;
using Desk.Core.Access;
using Desk.Core.Data;
using Desk.Core.Handover;
using Desk.Core.Incidents;
using Microsoft.EntityFrameworkCore;
using static Desk.Api.Endpoints.Shared;

namespace Desk.Api.Endpoints;

// Handover for one hotel + shift: notes for the next shift, incidents and events
// flagged for handover in that shift, and events still open. Each item list only
// holds what the person may see in that item's own module.
public static class HandoverEndpoints
{
    public record NoteRequest(int HotelId, DateOnly ShiftDate, Shift Shift, string? Text);
    public record EditNoteRequest(string? Text);
    public record ShiftRequest(int HotelId, DateOnly ShiftDate, Shift Shift);

    public record NoteDto(int Id, string Text, string By, DateTime CreatedUtc, DateTime UpdatedUtc, bool CanEdit);
    public record IncidentItem(int Id, string Number, IncidentForm Form, string Heading, string? Type, DateTime? OccurredLocal,
        IncidentStatus Status);
    public record EventItem(int Id, string Category, string? Detail, DateTime StartedLocal, DateTime? CompletedLocal,
        string? AssignedTo, string? Narrative);
    public record ReadDto(string Name, DateTime ReadUtc);
    public record HandoverDto(int HotelId, DateOnly ShiftDate, Shift Shift, DateTime StartLocal, DateTime EndLocal,
        DateOnly PreviousDate, Shift PreviousShift, DateOnly CurrentDate, Shift CurrentShift,
        List<NoteDto> Notes, List<IncidentItem>? Incidents, List<EventItem>? Events, List<EventItem>? OpenEvents,
        List<ReadDto> Reads, bool ReadByMe, bool CanWrite);

    public static void MapHandoverEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/handover").RequireModule(Modules.Handover, AccessLevel.View);

        g.MapGet("", Get);
        g.MapPost("/notes", AddNote).RequireModule(Modules.Handover, AccessLevel.Edit);
        g.MapPut("/notes/{id:int}", EditNote).RequireModule(Modules.Handover, AccessLevel.Edit);
        g.MapPost("/notes/{id:int}/remove", RemoveNote).RequireModule(Modules.Handover, AccessLevel.Edit);
        g.MapPost("/read", MarkRead).RequireModule(Modules.Handover, AccessLevel.View);
    }

    public record Ctx(DeskDbContext Db, CurrentUser Me, ActivityLogger Log);

    // Without a date and shift: the current shift.
    static async Task<IResult> Get(int hotelId, DateOnly? date, Shift? shift, [AsParameters] Ctx c, CancellationToken ct)
    {
        var level = await c.Me.LevelAtAsync(Modules.Handover, hotelId, ct);
        if (level < AccessLevel.View) return Results.NotFound();
        var (curDate, curShift) = HandoverRules.ShiftOf(PacificNow());
        var d = date ?? curDate;
        var s = shift ?? curShift;
        var (start, end) = HandoverRules.Window(d, s);
        var (prevDate, prevShift) = HandoverRules.Previous(d, s);
        var uid = (await c.Me.GetUserAsync(ct))?.Id ?? -1;

        var notes = await c.Db.HandoverNotes
            .Where(n => n.HotelId == hotelId && n.ShiftDate == d && n.Shift == s && n.RemovedUtc == null)
            .OrderBy(n => n.Id)
            .Select(n => new NoteDto(n.Id, n.Text, c.Db.Users.Where(u => u.Id == n.CreatedById).Select(u => u.DisplayName).First(),
                n.CreatedUtc, n.UpdatedUtc, level >= AccessLevel.Manage || (level >= AccessLevel.Edit && n.CreatedById == uid)))
            .ToListAsync(ct);

        // Incidents: only forms whose module the person may see at this hotel.
        var forms = new List<IncidentForm>();
        foreach (var f in Enum.GetValues<IncidentForm>())
            if (await c.Me.LevelAtAsync(IncidentRules.ModuleFor(f), hotelId, ct) >= AccessLevel.View) forms.Add(f);
        List<IncidentItem>? incidents = null;
        if (forms.Count > 0)
            incidents = await c.Db.Incidents
                .Where(r => r.HotelId == hotelId && r.ForHandover && r.Status != IncidentStatus.Voided && forms.Contains(r.Form)
                    && r.OccurredLocal >= start && r.OccurredLocal < end)
                .OrderBy(r => r.OccurredLocal)
                .Select(r => new IncidentItem(r.Id, r.Number, r.Form, r.Heading, r.Type != null ? r.Type.Name : r.Kind, r.OccurredLocal, r.Status))
                .ToListAsync(ct);

        List<EventItem>? events = null, open = null;
        if (await c.Me.LevelAtAsync(Modules.EventLog, hotelId, ct) >= AccessLevel.View)
        {
            var q = c.Db.Events.Where(e => e.HotelId == hotelId && e.VoidedUtc == null);
            events = await q.Where(e => e.ForHandover && e.StartedLocal >= start && e.StartedLocal < end)
                .OrderBy(e => e.StartedLocal).Select(ToItem).ToListAsync(ct);
            // Open entries started before the shift ended (whenever that was).
            open = await q.Where(e => e.CompletedLocal == null && e.StartedLocal < end)
                .OrderBy(e => e.StartedLocal).Take(100).Select(ToItem).ToListAsync(ct);
        }

        var reads = await c.Db.HandoverReads.Where(r => r.HotelId == hotelId && r.ShiftDate == d && r.Shift == s)
            .OrderBy(r => r.ReadUtc)
            .Select(r => new { r.UserId, r.ReadUtc, Name = c.Db.Users.Where(u => u.Id == r.UserId).Select(u => u.DisplayName).First() })
            .ToListAsync(ct);

        await c.Log.LogAsync("View", Modules.Handover, "Handover", $"{hotelId}/{d:yyyy-MM-dd}/{s}", null, ct);
        return Results.Ok(new HandoverDto(hotelId, d, s, start, end, prevDate, prevShift, curDate, curShift, notes, incidents,
            events, open, reads.Select(r => new ReadDto(r.Name, r.ReadUtc)).ToList(), reads.Any(r => r.UserId == uid),
            level >= AccessLevel.Edit));
    }

    static readonly System.Linq.Expressions.Expression<Func<Desk.Core.Events.SecurityEvent, EventItem>> ToItem = e =>
        new EventItem(e.Id, e.Category.Name, e.Detail, e.StartedLocal, e.CompletedLocal,
            e.AssignedTo != null ? e.AssignedTo.DisplayName : null,
            e.Narrative != null && e.Narrative.Length > 300 ? e.Narrative.Substring(0, 300) + "..." : e.Narrative);

    static async Task<IResult> AddNote(NoteRequest req, [AsParameters] Ctx c, CancellationToken ct)
    {
        if (await c.Me.LevelAtAsync(Modules.Handover, req.HotelId, ct) < AccessLevel.Edit) return Results.StatusCode(StatusCodes.Status403Forbidden);
        if (!Enum.IsDefined(req.Shift)) return Bad("Choose the shift.");
        var text = Clean(req.Text, 4000);
        if (text is null) return Bad("Write the note first.");
        if (req.ShiftDate > DateOnly.FromDateTime(PacificNow()).AddDays(1)) return Bad("That shift is too far ahead.");
        var me = await c.Me.GetUserAsync(ct);
        if (me is null) return Bad("Your account is not set up yet.");
        var now = DateTime.UtcNow;
        var n = new HandoverNote { HotelId = req.HotelId, ShiftDate = req.ShiftDate, Shift = req.Shift, Text = text,
            CreatedById = me.Id, CreatedUtc = now, UpdatedUtc = now };
        c.Db.HandoverNotes.Add(n);
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync("AddNote", Modules.Handover, "HandoverNote", n.Id.ToString(), new { req.HotelId, req.ShiftDate, req.Shift }, ct);
        return Results.Ok(new { n.Id });
    }

    static async Task<IResult> EditNote(int id, EditNoteRequest req, [AsParameters] Ctx c, CancellationToken ct)
    {
        var (n, error) = await Own(id, c, ct);
        if (n is null) return error!;
        var text = Clean(req.Text, 4000);
        if (text is null) return Bad("A note cannot be empty; remove it instead.");
        n.Text = text;
        n.UpdatedUtc = DateTime.UtcNow;
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync("EditNote", Modules.Handover, "HandoverNote", n.Id.ToString(), null, ct);
        return Results.Ok();
    }

    static async Task<IResult> RemoveNote(int id, [AsParameters] Ctx c, CancellationToken ct)
    {
        var (n, error) = await Own(id, c, ct);
        if (n is null) return error!;
        n.RemovedUtc = DateTime.UtcNow;
        n.RemovedById = (await c.Me.GetUserAsync(ct))?.Id;
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync("RemoveNote", Modules.Handover, "HandoverNote", n.Id.ToString(), null, ct);
        return Results.Ok();
    }

    // The writer may change their own note; a manager any note.
    static async Task<(HandoverNote?, IResult?)> Own(int id, Ctx c, CancellationToken ct)
    {
        var n = await c.Db.HandoverNotes.FirstOrDefaultAsync(x => x.Id == id && x.RemovedUtc == null, ct);
        if (n is null) return (null, Results.NotFound());
        var level = await c.Me.LevelAtAsync(Modules.Handover, n.HotelId, ct);
        if (level < AccessLevel.Edit) return (null, Results.NotFound());
        var uid = (await c.Me.GetUserAsync(ct))?.Id;
        if (level < AccessLevel.Manage && n.CreatedById != uid) return (null, Bad("Only the writer or a manager can change this note."));
        return (n, null);
    }

    static async Task<IResult> MarkRead(ShiftRequest req, [AsParameters] Ctx c, CancellationToken ct)
    {
        if (await c.Me.LevelAtAsync(Modules.Handover, req.HotelId, ct) < AccessLevel.View) return Results.NotFound();
        var me = await c.Me.GetUserAsync(ct);
        if (me is null) return Bad("Your account is not set up yet.");
        if (!await c.Db.HandoverReads.AnyAsync(r => r.HotelId == req.HotelId && r.ShiftDate == req.ShiftDate && r.Shift == req.Shift
                && r.UserId == me.Id, ct))
        {
            c.Db.HandoverReads.Add(new HandoverRead { HotelId = req.HotelId, ShiftDate = req.ShiftDate, Shift = req.Shift,
                UserId = me.Id, ReadUtc = DateTime.UtcNow });
            await c.Db.SaveChangesAsync(ct);
            await c.Log.LogAsync("ReadHandover", Modules.Handover, "Handover", $"{req.HotelId}/{req.ShiftDate:yyyy-MM-dd}/{req.Shift}", null, ct);
        }
        return Results.Ok();
    }
}
