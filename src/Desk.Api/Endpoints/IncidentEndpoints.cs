using Desk.Api.Auth;
using Desk.Api.Services;
using Desk.Core.Access;
using Desk.Core.Data;
using Desk.Core.Incidents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using static Desk.Api.Endpoints.Shared;

namespace Desk.Api.Endpoints;

// Incident reports (all 5 forms). Access is checked per report: the report's form
// decides the module (High Level and employee injury have their own), and the
// person's hotel limit in that module must include the report's hotel. A report
// someone may not see answers 404, so its existence is not given away.
// Rules for who may edit / submit / approve are in Desk.Core.Incidents.IncidentRules.
public static class IncidentEndpoints
{
    public record SaveRequest(IncidentForm Form, int HotelId, int? TypeId, string? Kind, string? OtherType, string? Heading,
        DateTime? OccurredLocal, DateTime? ReportedLocal, Shift? Shift, int? AreaId, int? LocationId, string? LocationDetail,
        string? Room, string? Narrative, string? FollowUp, IncidentDetails? Details, int? SupervisorId, string? DistributedTo);
    public record NoteRequest(string? Note);
    public record VoidRequest(string? Reason, string? ConfirmNumber);
    public record FlagRequest(bool Flag);

    public record Permissions(bool Edit, bool Submit, bool Review, bool Reopen, bool Void, bool History, bool Flag);
    public record AttachmentDto(int Id, string FileName, string ContentType, long SizeBytes, string? Caption,
        string UploadedBy, DateTime UploadedUtc, bool Inline);
    public record IncidentDto(int Id, string Number, IncidentForm Form, string FormName, IncidentStatus Status, int HotelId,
        int? TypeId, string? TypeName, string? Kind, string? OtherType, string Heading,
        DateTime? OccurredLocal, DateTime? ReportedLocal, Shift? Shift,
        int? AreaId, string? AreaName, int? LocationId, string? LocationName, string? LocationDetail, string? Room,
        string? Narrative, string? FollowUp, IncidentDetails Details,
        int WrittenById, string WrittenByName, int? SupervisorId, string? SupervisorName, string? DistributedTo,
        DateTime CreatedUtc, DateTime UpdatedUtc, DateTime? SubmittedUtc, string? ReviewedByName, DateTime? ReviewedUtc,
        string? ReviewNote, DateTime? VoidedUtc, string? VoidReason, bool ForHandover,
        List<AttachmentDto> Attachments, List<string> Missing, Permissions Can);
    public record IncidentRow(int Id, string Number, IncidentForm Form, IncidentStatus Status, string HotelCode, string? TypeName,
        string? Kind, string Heading, DateTime? OccurredLocal, Shift? Shift, string? AreaName, string? LocationName,
        string WrittenByName, string? SupervisorName, int Attachments);
    public record IncidentPage(int Total, List<IncidentRow> Items);
    public record PersonOption(int Id, string Name);
    public record HistoryEntry(DateTime Utc, string UserName, string Action, string? Details);

    const int PageSize = 50;
    const int MaxAttachments = 60;
    static readonly string[] IncidentModules = [Modules.Incidents, Modules.IncidentsHighLevel, Modules.IncidentsEmployee];

    public static void MapIncidentEndpoints(this WebApplication app)
    {
        // Anyone with at least View in one of the three incident modules gets in;
        // each endpoint then checks the specific report.
        var g = app.MapGroup("/api/incidents").AddEndpointFilter(async (ctx, next) =>
        {
            var me = ctx.HttpContext.RequestServices.GetRequiredService<CurrentUser>();
            var access = await me.GetAccessAsync(ctx.HttpContext.RequestAborted);
            return IncidentModules.Any(m => access[m].Level >= AccessLevel.View)
                ? await next(ctx) : Results.StatusCode(StatusCodes.Status403Forbidden);
        });

        g.MapGet("", Search);
        g.MapGet("/{id:int}", Get);
        g.MapPost("", Create);
        g.MapPut("/{id:int}", Update);
        g.MapPost("/{id:int}/submit", Submit);
        g.MapPost("/{id:int}/approve", (int id, NoteRequest r, [AsParameters] Ctx c, CancellationToken ct) => Review(id, r, approve: true, c, ct));
        g.MapPost("/{id:int}/return", (int id, NoteRequest r, [AsParameters] Ctx c, CancellationToken ct) => Review(id, r, approve: false, c, ct));
        g.MapPost("/{id:int}/reopen", Reopen);
        g.MapPost("/{id:int}/void", Void);
        g.MapGet("/{id:int}/history", History);
        g.MapPost("/{id:int}/handover", Flag);
        g.MapPost("/{id:int}/attachments", Upload);
        g.MapGet("/{id:int}/attachments/{attachmentId:int}", Download);
        g.MapDelete("/{id:int}/attachments/{attachmentId:int}", RemoveAttachment);
        // Supervisors to choose on a report: only for someone who may write that form at that hotel.
        g.MapGet("/reviewers", async (IncidentForm form, int hotelId, CurrentUser me, DeskDbContext db, CancellationToken ct) =>
            await LevelFor(me, form, hotelId, ct) < AccessLevel.Edit
                ? Results.StatusCode(StatusCodes.Status403Forbidden)
                : Results.Ok((await Reviewers(db, form, hotelId, ct)).Select(u => new PersonOption(u.Id, u.DisplayName)).ToList()));
    }

