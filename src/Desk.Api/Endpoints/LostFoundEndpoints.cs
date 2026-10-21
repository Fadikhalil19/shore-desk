using Desk.Api.Auth;
using Desk.Api.Services;
using Desk.Core.Access;
using Desk.Core.Data;
using Desk.Core.LostFound;
using Microsoft.EntityFrameworkCore;
using static Desk.Api.Endpoints.Shared;

namespace Desk.Api.Endpoints;

// Lost & found: found items, guests' lost reports with suggested matches, return in
// person (with signature), shipping (payment status only - never card data) and
// disposal after the purge date. Module "lostfound": View sees, Edit logs and returns
// items and handles shipping, Manage disposes. Hotel limits apply per record.
public static class LostFoundEndpoints
{
    public record FoundRequest(int HotelId, int? CategoryId, string? Detail, string? Description, int? ColorId, string? Brand,
        string? ModelSerial, bool HighValue, DateTime? FoundLocal, int? AreaId, string? LocationDetail, string? Room,
        string? FoundByType, string? FoundByName, int? FoundByUserId, int? FoundByDepartmentId, string? StorageLocation,
        DateOnly? PurgeDate, string? OwnerName, string? OwnerContact, string? Notes);
    public record ReturnRequest(string? Name, string? Signature, string? Note, int? LostReportId);
    public record DisposeRequest(string? Method, string? Note);
    public record FlagRequest(bool Flag);
    public record LostRequest(int HotelId, string? OwnerName, string? Phone, string? Email, string? Room, DateOnly? ArrivalDate,
        DateOnly? DepartureDate, int? CategoryId, string? Description, int? ColorId, string? Brand, string? LostWhere,
        DateOnly? LostFrom, DateOnly? LostTo, string? Notes);
    public record MatchRequest(int FoundItemId);
    public record NoteRequest(string? Note);
    public record ShipmentRequest(string? RecipientName, string? Street, string? City, string? State, string? Zip, string? Country,
        string? Phone, string? Email, string? Carrier, string? TrackingNumber, decimal? Cost, PaymentStatus Payment, string? PaymentNote);
    public record ShippedRequest(DateTime? ShippedLocal, string? Carrier, string? TrackingNumber);

    public record FoundRow(int Id, string Number, int HotelId, string? Category, string? Detail, string? Color, string? Brand,
        DateTime FoundLocal, string? Area, string? Room, string? StorageLocation, DateOnly PurgeDate, FoundStatus Status, bool HighValue,
        bool HasPhoto, bool ForHandover, string? ShipmentPayment);
    public record ShipmentDto(int Id, int FoundItemId, string ItemNumber, string? ItemName, string RecipientName, string? Street, string? City,
        string? State, string? Zip, string? Country, string? Phone, string? Email, string? Carrier, string? TrackingNumber, decimal? Cost,
        PaymentStatus Payment, string? PaymentNote, DateTime? ShippedLocal, DateTime? CancelledUtc, string CreatedBy, DateTime CreatedUtc);
    public record LostRow(int Id, string Number, int HotelId, DateTime ReportedLocal, string OwnerName, string? Room, string? Category,
        string? Color, string? Brand, string? Description, LostStatus Status, int? FoundItemId, bool ForHandover);
    public record Match(int Id, string Number, int Score, string Summary, DateTime When);
    public record FoundDto(FoundRow Item, string? Description, string? ModelSerial, int? CategoryId, int? ColorId, int? AreaId,
        string? LocationDetail, string? FoundByType, string? FoundByName, int? FoundByUserId, int? FoundByDepartmentId, string? OwnerName,
        string? OwnerContact, string? Notes, DateTime? ClosedLocal, string? ClosedBy, string? ReturnedToName, bool HasReturnSignature,
        string? DisposalMethod, string? CloseNote, string CreatedBy, List<ShipmentDto> Shipments, List<Match> Matches, bool CanEdit, bool CanDispose);
    public record LostDto(LostRow Report, string? Phone, string? Email, DateOnly? ArrivalDate, DateOnly? DepartureDate, int? CategoryId,
        int? ColorId, string? LostWhere, DateOnly? LostFrom, DateOnly? LostTo, string? Notes, string CreatedBy, List<Match> Matches);

