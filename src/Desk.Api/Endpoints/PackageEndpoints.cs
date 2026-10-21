using Desk.Api.Auth;
using Desk.Api.Services;
using Desk.Core.Access;
using Desk.Core.Data;
using Desk.Core.Packages;
using Microsoft.EntityFrameworkCore;
using static Desk.Api.Endpoints.Shared;

namespace Desk.Api.Endpoints;

// Packages in and out of the desk. Module "packages": View sees, Edit logs packages,
// notifies, hands them over (with signature) and ships them. Hotel limits apply.
public static class PackageEndpoints
{
    public record PackageRequest(int HotelId, PackageDirection Direction, DateTime? ReceivedLocal, string? Carrier, string? PackageType,
        string? TrackingNumber, int Count, string? Weight, decimal? HandlingFee, string? PersonType, string? PersonName, int? PersonUserId,
        string? Room, string? Company, DateOnly? ArrivalDate, DateOnly? DepartureDate, string? GroupName, string? OtherParty,
        string? StorageLocation, string? Notes);
    public record NotifyRequest(string? How, DateTime? AtLocal);
    public record CloseRequest(PackageStatus Status, string? Name, string? Signature, string? Note);
    public record FlagRequest(bool Flag);

    public record PackageDto(int Id, string Number, int HotelId, PackageDirection Direction, DateTime ReceivedLocal, string Carrier, string PackageType,
        string? TrackingNumber, int Count, string? Weight, decimal? HandlingFee, string PersonType, string PersonName, int? PersonUserId, string? Room,
        string? Company, DateOnly? ArrivalDate, DateOnly? DepartureDate, string? GroupName, string? OtherParty, string? StorageLocation,
        DateTime? NotifiedLocal, string? NotifiedHow, string? NotifiedBy, PackageStatus Status, DateTime? ClosedLocal, string? ClosedToName,
        bool HasSignature, string? ClosedBy, string? Notes, bool ForHandover, string CreatedBy, double DaysWaiting, bool Old);