    // The services almost every endpoint needs, bound in one go.
    public record Ctx(DeskDbContext Db, CurrentUser Me, ActivityLogger Log);

    // --- Reading -----------------------------------------------------------------

    static async Task<IResult> Search(string? q, IncidentForm? form, IncidentStatus? status, DateOnly? from, DateOnly? to,
        int? hotelId, int? typeId, bool? mine, bool? toReview, int? page, [AsParameters] Ctx c, CancellationToken ct)
    {
        var access = await c.Me.GetAccessAsync(ct);
        var uid = (await c.Me.GetUserAsync(ct))?.Id ?? -1;
        var query = VisibleTo(c.Db.Incidents, access);

        query = status is { } s ? query.Where(r => r.Status == s) : query.Where(r => r.Status != IncidentStatus.Voided);
        if (form is { } f) query = query.Where(r => r.Form == f);
        if (hotelId is int h) query = query.Where(r => r.HotelId == h);
        if (typeId is int t) query = query.Where(r => r.TypeId == t);
        if (from is { } fd) query = query.Where(r => r.OccurredLocal >= fd.ToDateTime(TimeOnly.MinValue));
        if (to is { } td) query = query.Where(r => r.OccurredLocal < td.AddDays(1).ToDateTime(TimeOnly.MinValue));
        if (mine == true) query = query.Where(r => r.WrittenById == uid);
        if (toReview == true) query = query.Where(r => r.Status == IncidentStatus.AwaitingReview && r.SupervisorId == uid);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var text = q.Trim();
            query = query.Where(r => r.Number.Contains(text) || r.Heading.Contains(text) || r.PeopleSearch!.Contains(text)
                || r.Room == text || r.Narrative!.Contains(text) || r.LocationDetail!.Contains(text));
        }

        var total = await query.CountAsync(ct);
        var p = Math.Max(1, page ?? 1);
        var items = await query
            .OrderByDescending(r => r.OccurredLocal).ThenByDescending(r => r.Id)
            .Skip((p - 1) * PageSize).Take(PageSize)
            .Select(r => new IncidentRow(r.Id, r.Number, r.Form, r.Status, r.Hotel.Code, r.Type!.Name, r.Kind, r.Heading,
                r.OccurredLocal, r.Shift, r.Area!.Name, r.Location!.Name, r.WrittenBy.DisplayName, r.Supervisor!.DisplayName,
                r.Attachments.Count(a => a.RemovedUtc == null)))
            .ToListAsync(ct);