    public static void MapLostFoundEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/lostfound").RequireModule(Modules.LostFound, AccessLevel.View);
        var edit = (RouteHandlerBuilder b) => b.RequireModule(Modules.LostFound, AccessLevel.Edit);
        g.MapGet("/found", FoundList);
        g.MapGet("/found/{id:int}", GetFound);
        edit(g.MapPost("/found", (FoundRequest r, [AsParameters] Ctx c, CancellationToken ct) => SaveFound(null, r, c, ct)));
        edit(g.MapPut("/found/{id:int}", (int id, FoundRequest r, [AsParameters] Ctx c, CancellationToken ct) => SaveFound(id, r, c, ct)));
        edit(g.MapPost("/found/{id:int}/photo", UploadPhoto));
        g.MapGet("/found/{id:int}/photo", Photo);
        g.MapGet("/found/{id:int}/signature", ReturnSignature);
        edit(g.MapPost("/found/{id:int}/return", Return));
        g.MapPost("/found/{id:int}/dispose", Dispose).RequireModule(Modules.LostFound, AccessLevel.Manage);
        edit(g.MapPost("/found/{id:int}/handover", (int id, FlagRequest r, [AsParameters] Ctx c, CancellationToken ct) => FlagFound(id, r, c, ct)));
        edit(g.MapPost("/found/{id:int}/shipments", CreateShipment));
        g.MapGet("/shipments", Shipments);
        edit(g.MapPut("/shipments/{id:int}", UpdateShipment));
        edit(g.MapPost("/shipments/{id:int}/shipped", MarkShipped));
        edit(g.MapPost("/shipments/{id:int}/cancel", CancelShipment));
        g.MapGet("/lost", LostList);
        g.MapGet("/lost/{id:int}", GetLost);
        edit(g.MapPost("/lost", (LostRequest r, [AsParameters] Ctx c, CancellationToken ct) => SaveLost(null, r, c, ct)));
        edit(g.MapPut("/lost/{id:int}", (int id, LostRequest r, [AsParameters] Ctx c, CancellationToken ct) => SaveLost(id, r, c, ct)));
        edit(g.MapPost("/lost/{id:int}/match", MatchLost));
        edit(g.MapPost("/lost/{id:int}/close", CloseLost));
        edit(g.MapPost("/lost/{id:int}/handover", (int id, FlagRequest r, [AsParameters] Ctx c, CancellationToken ct) => FlagLost(id, r, c, ct)));
    }

    public record Ctx(DeskDbContext Db, CurrentUser Me, ActivityLogger Log);
    const string M = Modules.LostFound;

    // --- Found items -----------------------------------------------------------------

    // view: storage (default) | due (past purge date) | closed | all
    static async Task<IResult> FoundList(int hotelId, string? view, string? q, [AsParameters] Ctx c, CancellationToken ct)
    {
        if (await c.Me.LevelAtAsync(M, hotelId, ct) < AccessLevel.View) return Results.NotFound();
        var today = DateOnly.FromDateTime(PacificNow());
        var query = c.Db.FoundItems.Where(f => f.HotelId == hotelId);
        query = view switch
        {
            "due" => query.Where(f => f.Status == FoundStatus.InStorage && f.PurgeDate <= today),
            "closed" => query.Where(f => f.Status != FoundStatus.InStorage),
            "all" => query,
            _ => query.Where(f => f.Status == FoundStatus.InStorage),
        };
        if (!string.IsNullOrWhiteSpace(q))
        {
            var t = q.Trim();
            query = query.Where(f => f.Number.Contains(t) || f.Detail!.Contains(t) || f.Description!.Contains(t) || f.Brand!.Contains(t)
                || f.Room == t || f.OwnerName!.Contains(t) || f.ModelSerial!.Contains(t));
        }
        var rows = await Rows(c.Db, query.OrderByDescending(f => f.FoundLocal).Take(500), ct);
        await c.Log.LogAsync(string.IsNullOrWhiteSpace(q) ? "View" : "Search", M, "FoundList", hotelId.ToString(), new { view, q, count = rows.Count }, ct);
        return Results.Ok(rows);
    }

    // Also used by the handover page (flagged found items).
    public static Task<List<FoundRow>> Rows(DeskDbContext db, IQueryable<FoundItem> q, CancellationToken ct) =>
        q.Select(f => new FoundRow(f.Id, f.Number, f.HotelId,
            db.ListItems.Where(i => i.Id == f.CategoryId).Select(i => i.Name).FirstOrDefault(), f.Detail,
            db.ListItems.Where(i => i.Id == f.ColorId).Select(i => i.Name).FirstOrDefault(), f.Brand, f.FoundLocal,
            f.Area != null ? f.Area.Name : null, f.Room, f.StorageLocation, f.PurgeDate, f.Status, f.HighValue, f.PhotoStoredName != null,
            f.ForHandover,
            db.Shipments.Where(s => s.FoundItemId == f.Id && s.CancelledUtc == null).OrderByDescending(s => s.Id).Select(s => s.Payment.ToString()).FirstOrDefault()))
            .ToListAsync(ct);

    static async Task<IResult> GetFound(int id, [AsParameters] Ctx c, CancellationToken ct)
    {
        var (f, level) = await LoadFound(id, c, ct);
        if (f is null) return Results.NotFound();
        var row = (await Rows(c.Db, c.Db.FoundItems.Where(x => x.Id == f.Id), ct)).Single();
        var names = await c.Db.Users.Where(u => u.Id == f.CreatedById || u.Id == f.ClosedById).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        var matches = f.Status == FoundStatus.InStorage
            ? (await c.Db.LostReports.Where(l => l.HotelId == f.HotelId && l.Status == LostStatus.Open).ToListAsync(ct))
                .Select(l => (l, s: LostFoundRules.MatchScore(l, f))).Where(x => x.s > 0).OrderByDescending(x => x.s).Take(10)
                .Select(x => new Match(x.l.Id, x.l.Number, x.s, $"{x.l.OwnerName}{(x.l.Room is null ? "" : $", room {x.l.Room}")}: {x.l.Description}", x.l.ReportedLocal)).ToList()
            : [];
        await c.Log.LogAsync("View", M, "FoundItem", f.Number, null, ct);
        return Results.Ok(new FoundDto(row, f.Description, f.ModelSerial, f.CategoryId, f.ColorId, f.AreaId, f.LocationDetail, f.FoundByType,
            f.FoundByName, f.FoundByUserId, f.FoundByDepartmentId, f.OwnerName, f.OwnerContact, f.Notes, f.ClosedLocal,
            f.ClosedById is int cb ? names.GetValueOrDefault(cb) : null, f.ReturnedToName, f.ReturnSignature is not null, f.DisposalMethod,
            f.CloseNote, names.GetValueOrDefault(f.CreatedById, "?"), await ShipmentRows(c.Db, c.Db.Shipments.Where(s => s.FoundItemId == f.Id), ct),
            matches, f.Status == FoundStatus.InStorage && level >= AccessLevel.Edit,
            LostFoundRules.CanDispose(f, level, DateOnly.FromDateTime(PacificNow()))));
    }

    static async Task<IResult> SaveFound(int? id, FoundRequest req, Ctx c, CancellationToken ct)
    {
        if (await c.Me.LevelAtAsync(M, req.HotelId, ct) < AccessLevel.Edit) return Results.StatusCode(StatusCodes.Status403Forbidden);
        if (await CheckLists(c.Db, req.CategoryId, req.ColorId, ct) is { } e1) return Bad(e1);
        if (req.CategoryId is null && Clean(req.Detail, 200) is null) return Bad("Choose a category or describe the item.");
        if (req.FoundByType is { } t && !LostFoundRules.FinderTypes.Contains(t)) return Bad("Unknown finder type.");
        if (req.AreaId is int a)
        {
            var area = await c.Db.LocationAreas.FirstOrDefaultAsync(x => x.Id == a, ct);
            if (area is null || (area.HotelId is int ah && ah != req.HotelId)) return Bad("Choose a location from this hotel.");
        }
        if (req.FoundByUserId is int fu && !await c.Db.Users.AnyAsync(u => u.Id == fu, ct)) return Bad("That person is not in Shore Desk.");
        if (new[] { req.Description, req.Notes, req.OwnerContact }.Any(LostFoundRules.LooksLikeCardNumber))
            return Bad("That looks like a card number. Card numbers are never stored here.");
        var now = PacificNow();
        var foundAt = req.FoundLocal ?? now;
        if (foundAt > now.AddMinutes(10)) return Bad("The time found is in the future.");
        var me = await c.Me.GetUserAsync(ct);
        if (me is null) return Bad("Your account is not set up yet.");

        var f = id is int i ? (await LoadFound(i, c, ct)).Item1 : null;
        if (id is not null && f is null) return Results.NotFound();
        if (f is not null && (f.Status != FoundStatus.InStorage || f.HotelId != req.HotelId)) return Bad("This item is closed, or belongs to another hotel.");
        f ??= c.Db.FoundItems.Add(new FoundItem { Number = "", HotelId = req.HotelId, CreatedById = me.Id, CreatedUtc = DateTime.UtcNow }).Entity;
        f.CategoryId = req.CategoryId; f.Detail = Clean(req.Detail, 200); f.Description = Clean(req.Description, 2000); f.ColorId = req.ColorId;
        f.Brand = Clean(req.Brand, 100); f.ModelSerial = Clean(req.ModelSerial, 100); f.HighValue = req.HighValue; f.FoundLocal = Minute(foundAt);
        f.AreaId = req.AreaId; f.LocationDetail = Clean(req.LocationDetail, 200); f.Room = Clean(req.Room, 40); f.FoundByType = req.FoundByType;
        f.FoundByUserId = req.FoundByUserId; f.FoundByDepartmentId = req.FoundByDepartmentId;
        f.FoundByName = req.FoundByUserId is int u2 ? await c.Db.Users.Where(u => u.Id == u2).Select(u => u.DisplayName).FirstAsync(ct) : Clean(req.FoundByName, 200);
        f.StorageLocation = Clean(req.StorageLocation, 100);
        f.PurgeDate = req.PurgeDate ?? DateOnly.FromDateTime(foundAt).AddDays(LostFoundRules.DefaultKeepDays);
        f.OwnerName = Clean(req.OwnerName, 200); f.OwnerContact = Clean(req.OwnerContact, 200); f.Notes = Clean(req.Notes, 2000);
        f.UpdatedUtc = DateTime.UtcNow;
        if (id is null) await SaveNumbered(c.Db, f, "F", ct); else await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync(id is null ? "LogFound" : "EditFound", M, "FoundItem", f.Number, new { f.HighValue }, ct);
        return Results.Ok(new { f.Id, f.Number });
    }

    static async Task<IResult> UploadPhoto(int id, HttpRequest http, FileStore files, [AsParameters] Ctx c, CancellationToken ct)
    {
        var (f, level) = await LoadFound(id, c, ct);
        if (f is null) return Results.NotFound();
        if (level < AccessLevel.Edit || f.Status != FoundStatus.InStorage) return Bad("Photos can only be added while the item is in storage.");
        if (!http.HasFormContentType) return Bad("No photo was sent.");
        var file = (await http.ReadFormAsync(ct)).Files.FirstOrDefault();
        if (file is null || file.Length == 0) return Bad("No photo was sent.");
        if (FileStore.ContentTypeFor(file.FileName) is not { } type || !type.StartsWith("image/")) return Bad("The photo must be a picture (JPG, PNG, HEIC).");
        f.PhotoStoredName = await files.SaveAsync(Path.Combine("lostfound", f.Id.ToString()), file, ct);
        f.UpdatedUtc = DateTime.UtcNow;
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync("AddPhoto", M, "FoundItem", f.Number, null, ct);
        return Results.Ok();
    }

    static async Task<IResult> Photo(int id, FileStore files, [AsParameters] Ctx c, HttpResponse response, CancellationToken ct)
    {
        var (f, _) = await LoadFound(id, c, ct);
        if (f?.PhotoStoredName is null) return Results.NotFound();
        var path = files.PathFor(Path.Combine("lostfound", f.Id.ToString()), f.PhotoStoredName);
        if (!File.Exists(path)) return Results.NotFound();
        response.Headers.CacheControl = "private, no-store";
        return Results.File(path, FileStore.ContentTypeFor(f.PhotoStoredName) ?? "image/jpeg");
    }

    static async Task<IResult> ReturnSignature(int id, [AsParameters] Ctx c, CancellationToken ct)
    {
        var (f, _) = await LoadFound(id, c, ct);
        if (f?.ReturnSignature is null) return Results.NotFound();
        await c.Log.LogAsync("ViewSignature", M, "FoundItem", f.Number, null, ct);
        return Results.File(f.ReturnSignature, "image/png");
    }

    // Handed back in person: the owner signs on the screen.
    static async Task<IResult> Return(int id, ReturnRequest req, [AsParameters] Ctx c, CancellationToken ct)
    {
        var (f, level) = await LoadFound(id, c, ct);
        if (f is null) return Results.NotFound();
        if (level < AccessLevel.Edit || f.Status != FoundStatus.InStorage) return Bad("This item is not in storage.");
        if (await c.Db.Shipments.AnyAsync(s => s.FoundItemId == f.Id && s.CancelledUtc == null && s.ShippedLocal == null, ct))
            return Bad("A shipment is open for this item. Cancel it first.");
        var name = Clean(req.Name, 200);
        if (name is null) return Bad("Who collected it?");
        var (sig, sigError) = KeyEndpoints.ParseSignature(req.Signature);
        if (sigError is not null) return Bad(sigError);
        if (sig is null) return Bad("The person collecting it signs on the screen.");
        f.Status = FoundStatus.ReturnedToOwner;
        f.ReturnedToName = name;
        f.ReturnSignature = sig;
        f.CloseNote = Clean(req.Note, 1000);
        await Close(f, c, ct);
        if (req.LostReportId is int lid) await LinkLost(lid, f, c, ct);
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync("ReturnFound", M, "FoundItem", f.Number, new { returnedTo = name }, ct);
        return Results.Ok();
    }

    static async Task<IResult> Dispose(int id, DisposeRequest req, [AsParameters] Ctx c, CancellationToken ct)
    {
        var (f, level) = await LoadFound(id, c, ct);
        if (f is null) return Results.NotFound();
        if (!LostFoundRules.CanDispose(f, level, DateOnly.FromDateTime(PacificNow())))
            return Bad(f.Status == FoundStatus.InStorage ? $"Keep it until its purge date ({f.PurgeDate:MM/dd/yyyy})." : "This item is not in storage.");
        if (req.Method is null || !LostFoundRules.DisposalMethods.Contains(req.Method)) return Bad("Choose how it was disposed of.");
        if (await c.Db.Shipments.AnyAsync(s => s.FoundItemId == f.Id && s.CancelledUtc == null && s.ShippedLocal == null, ct))
            return Bad("A shipment is open for this item. Cancel it first.");
        f.Status = FoundStatus.Disposed;
        f.DisposalMethod = req.Method;
        f.CloseNote = Clean(req.Note, 1000);
        await Close(f, c, ct);
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync("DisposeFound", M, "FoundItem", f.Number, new { req.Method }, ct);
        return Results.Ok();
    }

    static async Task<IResult> FlagFound(int id, FlagRequest req, Ctx c, CancellationToken ct)
    {
        var (f, level) = await LoadFound(id, c, ct);
        if (f is null) return Results.NotFound();
        if (level < AccessLevel.Edit) return Results.StatusCode(StatusCodes.Status403Forbidden);
        f.ForHandover = req.Flag;
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync(req.Flag ? "FlagHandover" : "UnflagHandover", M, "FoundItem", f.Number, null, ct);
        return Results.Ok();
    }

    // --- Shipping ----------------------------------------------------------------------

    static async Task<IResult> CreateShipment(int id, ShipmentRequest req, [AsParameters] Ctx c, CancellationToken ct)
    {
        var (f, level) = await LoadFound(id, c, ct);
        if (f is null) return Results.NotFound();
        if (level < AccessLevel.Edit || f.Status != FoundStatus.InStorage) return Bad("This item is not in storage.");
        if (await c.Db.Shipments.AnyAsync(s => s.FoundItemId == f.Id && s.CancelledUtc == null, ct)) return Bad("This item already has a shipment.");
        if (CheckShipment(req) is { } error) return Bad(error);
        var me = await c.Me.GetUserAsync(ct);
        if (me is null) return Bad("Your account is not set up yet.");
        var s = new Shipment { FoundItemId = f.Id, HotelId = f.HotelId, RecipientName = Clean(req.RecipientName, 200)!, CreatedById = me.Id, CreatedUtc = DateTime.UtcNow };
        ApplyShipment(s, req);
        c.Db.Shipments.Add(s);
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync("CreateShipment", M, "FoundItem", f.Number, new { shipment = s.Id, payment = s.Payment.ToString() }, ct);
        return Results.Ok(new { s.Id });
    }

    static async Task<IResult> Shipments(int hotelId, bool? open, [AsParameters] Ctx c, CancellationToken ct)
    {
        if (await c.Me.LevelAtAsync(M, hotelId, ct) < AccessLevel.View) return Results.NotFound();
        var q = c.Db.Shipments.Where(s => s.HotelId == hotelId && s.CancelledUtc == null);
        if (open != false) q = q.Where(s => s.ShippedLocal == null);
        var rows = await ShipmentRows(c.Db, q.OrderByDescending(s => s.Id).Take(300), ct);
        await c.Log.LogAsync("View", M, "ShipmentList", hotelId.ToString(), null, ct);
        return Results.Ok(rows);
    }

    static async Task<IResult> UpdateShipment(int id, ShipmentRequest req, [AsParameters] Ctx c, CancellationToken ct)
    {
        var s = await LoadShipment(id, c, ct);
        if (s is null) return Results.NotFound();
        if (s.ShippedLocal is not null || s.CancelledUtc is not null) return Bad("This shipment is closed.");
        if (CheckShipment(req) is { } error) return Bad(error);
        var before = s.Payment;
        ApplyShipment(s, req);
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync("EditShipment", M, "FoundItem", s.FoundItem.Number, new { shipment = s.Id, payment = $"{before} -> {s.Payment}" }, ct);
        return Results.Ok();
    }

    // Only a paid or no-charge shipment can go out.
    static async Task<IResult> MarkShipped(int id, ShippedRequest req, [AsParameters] Ctx c, CancellationToken ct)
    {
        var s = await LoadShipment(id, c, ct);
        if (s is null) return Results.NotFound();
        if (s.ShippedLocal is not null || s.CancelledUtc is not null) return Bad("This shipment is closed.");
        if (s.Payment == PaymentStatus.Waiting) return Bad("Payment is still waiting. Mark it Paid or No charge first.");
        s.Carrier = Clean(req.Carrier, 40) ?? s.Carrier;
        s.TrackingNumber = Clean(req.TrackingNumber, 60) ?? s.TrackingNumber;
        if (s.TrackingNumber is null) return Bad("Enter the tracking number.");
        s.ShippedLocal = Minute(req.ShippedLocal ?? PacificNow());
        s.UpdatedUtc = DateTime.UtcNow;
        var f = s.FoundItem;
        f.Status = FoundStatus.Shipped;
        f.CloseNote = $"Shipped by {s.Carrier} {s.TrackingNumber}";
        await Close(f, c, ct);
        var lost = await c.Db.LostReports.FirstOrDefaultAsync(l => l.FoundItemId == f.Id && l.Status == LostStatus.Matched, ct);
        if (lost is not null) { lost.Status = LostStatus.Closed; lost.UpdatedUtc = DateTime.UtcNow; }
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync("Shipped", M, "FoundItem", f.Number, new { shipment = s.Id, s.Carrier }, ct);
        return Results.Ok();
    }

    static async Task<IResult> CancelShipment(int id, [AsParameters] Ctx c, CancellationToken ct)
    {
        var s = await LoadShipment(id, c, ct);
        if (s is null) return Results.NotFound();
        if (s.ShippedLocal is not null || s.CancelledUtc is not null) return Bad("This shipment is closed.");
        s.CancelledUtc = DateTime.UtcNow;
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync("CancelShipment", M, "FoundItem", s.FoundItem.Number, new { shipment = s.Id }, ct);
        return Results.Ok();
    }

    static string? CheckShipment(ShipmentRequest r)
    {
        if (Clean(r.RecipientName, 200) is null) return "Who is it going to?";
        if (!Enum.IsDefined(r.Payment)) return "Choose the payment status.";
        if (r.Cost is < 0 or > 10_000) return "Check the shipping cost.";
        if (r.Carrier is { } cr && !LostFoundRules.Carriers.Contains(cr)) return "Choose the carrier.";
        // Tracking numbers can be 20+ digits (USPS), so only the payment note is checked.
        if (LostFoundRules.LooksLikeCardNumber(r.PaymentNote))
            return "That looks like a card number. Card numbers are never stored here.";
        return null;
    }

    static void ApplyShipment(Shipment s, ShipmentRequest r)
    {
        s.RecipientName = Clean(r.RecipientName, 200)!; s.Street = Clean(r.Street, 200); s.City = Clean(r.City, 100); s.State = Clean(r.State, 60);
        s.Zip = Clean(r.Zip, 20); s.Country = Clean(r.Country, 60); s.Phone = Clean(r.Phone, 40); s.Email = Clean(r.Email, 200);
        s.Carrier = r.Carrier; s.TrackingNumber = Clean(r.TrackingNumber, 60); s.Cost = r.Cost; s.Payment = r.Payment;
        s.PaymentNote = Clean(r.PaymentNote, 400); s.UpdatedUtc = DateTime.UtcNow;
    }

    static Task<List<ShipmentDto>> ShipmentRows(DeskDbContext db, IQueryable<Shipment> q, CancellationToken ct) =>
        q.Select(s => new ShipmentDto(s.Id, s.FoundItemId, s.FoundItem.Number,
            s.FoundItem.Detail ?? db.ListItems.Where(i => i.Id == s.FoundItem.CategoryId).Select(i => i.Name).FirstOrDefault(),
            s.RecipientName, s.Street, s.City, s.State, s.Zip, s.Country, s.Phone, s.Email, s.Carrier, s.TrackingNumber, s.Cost, s.Payment,
            s.PaymentNote, s.ShippedLocal, s.CancelledUtc, db.Users.Where(u => u.Id == s.CreatedById).Select(u => u.DisplayName).First(), s.CreatedUtc))
            .ToListAsync(ct);

    // --- Lost reports ------------------------------------------------------------------

    static async Task<IResult> LostList(int hotelId, string? view, string? q, [AsParameters] Ctx c, CancellationToken ct)
    {
        if (await c.Me.LevelAtAsync(M, hotelId, ct) < AccessLevel.View) return Results.NotFound();
        var query = c.Db.LostReports.Where(l => l.HotelId == hotelId);
        if (view != "all") query = query.Where(l => l.Status == LostStatus.Open);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var t = q.Trim();
            query = query.Where(l => l.Number.Contains(t) || l.OwnerName.Contains(t) || l.Description!.Contains(t) || l.Room == t || l.Email!.Contains(t));
        }
        var rows = await LostRows(c.Db, query.OrderByDescending(l => l.ReportedLocal).Take(500), ct);
        await c.Log.LogAsync(string.IsNullOrWhiteSpace(q) ? "View" : "Search", M, "LostList", hotelId.ToString(), new { view, q, count = rows.Count }, ct);
        return Results.Ok(rows);
    }

    public static Task<List<LostRow>> LostRows(DeskDbContext db, IQueryable<LostReport> q, CancellationToken ct) =>
        q.Select(l => new LostRow(l.Id, l.Number, l.HotelId, l.ReportedLocal, l.OwnerName, l.Room,
            db.ListItems.Where(i => i.Id == l.CategoryId).Select(i => i.Name).FirstOrDefault(),
            db.ListItems.Where(i => i.Id == l.ColorId).Select(i => i.Name).FirstOrDefault(), l.Brand, l.Description, l.Status, l.FoundItemId,
            l.ForHandover)).ToListAsync(ct);

    static async Task<IResult> GetLost(int id, [AsParameters] Ctx c, CancellationToken ct)
    {
        var (l, _) = await LoadLost(id, c, ct);
        if (l is null) return Results.NotFound();
        var row = (await LostRows(c.Db, c.Db.LostReports.Where(x => x.Id == l.Id), ct)).Single();
        var matches = l.Status == LostStatus.Open
            ? (await c.Db.FoundItems.Where(f => f.HotelId == l.HotelId && f.Status == FoundStatus.InStorage).ToListAsync(ct))
                .Select(f => (f, s: LostFoundRules.MatchScore(l, f))).Where(x => x.s > 0).OrderByDescending(x => x.s).Take(10)
                .Select(x => new Match(x.f.Id, x.f.Number, x.s, $"{x.f.Detail} {x.f.Brand} {x.f.Description}".Trim(), x.f.FoundLocal)).ToList()
            : [];
        var by = await c.Db.Users.Where(u => u.Id == l.CreatedById).Select(u => u.DisplayName).FirstAsync(ct);
        await c.Log.LogAsync("View", M, "LostReport", l.Number, null, ct);
        return Results.Ok(new LostDto(row, l.Phone, l.Email, l.ArrivalDate, l.DepartureDate, l.CategoryId, l.ColorId, l.LostWhere, l.LostFrom,
            l.LostTo, l.Notes, by, matches));
    }

    static async Task<IResult> SaveLost(int? id, LostRequest req, Ctx c, CancellationToken ct)
    {
        if (await c.Me.LevelAtAsync(M, req.HotelId, ct) < AccessLevel.Edit) return Results.StatusCode(StatusCodes.Status403Forbidden);
        var owner = Clean(req.OwnerName, 200);
        if (owner is null) return Bad("Whose item is it?");
        if (await CheckLists(c.Db, req.CategoryId, req.ColorId, ct) is { } e1) return Bad(e1);
        if (req.LostFrom is { } lf && req.LostTo is { } lt && lt < lf) return Bad("Check the dates it was lost.");
        if (new[] { req.Description, req.Notes }.Any(LostFoundRules.LooksLikeCardNumber)) return Bad("That looks like a card number. Card numbers are never stored here.");
        var me = await c.Me.GetUserAsync(ct);
        if (me is null) return Bad("Your account is not set up yet.");
        var l = id is int i ? (await LoadLost(i, c, ct)).Item1 : null;
        if (id is not null && l is null) return Results.NotFound();
        if (l is not null && (l.Status != LostStatus.Open || l.HotelId != req.HotelId)) return Bad("This report is closed, or belongs to another hotel.");
        l ??= c.Db.LostReports.Add(new LostReport { Number = "", HotelId = req.HotelId, OwnerName = owner, ReportedLocal = Minute(PacificNow()), CreatedById = me.Id, CreatedUtc = DateTime.UtcNow }).Entity;
        l.OwnerName = owner; l.Phone = Clean(req.Phone, 40); l.Email = Clean(req.Email, 200); l.Room = Clean(req.Room, 40);
        l.ArrivalDate = req.ArrivalDate; l.DepartureDate = req.DepartureDate; l.CategoryId = req.CategoryId; l.Description = Clean(req.Description, 2000);
        l.ColorId = req.ColorId; l.Brand = Clean(req.Brand, 100); l.LostWhere = Clean(req.LostWhere, 200); l.LostFrom = req.LostFrom; l.LostTo = req.LostTo;
        l.Notes = Clean(req.Notes, 2000); l.UpdatedUtc = DateTime.UtcNow;
        if (id is null) await SaveNumbered(c.Db, l, "L", ct); else await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync(id is null ? "LogLost" : "EditLost", M, "LostReport", l.Number, null, ct);
        return Results.Ok(new { l.Id, l.Number });
    }

    static async Task<IResult> MatchLost(int id, MatchRequest req, [AsParameters] Ctx c, CancellationToken ct)
    {
        var (l, level) = await LoadLost(id, c, ct);
        if (l is null) return Results.NotFound();
        if (l.Status != LostStatus.Open) return Bad("This report is already closed.");
        var f = await c.Db.FoundItems.FirstOrDefaultAsync(x => x.Id == req.FoundItemId && x.HotelId == l.HotelId && x.Status == FoundStatus.InStorage, ct);
        if (f is null) return Bad("That item is not in storage at this hotel.");
        l.Status = LostStatus.Matched;
        l.FoundItemId = f.Id;
        l.UpdatedUtc = DateTime.UtcNow;
        f.OwnerName ??= l.OwnerName;
        f.OwnerContact ??= string.Join(", ", new[] { l.Phone, l.Email }.Where(x => !string.IsNullOrEmpty(x)));
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync("Match", M, "LostReport", l.Number, new { found = f.Number }, ct);
        return Results.Ok();
    }

    static async Task<IResult> CloseLost(int id, NoteRequest req, [AsParameters] Ctx c, CancellationToken ct)
    {
        var (l, _) = await LoadLost(id, c, ct);
        if (l is null) return Results.NotFound();
        if (l.Status == LostStatus.Closed) return Bad("Already closed.");
        var note = Clean(req.Note, 1000);
        if (note is null) return Bad("Say why it is closed (e.g. not found, guest found it).");
        l.Status = LostStatus.Closed;
        l.Notes = string.IsNullOrEmpty(l.Notes) ? $"Closed: {note}" : $"{l.Notes}\nClosed: {note}";
        l.UpdatedUtc = DateTime.UtcNow;
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync("CloseLost", M, "LostReport", l.Number, null, ct);
        return Results.Ok();
    }

    static async Task<IResult> FlagLost(int id, FlagRequest req, Ctx c, CancellationToken ct)
    {
        var (l, level) = await LoadLost(id, c, ct);
        if (l is null) return Results.NotFound();
        if (level < AccessLevel.Edit) return Results.StatusCode(StatusCodes.Status403Forbidden);
        l.ForHandover = req.Flag;
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync(req.Flag ? "FlagHandover" : "UnflagHandover", M, "LostReport", l.Number, null, ct);
        return Results.Ok();
    }

    // --- Helpers -----------------------------------------------------------------------

    static async Task<(FoundItem?, AccessLevel)> LoadFound(int id, Ctx c, CancellationToken ct)
    {
        var f = await c.Db.FoundItems.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (f is null) return (null, AccessLevel.None);
        var level = await c.Me.LevelAtAsync(M, f.HotelId, ct);
        return level >= AccessLevel.View ? (f, level) : (null, AccessLevel.None);
    }

    static async Task<(LostReport?, AccessLevel)> LoadLost(int id, Ctx c, CancellationToken ct)
    {
        var l = await c.Db.LostReports.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (l is null) return (null, AccessLevel.None);
        var level = await c.Me.LevelAtAsync(M, l.HotelId, ct);
        return level >= AccessLevel.View ? (l, level) : (null, AccessLevel.None);
    }

    static async Task<Shipment?> LoadShipment(int id, Ctx c, CancellationToken ct)
    {
        var s = await c.Db.Shipments.Include(x => x.FoundItem).FirstOrDefaultAsync(x => x.Id == id, ct);
        return s is not null && await c.Me.LevelAtAsync(M, s.HotelId, ct) >= AccessLevel.Edit ? s : null;
    }

    static async Task Close(FoundItem f, Ctx c, CancellationToken ct)
    {
        f.ClosedLocal = Minute(PacificNow());
        f.ClosedById = (await c.Me.GetUserAsync(ct))?.Id;
        f.ForHandover = false;
        f.UpdatedUtc = DateTime.UtcNow;
    }

    static async Task LinkLost(int lostId, FoundItem f, Ctx c, CancellationToken ct)
    {
        var l = await c.Db.LostReports.FirstOrDefaultAsync(x => x.Id == lostId && x.HotelId == f.HotelId && x.Status != LostStatus.Closed, ct);
        if (l is null) return;
        l.Status = LostStatus.Closed;
        l.FoundItemId = f.Id;
        l.UpdatedUtc = DateTime.UtcNow;
    }

    static async Task<string?> CheckLists(DeskDbContext db, int? categoryId, int? colorId, CancellationToken ct)
    {
        if (categoryId is int cat && !await db.ListItems.AnyAsync(i => i.Id == cat && i.ListKey == ListKeys.PropertyCategory, ct)) return "Choose a category from the list.";
        if (colorId is int col && !await db.ListItems.AnyAsync(i => i.Id == col && i.ListKey == ListKeys.Color, ct)) return "Choose a color from the list.";
        return null;
    }

    // F2026-0001 / L2026-0001, per Pacific year; a clash with a number saved at the same
    // moment is refused by the unique index and the next number is taken.
    static async Task SaveNumbered(DeskDbContext db, object entity, string letter, CancellationToken ct)
    {
        var prefix = $"{letter}{PacificNow().Year}-";
        for (var attempt = 1; ; attempt++)
        {
            var last = letter == "F"
                ? await db.FoundItems.Where(x => x.Number.StartsWith(prefix)).OrderByDescending(x => x.Id).Select(x => x.Number).FirstOrDefaultAsync(ct)
                : await db.LostReports.Where(x => x.Number.StartsWith(prefix)).OrderByDescending(x => x.Id).Select(x => x.Number).FirstOrDefaultAsync(ct);
            var n = last is not null && int.TryParse(last[prefix.Length..], out var x2) ? x2 + 1 : 1;
            var number = $"{prefix}{n:D4}";
            if (entity is FoundItem f) f.Number = number; else ((LostReport)entity).Number = number;
            try { await db.SaveChangesAsync(ct); return; }
            catch (DbUpdateException) when (attempt < 5) { }
        }
    }

    static DateTime Minute(DateTime t) => new(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0);
}