    public static void MapPackageEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/packages").RequireModule(Modules.Packages, AccessLevel.View);
        g.MapGet("", List);
        g.MapGet("/{id:int}", Get);
        g.MapGet("/lookup", Lookup);
        g.MapGet("/{id:int}/signature", Signature);
        g.MapPost("", (PackageRequest r, [AsParameters] Ctx c, CancellationToken ct) => Save(null, r, c, ct)).RequireModule(Modules.Packages, AccessLevel.Edit);
        g.MapPut("/{id:int}", (int id, PackageRequest r, [AsParameters] Ctx c, CancellationToken ct) => Save(id, r, c, ct)).RequireModule(Modules.Packages, AccessLevel.Edit);
        g.MapPost("/{id:int}/notify", Notify).RequireModule(Modules.Packages, AccessLevel.Edit);
        g.MapPost("/{id:int}/close", Close).RequireModule(Modules.Packages, AccessLevel.Edit);
        g.MapPost("/{id:int}/handover", Flag).RequireModule(Modules.Packages, AccessLevel.Edit);
    }

    public record Ctx(DeskDbContext Db, CurrentUser Me, ActivityLogger Log);
    const string M = Modules.Packages;

    // view: waiting (default) | today | search (q, from, to)
    static async Task<IResult> List(int hotelId, string? view, PackageDirection? direction, string? q, DateOnly? from, DateOnly? to,
        [AsParameters] Ctx c, CancellationToken ct)
    {
        if (await c.Me.LevelAtAsync(M, hotelId, ct) < AccessLevel.View) return Results.NotFound();
        var query = c.Db.Packages.Where(p => p.HotelId == hotelId);
        if (direction is { } d) query = query.Where(p => p.Direction == d);
        var today = DateOnly.FromDateTime(PacificNow());
        switch (view)
        {
            case "today":
                query = query.Where(p => p.ReceivedLocal >= today.ToDateTime(TimeOnly.MinValue) || p.ClosedLocal >= today.ToDateTime(TimeOnly.MinValue));
                break;
            case "search":
                var f = (from ?? today.AddDays(-90)).ToDateTime(TimeOnly.MinValue);
                var t = (to ?? today).AddDays(1).ToDateTime(TimeOnly.MinValue);
                query = query.Where(p => p.ReceivedLocal >= f && p.ReceivedLocal < t);
                if (!string.IsNullOrWhiteSpace(q))
                {
                    var text = q.Trim();
                    query = query.Where(p => p.Number.Contains(text) || p.TrackingNumber!.Contains(text) || p.PersonName.Contains(text)
                        || p.Room == text || p.GroupName!.Contains(text) || p.Company!.Contains(text) || p.OtherParty!.Contains(text));
                }
                break;
            default:
                query = query.Where(p => p.Status == PackageStatus.Waiting);
                break;
        }
        var list = await Load(c.Db, query.OrderByDescending(p => p.ReceivedLocal).Take(500), ct);
        await c.Log.LogAsync(view == "search" ? "Search" : "View", M, "PackageList", hotelId.ToString(), new { view, direction, q, count = list.Count }, ct);
        return Results.Ok(list);
    }

    // Waiting packages for the handover page.
    public static Task<List<PackageDto>> Waiting(DeskDbContext db, int hotelId, CancellationToken ct) =>
        Load(db, db.Packages.Where(p => p.HotelId == hotelId && p.Status == PackageStatus.Waiting && p.ForHandover).OrderBy(p => p.ReceivedLocal), ct);

    static async Task<List<PackageDto>> Load(DeskDbContext db, IQueryable<Package> q, CancellationToken ct)
    {
        var now = PacificNow();
        var rows = await q.Select(p => new
        {
            P = p,
            Notified = db.Users.Where(u => u.Id == p.NotifiedById).Select(u => u.DisplayName).FirstOrDefault(),
            Closed = db.Users.Where(u => u.Id == p.ClosedById).Select(u => u.DisplayName).FirstOrDefault(),
            Created = db.Users.Where(u => u.Id == p.CreatedById).Select(u => u.DisplayName).First(),
            HasSig = p.Signature != null,
        }).ToListAsync(ct);
        return rows.Select(r =>
        {
            var p = r.P;
            var days = Math.Round(((p.ClosedLocal ?? now) - p.ReceivedLocal).TotalDays, 1);
            return new PackageDto(p.Id, p.Number, p.HotelId, p.Direction, p.ReceivedLocal, p.Carrier, p.PackageType, p.TrackingNumber, p.Count,
                p.Weight, p.HandlingFee, p.PersonType, p.PersonName, p.PersonUserId, p.Room, p.Company, p.ArrivalDate, p.DepartureDate, p.GroupName,
                p.OtherParty, p.StorageLocation, p.NotifiedLocal, p.NotifiedHow, r.Notified, p.Status, p.ClosedLocal, p.ClosedToName, r.HasSig,
                r.Closed, p.Notes, p.ForHandover, r.Created, days, p.Status == PackageStatus.Waiting && days > PackageRules.OldAfterDays);
        }).ToList();
    }

    static async Task<IResult> Get(int id, [AsParameters] Ctx c, CancellationToken ct)
    {
        var (p, _) = await Find(id, c, ct);
        if (p is null) return Results.NotFound();
        await c.Log.LogAsync("View", M, "Package", p.Number, null, ct);
        return Results.Ok((await Load(c.Db, c.Db.Packages.Where(x => x.Id == p.Id), ct)).Single());
    }

    // A scanner reads the tracking barcode: find the waiting package with that number.
    static async Task<IResult> Lookup(int hotelId, string code, [AsParameters] Ctx c, CancellationToken ct)
    {
        if (await c.Me.LevelAtAsync(M, hotelId, ct) < AccessLevel.View) return Results.NotFound();
        var text = code.Trim();
        var id = await c.Db.Packages.Where(p => p.HotelId == hotelId && (p.TrackingNumber == text || p.Number == text))
            .OrderBy(p => p.Status == PackageStatus.Waiting ? 0 : 1).ThenByDescending(p => p.Id).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        return id is int found ? Results.Ok(new { id = found }) : Results.NotFound();
    }

    static async Task<IResult> Signature(int id, [AsParameters] Ctx c, CancellationToken ct)
    {
        var (p, _) = await Find(id, c, ct);
        if (p?.Signature is null) return Results.NotFound();
        await c.Log.LogAsync("ViewSignature", M, "Package", p.Number, null, ct);
        return Results.File(p.Signature, "image/png");
    }

    static async Task<IResult> Save(int? id, PackageRequest req, Ctx c, CancellationToken ct)
    {
        if (await c.Me.LevelAtAsync(M, req.HotelId, ct) < AccessLevel.Edit) return Results.StatusCode(StatusCodes.Status403Forbidden);
        if (!Enum.IsDefined(req.Direction)) return Bad("Incoming or outgoing?");
        if (req.Carrier is null || !PackageRules.Carriers.Contains(req.Carrier)) return Bad("Choose the carrier.");
        if (req.PackageType is null || !PackageRules.Types.Contains(req.PackageType)) return Bad("Choose the package type.");
        if (req.PersonType is null || !PackageRules.PersonTypes.Contains(req.PersonType)) return Bad("Choose who it is for (guest, employee, ...).");
        if (req.Count is < 1 or > 500) return Bad("Check the number of packages.");
        if (req.HandlingFee is < 0 or > 10_000) return Bad("Check the handling fee.");
        string? name = Clean(req.PersonName, 200);
        if (req.PersonUserId is int uid)
        {
            name = await c.Db.Users.Where(u => u.Id == uid).Select(u => u.DisplayName).FirstOrDefaultAsync(ct);
            if (name is null) return Bad("That person is not in Shore Desk.");
        }
        if (name is null) return Bad(req.Direction == PackageDirection.Incoming ? "Who is it for?" : "Who is sending it?");
        var now = PacificNow();
        var at = req.ReceivedLocal ?? now;
        if (at > now.AddMinutes(10)) return Bad("The time is in the future.");
        var me = await c.Me.GetUserAsync(ct);
        if (me is null) return Bad("Your account is not set up yet.");

        var p = id is int i ? (await Find(i, c, ct)).Item1 : null;
        if (id is not null && p is null) return Results.NotFound();
        if (p is not null && (p.Status != PackageStatus.Waiting || p.HotelId != req.HotelId || p.Direction != req.Direction))
            return Bad("This package is closed, or belongs to another hotel.");
        p ??= c.Db.Packages.Add(new Package { Number = "", HotelId = req.HotelId, Direction = req.Direction, Carrier = req.Carrier, PackageType = req.PackageType,
            PersonType = req.PersonType, PersonName = name, CreatedById = me.Id, CreatedUtc = DateTime.UtcNow }).Entity;
        p.ReceivedLocal = new DateTime(at.Year, at.Month, at.Day, at.Hour, at.Minute, 0); p.Carrier = req.Carrier; p.PackageType = req.PackageType;
        p.TrackingNumber = Clean(req.TrackingNumber, 80); p.Count = req.Count; p.Weight = Clean(req.Weight, 40); p.HandlingFee = req.HandlingFee;
        p.PersonType = req.PersonType; p.PersonName = name; p.PersonUserId = req.PersonUserId; p.Room = Clean(req.Room, 40); p.Company = Clean(req.Company, 200);
        p.ArrivalDate = req.ArrivalDate; p.DepartureDate = req.DepartureDate; p.GroupName = Clean(req.GroupName, 200); p.OtherParty = Clean(req.OtherParty, 400);
        p.StorageLocation = Clean(req.StorageLocation, 100); p.Notes = Clean(req.Notes, 2000); p.UpdatedUtc = DateTime.UtcNow;
        if (id is null) await SaveNumbered(c.Db, p, ct); else await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync(id is null ? "LogPackage" : "EditPackage", M, "Package", p.Number, new { direction = p.Direction.ToString(), p.Count }, ct);
        return Results.Ok(new { p.Id, p.Number });
    }

    static async Task<IResult> Notify(int id, NotifyRequest req, [AsParameters] Ctx c, CancellationToken ct)
    {
        var (p, level) = await Find(id, c, ct);
        if (p is null) return Results.NotFound();
        if (level < AccessLevel.Edit || p.Status != PackageStatus.Waiting || p.Direction != PackageDirection.Incoming) return Bad("This package is not waiting.");
        if (req.How is null || !PackageRules.NotifyMethods.Contains(req.How)) return Bad("How were they told?");
        var at = req.AtLocal ?? PacificNow();
        p.NotifiedLocal = new DateTime(at.Year, at.Month, at.Day, at.Hour, at.Minute, 0);
        p.NotifiedHow = req.How;
        p.NotifiedById = (await c.Me.GetUserAsync(ct))?.Id;
        p.UpdatedUtc = DateTime.UtcNow;
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync("NotifyPackage", M, "Package", p.Number, new { req.How }, ct);
        return Results.Ok();
    }

    // Incoming: PickedUp (signature) / Delivered (to the room) / ReturnedToSender.
    // Outgoing: Shipped (the carrier collected it).
    static async Task<IResult> Close(int id, CloseRequest req, [AsParameters] Ctx c, CancellationToken ct)
    {
        var (p, level) = await Find(id, c, ct);
        if (p is null) return Results.NotFound();
        if (level < AccessLevel.Edit || p.Status != PackageStatus.Waiting) return Bad("This package is already closed.");
        var allowed = p.Direction == PackageDirection.Incoming
            ? new[] { PackageStatus.PickedUp, PackageStatus.Delivered, PackageStatus.ReturnedToSender }
            : [PackageStatus.Shipped];
        if (!allowed.Contains(req.Status)) return Bad("That does not apply to this package.");
        var name = Clean(req.Name, 200);
        if (req.Status is PackageStatus.PickedUp or PackageStatus.Delivered && name is null) return Bad("Who received it?");
        var (sig, sigError) = KeyEndpoints.ParseSignature(req.Signature);
        if (sigError is not null) return Bad(sigError);
        if (req.Status == PackageStatus.PickedUp && sig is null) return Bad("The person picking it up signs on the screen.");
        p.Status = req.Status;
        p.ClosedToName = name;
        p.Signature = sig;
        p.ClosedLocal = new DateTime(PacificNow().Ticks / TimeSpan.TicksPerMinute * TimeSpan.TicksPerMinute);
        p.ClosedById = (await c.Me.GetUserAsync(ct))?.Id;
        p.ForHandover = false;
        if (Clean(req.Note, 1000) is { } note) p.Notes = string.IsNullOrEmpty(p.Notes) ? note : $"{p.Notes}\n{note}";
        p.UpdatedUtc = DateTime.UtcNow;
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync("ClosePackage", M, "Package", p.Number, new { status = req.Status.ToString(), to = name }, ct);
        return Results.Ok();
    }

    static async Task<IResult> Flag(int id, FlagRequest req, [AsParameters] Ctx c, CancellationToken ct)
    {
        var (p, level) = await Find(id, c, ct);
        if (p is null) return Results.NotFound();
        if (level < AccessLevel.Edit) return Results.StatusCode(StatusCodes.Status403Forbidden);
        p.ForHandover = req.Flag;
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync(req.Flag ? "FlagHandover" : "UnflagHandover", M, "Package", p.Number, null, ct);
        return Results.Ok();
    }

    static async Task<(Package?, AccessLevel)> Find(int id, Ctx c, CancellationToken ct)
    {
        var p = await c.Db.Packages.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return (null, AccessLevel.None);
        var level = await c.Me.LevelAtAsync(M, p.HotelId, ct);
        return level >= AccessLevel.View ? (p, level) : (null, AccessLevel.None);
    }

    static async Task SaveNumbered(DeskDbContext db, Package p, CancellationToken ct)
    {
        var prefix = $"P{PacificNow().Year}-";
        for (var attempt = 1; ; attempt++)
        {
            var last = await db.Packages.Where(x => x.Number.StartsWith(prefix)).OrderByDescending(x => x.Id).Select(x => x.Number).FirstOrDefaultAsync(ct);
            p.Number = $"{prefix}{(last is not null && int.TryParse(last[prefix.Length..], out var n) ? n + 1 : 1):D4}";
            try { await db.SaveChangesAsync(ct); return; }
            catch (DbUpdateException) when (attempt < 5) { }
        }
    }
}