        await c.Log.LogAsync("Search", Modules.Incidents, "IncidentReport", null,
            new { q, form, status, from, to, hotelId, typeId, mine, toReview, page = p, total }, ct);
        return Results.Ok(new IncidentPage(total, items));
    }

    static async Task<IResult> Get(int id, [AsParameters] Ctx c, CancellationToken ct)
    {
        var (r, level) = await Load(id, c, ct);
        if (r is null) return Results.NotFound();
        await c.Log.LogAsync("View", IncidentRules.ModuleFor(r.Form), "IncidentReport", r.Number, null, ct);
        return Results.Ok(await ToDto(r, level, c, ct));
    }

    static async Task<IResult> History(int id, [AsParameters] Ctx c, CancellationToken ct)
    {
        var (r, level) = await Load(id, c, ct);
        if (r is null) return Results.NotFound();
        if (level < AccessLevel.Manage) return Results.StatusCode(StatusCodes.Status403Forbidden);
        var rows = await c.Db.ActivityLog
            .Where(a => a.EntityType == "IncidentReport" && a.EntityId == r.Number)
            .OrderByDescending(a => a.Id).Take(500)
            .Select(a => new HistoryEntry(a.Utc, a.UserName, a.Action, a.Details))
            .ToListAsync(ct);
        return Results.Ok(rows);
    }

    // --- Writing -----------------------------------------------------------------

    static async Task<IResult> Create(SaveRequest req, [AsParameters] Ctx c, CancellationToken ct)
    {
        if (!Enum.IsDefined(req.Form)) return Bad("Unknown report form.");
        var level = await LevelFor(c.Me, req.Form, req.HotelId, ct);
        if (level < AccessLevel.Edit) return Results.StatusCode(StatusCodes.Status403Forbidden);
        var me = await c.Me.GetUserAsync(ct);
        if (me is null) return Bad("Your account is not set up yet. Sign out and in again.");

        var details = req.Details ?? new IncidentDetails();
        if (await Validate(req, details, null, c.Db, ct) is { } error) return Bad(error);

        var now = DateTime.UtcNow;
        var r = new IncidentReport
        {
            Number = "", Form = req.Form, Status = IncidentStatus.Draft, WrittenById = me.Id, CreatedUtc = now, UpdatedUtc = now,
        };
        Apply(r, req, details);
        c.Db.Incidents.Add(r);

        // Numbers run per year (2026-0001). Two officers saving at the same moment can
        // pick the same number; the unique index refuses one and it takes the next.
        for (var attempt = 1; ; attempt++)
        {
            r.Number = await NextNumber(c.Db, ct);
            try { await c.Db.SaveChangesAsync(ct); break; }
            catch (DbUpdateException) when (attempt < 5) { }
        }

        await c.Log.LogAsync("Create", IncidentRules.ModuleFor(r.Form), "IncidentReport", r.Number,
            new { form = r.Form.ToString(), hotel = r.HotelId }, ct);
        return Results.Ok(new { r.Id, r.Number });
    }

    static async Task<IResult> Update(int id, SaveRequest req, [AsParameters] Ctx c, CancellationToken ct)
    {
        var (r, level) = await Load(id, c, ct);
        if (r is null) return Results.NotFound();
        var uid = (await c.Me.GetUserAsync(ct))?.Id;
        if (!IncidentRules.CanEdit(r.Status, level, r.WrittenById == uid))
            return Bad(r.Status == IncidentStatus.Approved ? "This report is approved. A manager has to reopen it first." : "You cannot change this report now.");
        if (req.Form != r.Form) return Bad("The form of a report cannot be changed. Start a new report instead.");
        if (req.HotelId != r.HotelId && await LevelFor(c.Me, r.Form, req.HotelId, ct) < AccessLevel.Edit)
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        var details = req.Details ?? new IncidentDetails();
        if (await Validate(req, details, r, c.Db, ct) is { } error) return Bad(error);

        var before = Snapshot(r);
        Apply(r, req, details);
        var changed = Snapshot(r).Where(kv => before[kv.Key] != kv.Value).Select(kv => kv.Key).ToList();
        if (changed.Count == 0) return Results.Ok(new { r.Id, r.Number });

        r.UpdatedUtc = DateTime.UtcNow;
        await c.Db.SaveChangesAsync(ct);
        // Only the names of the changed fields are logged, not their (sensitive) contents.
        await c.Log.LogAsync("Edit", IncidentRules.ModuleFor(r.Form), "IncidentReport", r.Number, new { changed }, ct);
        return Results.Ok(new { r.Id, r.Number });
    }

    static async Task<IResult> Submit(int id, [AsParameters] Ctx c, CancellationToken ct)
    {
        var (r, level) = await Load(id, c, ct);
        if (r is null) return Results.NotFound();
        var uid = (await c.Me.GetUserAsync(ct))?.Id;
        if (!IncidentRules.CanSubmit(r.Status, level, r.WrittenById == uid)) return Bad("This report cannot be sent for review now.");
        var missing = IncidentRules.MissingForSubmit(r, IncidentDetails.Parse(r.DetailsJson));
        if (missing.Count > 0)
            return Results.BadRequest(new { message = "Fill in before sending: " + string.Join(", ", missing) + ".", missing });

        r.Status = IncidentStatus.AwaitingReview;
        r.SubmittedUtc = r.UpdatedUtc = DateTime.UtcNow;
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync("Submit", IncidentRules.ModuleFor(r.Form), "IncidentReport", r.Number,
            new { supervisor = r.SupervisorId }, ct);
        return Results.Ok();
    }

    static async Task<IResult> Review(int id, NoteRequest req, bool approve, Ctx c, CancellationToken ct)
    {
        var (r, level) = await Load(id, c, ct);
        if (r is null) return Results.NotFound();
        var me = await c.Me.GetUserAsync(ct);
        if (me is null || !IncidentRules.CanReview(r.Status, level, r.WrittenById == me.Id, c.Me.IsAdmin))
            return Bad(r.WrittenById == me?.Id ? "Your own report is approved by your supervisor." : "This report is not waiting for your review.");
        var note = Clean(req.Note, 1000);
        if (!approve && note is null) return Bad("Say what needs to change, so the writer knows.");

        r.Status = approve ? IncidentStatus.Approved : IncidentStatus.Draft;
        r.ReviewedById = me.Id;
        r.ReviewedUtc = r.UpdatedUtc = DateTime.UtcNow;
        r.ReviewNote = note;
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync(approve ? "Approve" : "Return", IncidentRules.ModuleFor(r.Form), "IncidentReport", r.Number,
            new { note }, ct);
        return Results.Ok();
    }

    static async Task<IResult> Reopen(int id, NoteRequest req, [AsParameters] Ctx c, CancellationToken ct)
    {
        var (r, level) = await Load(id, c, ct);
        if (r is null) return Results.NotFound();
        if (!IncidentRules.CanReopen(r.Status, level)) return Bad("Only an approved report can be reopened, by a manager.");
        var reason = Clean(req.Note, 1000);
        if (reason is null) return Bad("Give the reason for reopening; it is kept in the report history.");

        r.Status = IncidentStatus.Draft;
        r.ReviewNote = $"Reopened: {reason}";
        r.UpdatedUtc = DateTime.UtcNow;
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync("Reopen", IncidentRules.ModuleFor(r.Form), "IncidentReport", r.Number, new { reason }, ct);
        return Results.Ok();
    }

    // Cancelling a report is never a delete: it stays, marked Voided with the reason.
    // Like every destructive action, the report number has to be typed to confirm.
    static async Task<IResult> Void(int id, VoidRequest req, [AsParameters] Ctx c, CancellationToken ct)
    {
        var (r, level) = await Load(id, c, ct);
        if (r is null) return Results.NotFound();
        if (!IncidentRules.CanVoid(r.Status, level)) return Bad("Only a manager can void a report.");
        var reason = Clean(req.Reason, 1000);
        if (reason is null) return Bad("Give the reason for voiding.");
        if (!string.Equals(req.ConfirmNumber?.Trim(), r.Number, StringComparison.OrdinalIgnoreCase))
            return Bad($"Type the report number ({r.Number}) to confirm.");

        r.Status = IncidentStatus.Voided;
        r.VoidedUtc = r.UpdatedUtc = DateTime.UtcNow;
        r.VoidReason = reason;
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync("Void", IncidentRules.ModuleFor(r.Form), "IncidentReport", r.Number, new { reason }, ct);
        return Results.Ok();
    }

    // Show (or stop showing) the report on the handover of the shift it happened in.
    // Anyone who may write this kind of report may flag it, whatever its status.
    static async Task<IResult> Flag(int id, FlagRequest req, [AsParameters] Ctx c, CancellationToken ct)
    {
        var (r, level) = await Load(id, c, ct);
        if (r is null) return Results.NotFound();
        if (level < AccessLevel.Edit || r.Status == IncidentStatus.Voided) return Bad("You cannot flag this report.");
        if (r.ForHandover == req.Flag) return Results.Ok();
        r.ForHandover = req.Flag;
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync(req.Flag ? "FlagHandover" : "UnflagHandover", IncidentRules.ModuleFor(r.Form), "IncidentReport", r.Number, null, ct);
        return Results.Ok();
    }

    // --- Attachments -------------------------------------------------------------

    // The form is read by hand (not bound) so no anti-forgery token is expected;
    // the X-CSRF header check covers uploads like every other write.
    static async Task<IResult> Upload(int id, HttpRequest http, FileStore files, [AsParameters] Ctx c, CancellationToken ct)
    {
        var (r, level) = await Load(id, c, ct);
        if (r is null) return Results.NotFound();
        var me = await c.Me.GetUserAsync(ct);
        if (me is null || !IncidentRules.CanEdit(r.Status, level, r.WrittenById == me.Id))
            return Bad("Photos and files can only be added while the report can be changed.");
        if (!http.HasFormContentType) return Bad("No file was sent.");

        var form = await http.ReadFormAsync(ct);
        if (form.Files.Count == 0) return Bad("No file was sent.");
        var existing = await c.Db.IncidentAttachments.CountAsync(a => a.IncidentId == r.Id && a.RemovedUtc == null, ct);
        if (existing + form.Files.Count > MaxAttachments) return Bad($"A report can hold at most {MaxAttachments} files.");
        foreach (var f in form.Files)
        {
            if (FileStore.ContentTypeFor(f.FileName) is null)
                return Bad($"{f.FileName}: this kind of file is not accepted. Accepted: {FileStore.AllowedList}.");
            if (f.Length == 0) return Bad($"{f.FileName} is empty.");
        }

        var caption = Clean(form["caption"].ToString(), 400);
        var added = new List<IncidentAttachment>();
        foreach (var f in form.Files)
        {
            var a = new IncidentAttachment
            {
                IncidentId = r.Id, FileName = SafeName(f.FileName), ContentType = FileStore.ContentTypeFor(f.FileName)!,
                SizeBytes = f.Length, Caption = caption, UploadedById = me.Id, UploadedUtc = DateTime.UtcNow,
                StoredName = await files.SaveAsync(Folder(r.Id), f, ct),
            };
            c.Db.IncidentAttachments.Add(a);
            added.Add(a);
        }
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync("AddAttachment", IncidentRules.ModuleFor(r.Form), "IncidentReport", r.Number,
            added.Select(a => new { a.Id, a.FileName, a.SizeBytes }), ct);
        return Results.Ok(added.Select(a => a.Id));
    }

    static async Task<IResult> Download(int id, int attachmentId, bool? download, FileStore files, [AsParameters] Ctx c,
        HttpResponse response, CancellationToken ct)
    {
        var (r, _) = await Load(id, c, ct);
        if (r is null) return Results.NotFound();
        var a = await c.Db.IncidentAttachments.FirstOrDefaultAsync(x => x.Id == attachmentId && x.IncidentId == r.Id && x.RemovedUtc == null, ct);
        if (a is null) return Results.NotFound();
        var path = files.PathFor(Folder(r.Id), a.StoredName);
        if (!File.Exists(path)) return Results.Problem("The file is missing on the server. Check App health.");

        await c.Log.LogAsync("Download", IncidentRules.ModuleFor(r.Form), "IncidentReport", r.Number, new { a.Id, a.FileName }, ct);
        if (download != true && FileStore.ShowInline(a.ContentType))
        {
            response.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline") { FileNameStar = a.FileName }.ToString();
            response.Headers.CacheControl = "private, no-store";
            return Results.File(path, a.ContentType, enableRangeProcessing: true);
        }
        return Results.File(path, a.ContentType, a.FileName, enableRangeProcessing: true);
    }

    static async Task<IResult> RemoveAttachment(int id, int attachmentId, [AsParameters] Ctx c, CancellationToken ct)
    {
        var (r, level) = await Load(id, c, ct);
        if (r is null) return Results.NotFound();
        var me = await c.Me.GetUserAsync(ct);
        if (me is null || !IncidentRules.CanEdit(r.Status, level, r.WrittenById == me.Id))
            return Bad("Files can only be removed while the report can be changed.");
        var a = await c.Db.IncidentAttachments.FirstOrDefaultAsync(x => x.Id == attachmentId && x.IncidentId == r.Id && x.RemovedUtc == null, ct);
        if (a is null) return Results.NotFound();
        // Hidden, not deleted: the file stays on disk and in the backup.
        a.RemovedUtc = DateTime.UtcNow;
        a.RemovedById = me.Id;
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync("RemoveAttachment", IncidentRules.ModuleFor(r.Form), "IncidentReport", r.Number, new { a.Id, a.FileName }, ct);
        return Results.Ok();
    }

    // --- Helpers -----------------------------------------------------------------

    static string Folder(int reportId) => Path.Combine("incidents", reportId.ToString());

    static (bool Ok, int? Hotel) Scope(EffectiveAccess a) => (a.Level >= AccessLevel.View, a.HotelId);

    // Only the reports this person may see: each form through its own module and hotel
    // limit. Used by the search here and by the safety reports.
    public static IQueryable<IncidentReport> VisibleTo(IQueryable<IncidentReport> q, Dictionary<string, EffectiveAccess> access)
    {
        var (incOk, incHotel) = Scope(access[Modules.Incidents]);
        var (hiOk, hiHotel) = Scope(access[Modules.IncidentsHighLevel]);
        var (empOk, empHotel) = Scope(access[Modules.IncidentsEmployee]);
        return q.Where(r =>
            (incOk && r.Form != IncidentForm.HighLevel && r.Form != IncidentForm.EmployeeInjury && (incHotel == null || r.HotelId == incHotel))
            || (hiOk && r.Form == IncidentForm.HighLevel && (hiHotel == null || r.HotelId == hiHotel))
            || (empOk && r.Form == IncidentForm.EmployeeInjury && (empHotel == null || r.HotelId == empHotel)));
    }

    static Task<AccessLevel> LevelFor(CurrentUser me, IncidentForm form, int hotelId, CancellationToken ct) =>
        me.LevelAtAsync(IncidentRules.ModuleFor(form), hotelId, ct);

    static async Task<(IncidentReport? Report, AccessLevel Level)> Load(int id, Ctx c, CancellationToken ct)
    {
        var r = await c.Db.Incidents.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return (null, AccessLevel.None);
        var level = await LevelFor(c.Me, r.Form, r.HotelId, ct);
        return level >= AccessLevel.View ? (r, level) : (null, AccessLevel.None);
    }

    static async Task<IncidentDto> ToDto(IncidentReport r, AccessLevel level, Ctx c, CancellationToken ct)
    {
        var uid = (await c.Me.GetUserAsync(ct))?.Id;
        var isWriter = r.WrittenById == uid;
        var details = IncidentDetails.Parse(r.DetailsJson);
        var names = await c.Db.Users.Where(u => u.Id == r.WrittenById || u.Id == r.SupervisorId || u.Id == r.ReviewedById)
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        var type = r.TypeId is int t ? await c.Db.ListItems.Where(i => i.Id == t).Select(i => i.Name).FirstOrDefaultAsync(ct) : null;
        var area = r.AreaId is int a ? await c.Db.LocationAreas.Where(x => x.Id == a).Select(x => x.Name).FirstOrDefaultAsync(ct) : null;
        var spot = r.LocationId is int l ? await c.Db.Locations.Where(x => x.Id == l).Select(x => x.Name).FirstOrDefaultAsync(ct) : null;
        var attachments = await c.Db.IncidentAttachments.Where(x => x.IncidentId == r.Id && x.RemovedUtc == null)
            .OrderBy(x => x.Id)
            .Select(x => new AttachmentDto(x.Id, x.FileName, x.ContentType, x.SizeBytes, x.Caption, x.UploadedBy.DisplayName,
                x.UploadedUtc, false))
            .ToListAsync(ct);

        return new IncidentDto(r.Id, r.Number, r.Form, IncidentRules.FormName(r.Form), r.Status, r.HotelId,
            r.TypeId, type, r.Kind, r.OtherType, r.Heading, r.OccurredLocal, r.ReportedLocal, r.Shift,
            r.AreaId, area, r.LocationId, spot, r.LocationDetail, r.Room, r.Narrative, r.FollowUp, details,
            r.WrittenById, names.GetValueOrDefault(r.WrittenById, "?"), r.SupervisorId,
            r.SupervisorId is int s ? names.GetValueOrDefault(s) : null, r.DistributedTo,
            r.CreatedUtc, r.UpdatedUtc, r.SubmittedUtc, r.ReviewedById is int rv ? names.GetValueOrDefault(rv) : null,
            r.ReviewedUtc, r.ReviewNote, r.VoidedUtc, r.VoidReason, r.ForHandover,
            attachments.Select(x => x with { Inline = FileStore.ShowInline(x.ContentType) }).ToList(),
            r.Status == IncidentStatus.Draft ? IncidentRules.MissingForSubmit(r, details) : [],
            new Permissions(
                IncidentRules.CanEdit(r.Status, level, isWriter),
                IncidentRules.CanSubmit(r.Status, level, isWriter),
                IncidentRules.CanReview(r.Status, level, isWriter, c.Me.IsAdmin),
                IncidentRules.CanReopen(r.Status, level),
                IncidentRules.CanVoid(r.Status, level),
                level >= AccessLevel.Manage,
                r.Status != IncidentStatus.Voided && level >= AccessLevel.Edit));
    }

    static void Apply(IncidentReport r, SaveRequest q, IncidentDetails d)
    {
        r.HotelId = q.HotelId;
        r.TypeId = IncidentRules.TypeList(q.Form) is null ? null : q.TypeId;
        r.Kind = IncidentRules.Kinds(q.Form).Length == 0 ? null : Clean(q.Kind, 40);
        r.OtherType = Clean(q.OtherType, 200);
        r.Heading = Clean(q.Heading, 200) ?? "";
        r.OccurredLocal = q.OccurredLocal;
        r.ReportedLocal = q.ReportedLocal;
        r.Shift = q.Shift ?? (q.OccurredLocal is { } o ? IncidentRules.ShiftFor(o) : null);
        r.AreaId = q.AreaId;
        r.LocationId = q.AreaId is null ? null : q.LocationId;
        r.LocationDetail = Clean(q.LocationDetail, 200);
        r.Room = Clean(q.Room, 40);
        r.Narrative = Clean(q.Narrative, 50_000);
        r.FollowUp = Clean(q.FollowUp, 50_000);
        r.SupervisorId = q.SupervisorId;
        r.DistributedTo = Clean(q.DistributedTo, 400);
        r.DetailsJson = d.ToJson();
        r.PeopleSearch = IncidentRules.PeopleSearch(d);
    }

    static Dictionary<string, string?> Snapshot(IncidentReport r) => new()
    {
        ["hotel"] = r.HotelId.ToString(), ["type"] = $"{r.TypeId}{r.Kind}{r.OtherType}", ["heading"] = r.Heading,
        ["occurred"] = r.OccurredLocal?.ToString("O"), ["reported"] = r.ReportedLocal?.ToString("O"), ["shift"] = r.Shift?.ToString(),
        ["location"] = $"{r.AreaId}/{r.LocationId}/{r.LocationDetail}/{r.Room}", ["narrative"] = r.Narrative,
        ["followUp"] = r.FollowUp, ["details"] = r.DetailsJson, ["supervisor"] = r.SupervisorId?.ToString(),
        ["distributedTo"] = r.DistributedTo,
    };

    static async Task<string?> Validate(SaveRequest q, IncidentDetails d, IncidentReport? existing, DeskDbContext db, CancellationToken ct)
    {
        if (!await db.Hotels.AnyAsync(h => h.Id == q.HotelId, ct)) return "Choose the hotel.";
        if (q.Heading?.Trim().Length > 200) return "The heading is too long (200 characters at most).";
        if (q.Narrative?.Length > 50_000 || q.FollowUp?.Length > 50_000) return "The narrative is too long (50,000 characters at most).";

        if (q.TypeId is int t && IncidentRules.TypeList(q.Form) is { } list)
        {
            var item = await db.ListItems.FirstOrDefaultAsync(i => i.Id == t, ct);
            if (item is null || item.ListKey != list || (!item.IsActive && existing?.TypeId != t)) return "Choose a type from the list.";
        }
        if (!string.IsNullOrWhiteSpace(q.Kind) && IncidentRules.Kinds(q.Form).Length > 0 && !IncidentRules.Kinds(q.Form).Contains(q.Kind.Trim()))
            return "Choose a type from the list.";

        if (q.AreaId is int areaId)
        {
            var area = await db.LocationAreas.FirstOrDefaultAsync(a => a.Id == areaId, ct);
            if (area is null || (!area.IsActive && existing?.AreaId != areaId)) return "Choose a location from the list.";
            if (area.HotelId is int ah && ah != q.HotelId) return "That location belongs to another hotel.";
            if (q.LocationId is int locId && !await db.Locations.AnyAsync(l => l.Id == locId && l.AreaId == areaId, ct))
                return "Choose a spot inside the chosen location.";
        }

        var now = PacificNow();
        if (q.OccurredLocal > now.AddHours(1) || q.ReportedLocal > now.AddHours(1)) return "A date and time is in the future.";
        if (q.OccurredLocal is { } o && q.ReportedLocal is { } rep && rep < o.AddMinutes(-1))
            return "It cannot have been reported before it happened.";

        if (IncidentRules.CheckDetails(d) is { } detailsError) return detailsError;
        if (d.ToJson().Length > 200_000) return "The report is too large. Shorten the longest texts.";

        // Every list entry, person and department the sections point at must exist.
        var listIds = new List<(int? Id, string Key)>
        {
            (d.Medical?.InjuryTypeId, ListKeys.InjuryType), (d.Medical?.BodyPartId, ListKeys.BodyPart),
            (d.Medical?.BodyPartLocationId, ListKeys.BodyPartLocation), (d.Medical?.ContributingFactorId, ListKeys.ContributingFactor),
            (d.Vehicle?.CircumstanceId, ListKeys.VehicleCircumstance), (d.Vehicle?.ColorId, ListKeys.Color),
            (d.Evidence?.StatusId, ListKeys.EvidenceStatus),
        };
        listIds.AddRange(d.PropertyItems.SelectMany(i => new[] { (i.CategoryId, ListKeys.PropertyCategory), (i.ColorId, ListKeys.Color) }));
        var wanted = listIds.Where(x => x.Id is not null).Select(x => x.Id!.Value).Distinct().ToList();
        var found = await db.ListItems.Where(i => wanted.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.ListKey, ct);
        if (listIds.Any(x => x.Id is int id && found.GetValueOrDefault(id) != x.Key)) return "A choice on the report is not in its list.";

        var people = d.Subjects.Concat(d.Victims).Concat(d.Witnesses).Append(d.ReportedBy).Where(p => p is not null).ToList();
        var userIds = people.Select(p => p!.UserId).Concat(d.LockInterrogation.Select(l => l.UserId))
            .Where(x => x is not null).Select(x => x!.Value).Distinct().ToList();
        if (userIds.Count > 0 && await db.Users.CountAsync(u => userIds.Contains(u.Id), ct) != userIds.Count)
            return "A person chosen on the report is not in Shore Desk.";
        var deptIds = people.Select(p => p!.DepartmentId).Concat(d.LockInterrogation.Select(l => l.DepartmentId))
            .Where(x => x is not null).Select(x => x!.Value).Distinct().ToList();
        if (deptIds.Count > 0 && await db.Departments.CountAsync(x => deptIds.Contains(x.Id), ct) != deptIds.Count)
            return "A department chosen on the report is not in the list.";

        if (q.SupervisorId is int sup && sup != existing?.SupervisorId
            && !(await Reviewers(db, q.Form, q.HotelId, ct)).Any(u => u.Id == sup))
            return "Choose a supervisor who manages this kind of report at this hotel.";
        return null;
    }

    // People who may approve this form at this hotel: Manage in its module (admins included).
    static async Task<List<AppUser>> Reviewers(DeskDbContext db, IncidentForm form, int hotelId, CancellationToken ct)
    {
        var module = Modules.Find(IncidentRules.ModuleFor(form))!;
        var users = await db.Users.Where(u => u.BlockedUtc == null && u.RemovedUtc == null).ToListAsync(ct);
        var personal = await db.UserAccess.Where(a => a.Module == module.Key).ToDictionaryAsync(a => a.AppUserId, ct);
        var defaults = await db.DepartmentAccess.Where(a => a.Module == module.Key).ToDictionaryAsync(a => a.DepartmentId, ct);
        return users
            .Where(u => AccessRules.Resolve(module, u.IsAdmin, u.DepartmentId, personal.GetValueOrDefault(u.Id)?.ToSpec(),
                    u.DepartmentId is int d ? defaults.GetValueOrDefault(d)?.ToSpec() : null)
                .Allows(AccessLevel.Manage, hotelId))
            .OrderBy(u => u.DisplayName)
            .ToList();
    }

    static async Task<string> NextNumber(DeskDbContext db, CancellationToken ct)
    {
        var prefix = $"{PacificNow().Year}-";
        var last = await db.Incidents.Where(r => r.Number.StartsWith(prefix)).OrderByDescending(r => r.Id)
            .Select(r => r.Number).FirstOrDefaultAsync(ct);
        var n = last is not null && int.TryParse(last[prefix.Length..], out var x) ? x + 1 : 1;
        return $"{prefix}{n:D4}";
    }

    static string SafeName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        foreach (var bad in Path.GetInvalidFileNameChars().Append('"')) name = name.Replace(bad, '_');
        return name.Length > 200 ? name[^200..] : name;
    }
}
