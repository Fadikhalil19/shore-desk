using Desk.Api.Auth;
using Desk.Api.Services;
using Desk.Core.Access;
using Desk.Core.Data;
using Desk.Core.Incidents;
using Desk.Core.Keys;
using Microsoft.EntityFrameworkCore;
using static Desk.Api.Endpoints.Shared;

namespace Desk.Api.Endpoints;

// Keys: the key list per hotel, sign out / sign in with finger signatures, missing /
// broken, shift key counts, history. Module "keys": View sees, Edit signs keys out and
// in, reports missing/broken and counts; Manage keeps the key list and puts keys back
// in service. The person's hotel limit applies to every key.
public static class KeyEndpoints
{
    public record KeyRequest(int HotelId, string? Name, string? Barcode, string? Description, int SortOrder, bool IsActive = true);
    public record SignOutRequest(string? ToType, int? UserId, string? Name, string? Company, int? DepartmentId,
        DateTime? OutLocal, DateTime? DueBackLocal, string? Notes, string? Signature);
    public record SignInRequest(DateTime? InLocal, string? Notes, string? Signature);
    public record StatusRequest(KeyStatus Status, string? Note);
    public record CountRequest(int HotelId, DateOnly ShiftDate, Shift Shift, string? Notes, List<CountLineRequest> Lines);
    public record CountLineRequest(int KeyId, bool OnHand, string? Note);

    public record HolderDto(int CheckoutId, string ToType, string Name, string? Company, int? DepartmentId, DateTime OutLocal,
        DateTime? DueBackLocal, string OutBy, bool Overdue, double HoursOut);
    public record KeyDto(int Id, int HotelId, string Name, string? Barcode, string? Description, KeyStatus Status, bool IsActive,
        int SortOrder, HolderDto? Holder);
    public record HistoryRow(string Kind, int KeyId, string KeyName, DateTime AtLocal, string? Who, string? Company, string By,
        string? Note, int? CheckoutId, bool HasSignature, DateTime? BackLocal, string? BackBy, bool HasBackSignature);
    public record CountDto(int Id, int HotelId, DateOnly ShiftDate, Shift Shift, string CountedBy, DateTime CountedUtc, string? Notes,
        int Keys, int OnHand, List<CountLineDto>? Lines);
    public record CountLineDto(int KeyId, string KeyName, KeyStatus ExpectedStatus, bool OnHand, string? Note);

    public static void MapKeyEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/keys").RequireModule(Modules.Keys, AccessLevel.View);
        g.MapGet("", List);
        g.MapPost("", (KeyRequest r, [AsParameters] Ctx c, CancellationToken ct) => SaveKey(null, r, c, ct)).RequireModule(Modules.Keys, AccessLevel.Manage);
        g.MapPut("/{id:int}", (int id, KeyRequest r, [AsParameters] Ctx c, CancellationToken ct) => SaveKey(id, r, c, ct)).RequireModule(Modules.Keys, AccessLevel.Manage);
        g.MapGet("/lookup", Lookup);
        g.MapPost("/{id:int}/signout", SignOut).RequireModule(Modules.Keys, AccessLevel.Edit);
        g.MapPost("/{id:int}/signin", SignIn).RequireModule(Modules.Keys, AccessLevel.Edit);
        g.MapPost("/{id:int}/status", ChangeStatus).RequireModule(Modules.Keys, AccessLevel.Edit);
        g.MapGet("/history", History);
        g.MapGet("/checkouts/{id:int}/signature/{which}", Signature);
        g.MapGet("/counts", Counts);
        g.MapGet("/counts/{id:int}", Count);
        g.MapPost("/counts", SaveCount).RequireModule(Modules.Keys, AccessLevel.Edit);
    }

    public record Ctx(DeskDbContext Db, CurrentUser Me, ActivityLogger Log);

    // --- Key list ----------------------------------------------------------------

    static async Task<IResult> List(int hotelId, bool? all, [AsParameters] Ctx c, CancellationToken ct)
    {
        if (await c.Me.LevelAtAsync(Modules.Keys, hotelId, ct) < AccessLevel.View) return Results.NotFound();
        var keys = await Keys(c.Db, hotelId, all == true, ct);
        await c.Log.LogAsync("View", Modules.Keys, "KeyList", hotelId.ToString(), null, ct);
        return Results.Ok(keys);
    }

    // Also used by the handover page ("keys still out").
    public static async Task<List<KeyDto>> Keys(DeskDbContext db, int hotelId, bool includeInactive, CancellationToken ct)
    {
        var now = PacificNow();
        var rows = await db.Keys.Where(k => k.HotelId == hotelId && (includeInactive || k.IsActive))
            .OrderBy(k => k.SortOrder).ThenBy(k => k.Name)
            .Select(k => new
            {
                Key = k,
                Out = db.KeyCheckouts.Where(o => o.Id == k.CurrentCheckoutId)
                    .Select(o => new { o.Id, o.ToType, o.Name, o.Company, o.DepartmentId, o.OutLocal, o.DueBackLocal,
                        By = db.Users.Where(u => u.Id == o.OutById).Select(u => u.DisplayName).First() })
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);
        return rows.Select(r => new KeyDto(r.Key.Id, r.Key.HotelId, r.Key.Name, r.Key.Barcode, r.Key.Description, r.Key.Status,
            r.Key.IsActive, r.Key.SortOrder,
            r.Out is null ? null : new HolderDto(r.Out.Id, r.Out.ToType, r.Out.Name, r.Out.Company, r.Out.DepartmentId, r.Out.OutLocal,
                r.Out.DueBackLocal, r.Out.By, KeyRules.Overdue(r.Out.OutLocal, r.Out.DueBackLocal, now),
                Math.Round((now - r.Out.OutLocal).TotalHours, 1)))).ToList();
    }

    static async Task<IResult> SaveKey(int? id, KeyRequest req, Ctx c, CancellationToken ct)
    {
        if (await c.Me.LevelAtAsync(Modules.Keys, req.HotelId, ct) < AccessLevel.Manage) return Results.StatusCode(StatusCodes.Status403Forbidden);
        var name = Clean(req.Name, 100);
        if (name is null) return Bad("A key name is required.");
        var barcode = Clean(req.Barcode, 100);
        if (await c.Db.Keys.AnyAsync(k => k.HotelId == req.HotelId && k.Name == name && k.Id != id, ct)) return Bad($"There is already a key called {name}.");
        if (barcode is not null && await c.Db.Keys.AnyAsync(k => k.HotelId == req.HotelId && k.Barcode == barcode && k.Id != id, ct))
            return Bad("Another key already has that barcode.");
        var k = id is int i ? await c.Db.Keys.FirstOrDefaultAsync(x => x.Id == i, ct) : c.Db.Keys.Add(new HotelKey { Name = name, HotelId = req.HotelId }).Entity;
        if (k is null || k.HotelId != req.HotelId) return Results.NotFound();
        if (!req.IsActive && k.Status == KeyStatus.SignedOut) return Bad("Sign the key in before taking it out of service.");
        k.Name = name; k.Barcode = barcode; k.Description = Clean(req.Description, 400); k.SortOrder = req.SortOrder; k.IsActive = req.IsActive;
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync(id is null ? "AddKey" : "EditKey", Modules.Keys, "Key", k.Id.ToString(), new { k.Name, k.Barcode, k.IsActive }, ct);
        return Results.Ok(new { k.Id });
    }

    // A scanner (or a person) types a barcode or a key name.
    static async Task<IResult> Lookup(int hotelId, string code, [AsParameters] Ctx c, CancellationToken ct)
    {
        if (await c.Me.LevelAtAsync(Modules.Keys, hotelId, ct) < AccessLevel.View) return Results.NotFound();
        var text = code.Trim();
        var id = await c.Db.Keys.Where(k => k.HotelId == hotelId && k.IsActive && (k.Barcode == text || k.Name == text))
            .Select(k => (int?)k.Id).FirstOrDefaultAsync(ct);
        return id is int found ? Results.Ok(new { id = found }) : Results.NotFound();
    }

    // --- Sign out / in -----------------------------------------------------------

    static async Task<IResult> SignOut(int id, SignOutRequest req, [AsParameters] Ctx c, CancellationToken ct)
    {
        var (k, level) = await Load(id, c, ct);
        if (k is null) return Results.NotFound();
        if (level < AccessLevel.Edit) return Results.StatusCode(StatusCodes.Status403Forbidden);
        if (!k.IsActive) return Bad("This key is out of service.");
        if (k.Status != KeyStatus.In) return Bad(k.Status == KeyStatus.SignedOut ? "This key is already signed out." : $"This key is marked {k.Status}.");
        if (req.ToType is null || !KeyRules.HolderTypes.Contains(req.ToType)) return Bad("Choose who it is signed out to (employee, contractor, ...).");
        string? name = Clean(req.Name, 200);
        if (req.UserId is int uid)
        {
            name = await c.Db.Users.Where(u => u.Id == uid).Select(u => u.DisplayName).FirstOrDefaultAsync(ct);
            if (name is null) return Bad("That person is not in Shore Desk.");
        }
        if (name is null) return Bad("Choose the person or type their name.");
        if (req.DepartmentId is int d && !await c.Db.Departments.AnyAsync(x => x.Id == d, ct)) return Bad("Unknown department.");
        var (sig, sigError) = ParseSignature(req.Signature);
        if (sigError is not null) return Bad(sigError);
        if (sig is null) return Bad("The person taking the key signs on the screen.");
        var now = PacificNow();
        var outAt = req.OutLocal ?? now;
        if (outAt > now.AddMinutes(10)) return Bad("The time is in the future.");
        if (req.DueBackLocal is { } due && due <= outAt) return Bad("The due back time must be after the sign-out time.");
        var me = await c.Me.GetUserAsync(ct);
        if (me is null) return Bad("Your account is not set up yet.");

        var o = new KeyCheckout
        {
            KeyId = k.Id, HotelId = k.HotelId, ToType = req.ToType, UserId = req.UserId, Name = name, Company = Clean(req.Company, 200),
            DepartmentId = req.DepartmentId, OutLocal = Minute(outAt), DueBackLocal = req.DueBackLocal, OutById = me.Id,
            OutSignature = sig, OutNotes = Clean(req.Notes, 1000), CreatedUtc = DateTime.UtcNow,
        };
        c.Db.KeyCheckouts.Add(o);
        await c.Db.SaveChangesAsync(ct);
        k.Status = KeyStatus.SignedOut;
        k.CurrentCheckoutId = o.Id;
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync("SignOutKey", Modules.Keys, "Key", k.Id.ToString(), new { checkout = o.Id, o.Name, o.ToType }, ct);
        return Results.Ok(new { o.Id });
    }

    static async Task<IResult> SignIn(int id, SignInRequest req, [AsParameters] Ctx c, CancellationToken ct)
    {
        var (k, level) = await Load(id, c, ct);
        if (k is null) return Results.NotFound();
        if (level < AccessLevel.Edit) return Results.StatusCode(StatusCodes.Status403Forbidden);
        if (k.Status != KeyStatus.SignedOut || k.CurrentCheckoutId is null) return Bad("This key is not signed out.");
        var (sig, sigError) = ParseSignature(req.Signature);
        if (sigError is not null) return Bad(sigError);
        var o = await c.Db.KeyCheckouts.FirstAsync(x => x.Id == k.CurrentCheckoutId, ct);
        var inAt = req.InLocal ?? PacificNow();
        if (inAt < o.OutLocal) return Bad("It cannot come back before it went out.");
        if (inAt > PacificNow().AddMinutes(10)) return Bad("The time is in the future.");
        var me = await c.Me.GetUserAsync(ct);

        o.InLocal = Minute(inAt);
        o.InById = me?.Id;
        o.InSignature = sig;
        o.InNotes = Clean(req.Notes, 1000);
        k.Status = KeyStatus.In;
        k.CurrentCheckoutId = null;
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync("SignInKey", Modules.Keys, "Key", k.Id.ToString(), new { checkout = o.Id }, ct);
        return Results.Ok();
    }

    static async Task<IResult> ChangeStatus(int id, StatusRequest req, [AsParameters] Ctx c, CancellationToken ct)
    {
        var (k, level) = await Load(id, c, ct);
        if (k is null) return Results.NotFound();
        if (!Enum.IsDefined(req.Status)) return Bad("Unknown status.");
        if (KeyRules.CheckStatusChange(k.Status, req.Status, level) is { } error) return Bad(error);
        var note = Clean(req.Note, 1000);
        if (note is null) return Bad("Say what happened.");
        var me = await c.Me.GetUserAsync(ct);
        if (me is null) return Bad("Your account is not set up yet.");

        // A key reported missing while signed out closes that sign-out (it never came back).
        if (k.CurrentCheckoutId is int oid)
        {
            var o = await c.Db.KeyCheckouts.FirstAsync(x => x.Id == oid, ct);
            o.InNotes = $"Not returned: marked {req.Status}. {note}";
            k.CurrentCheckoutId = null;
        }
        c.Db.KeyStatusChanges.Add(new KeyStatusChange { KeyId = k.Id, HotelId = k.HotelId, From = k.Status, To = req.Status, Note = note,
            ById = me.Id, AtLocal = Minute(PacificNow()) });
        var from = k.Status;
        k.Status = req.Status;
        await c.Db.SaveChangesAsync(ct);
        await c.Log.LogAsync("KeyStatus", Modules.Keys, "Key", k.Id.ToString(), new { from = from.ToString(), to = req.Status.ToString(), note }, ct);
        return Results.Ok();
    }

    // --- History -----------------------------------------------------------------

    static async Task<IResult> History(int hotelId, int? keyId, string? q, DateOnly? from, DateOnly? to, [AsParameters] Ctx c, CancellationToken ct)
    {
        if (await c.Me.LevelAtAsync(Modules.Keys, hotelId, ct) < AccessLevel.View) return Results.NotFound();
        var f = (from ?? DateOnly.FromDateTime(PacificNow()).AddDays(-7)).ToDateTime(TimeOnly.MinValue);
        var t = (to ?? DateOnly.FromDateTime(PacificNow())).AddDays(1).ToDateTime(TimeOnly.MinValue);
        var text = q?.Trim();

        var outs = c.Db.KeyCheckouts.Where(o => o.HotelId == hotelId && o.OutLocal >= f && o.OutLocal < t);
        if (keyId is int kid) outs = outs.Where(o => o.KeyId == kid);
        if (!string.IsNullOrEmpty(text)) outs = outs.Where(o => o.Name.Contains(text) || o.Company!.Contains(text) || o.Key.Name.Contains(text));
        var rows = await outs.OrderByDescending(o => o.OutLocal).Take(500)
            .Select(o => new HistoryRow("Signed out", o.KeyId, o.Key.Name, o.OutLocal, o.Name + " (" + o.ToType + ")", o.Company,
                c.Db.Users.Where(u => u.Id == o.OutById).Select(u => u.DisplayName).First(),
                o.InNotes != null && o.InLocal == null ? o.InNotes : o.OutNotes, o.Id, o.OutSignature != null,
                o.InLocal, c.Db.Users.Where(u => u.Id == o.InById).Select(u => u.DisplayName).FirstOrDefault(), o.InSignature != null))
            .ToListAsync(ct);

        var changes = c.Db.KeyStatusChanges.Where(s => s.HotelId == hotelId && s.AtLocal >= f && s.AtLocal < t);
        if (keyId is int kid2) changes = changes.Where(s => s.KeyId == kid2);
        if (!string.IsNullOrEmpty(text)) changes = changes.Where(s => s.Note!.Contains(text) || c.Db.Keys.Any(k => k.Id == s.KeyId && k.Name.Contains(text)));
        rows.AddRange(await changes.OrderByDescending(s => s.AtLocal).Take(500)
            .Select(s => new HistoryRow("Marked " + s.To.ToString(), s.KeyId, c.Db.Keys.Where(k => k.Id == s.KeyId).Select(k => k.Name).First(),
                s.AtLocal, null, null, c.Db.Users.Where(u => u.Id == s.ById).Select(u => u.DisplayName).First(), s.Note, null, false, null, null, false))
            .ToListAsync(ct));

        await c.Log.LogAsync("Search", Modules.Keys, "KeyHistory", null, new { hotelId, keyId, q, from = f, to = t }, ct);
        return Results.Ok(rows.OrderByDescending(r => r.AtLocal).ToList());
    }

    static async Task<IResult> Signature(int id, string which, [AsParameters] Ctx c, CancellationToken ct)
    {
        var o = await c.Db.KeyCheckouts.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (o is null || await c.Me.LevelAtAsync(Modules.Keys, o.HotelId, ct) < AccessLevel.View) return Results.NotFound();
        var bytes = which == "in" ? o.InSignature : which == "out" ? o.OutSignature : null;
        if (bytes is null) return Results.NotFound();
        await c.Log.LogAsync("ViewSignature", Modules.Keys, "KeyCheckout", o.Id.ToString(), new { which }, ct);
        return Results.File(bytes, "image/png");
    }

    // --- Shift key count ---------------------------------------------------------

    static async Task<IResult> Counts(int hotelId, [AsParameters] Ctx c, CancellationToken ct)
    {
        if (await c.Me.LevelAtAsync(Modules.Keys, hotelId, ct) < AccessLevel.View) return Results.NotFound();
        var list = await c.Db.KeyCounts.Where(k => k.HotelId == hotelId).OrderByDescending(k => k.CountedUtc).Take(60)
            .Select(k => new CountDto(k.Id, k.HotelId, k.ShiftDate, k.Shift, c.Db.Users.Where(u => u.Id == k.CountedById).Select(u => u.DisplayName).First(),
                k.CountedUtc, k.Notes, k.Lines.Count, k.Lines.Count(l => l.OnHand), null))
            .ToListAsync(ct);
        return Results.Ok(list);
    }

    static async Task<IResult> Count(int id, [AsParameters] Ctx c, CancellationToken ct)
    {
        var k = await c.Db.KeyCounts.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (k is null || await c.Me.LevelAtAsync(Modules.Keys, k.HotelId, ct) < AccessLevel.View) return Results.NotFound();
        var names = await c.Db.Keys.Where(x => x.HotelId == k.HotelId).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var by = await c.Db.Users.Where(u => u.Id == k.CountedById).Select(u => u.DisplayName).FirstAsync(ct);
        await c.Log.LogAsync("View", Modules.Keys, "KeyCount", k.Id.ToString(), null, ct);
        return Results.Ok(new CountDto(k.Id, k.HotelId, k.ShiftDate, k.Shift, by, k.CountedUtc, k.Notes, k.Lines.Count, k.Lines.Count(l => l.OnHand),
            k.Lines.Select(l => new CountLineDto(l.KeyId, names.GetValueOrDefault(l.KeyId, "?"), l.ExpectedStatus, l.OnHand, l.Note)).OrderBy(l => l.KeyName).ToList()));
    }

    // Every active key of the hotel must be in the count, once.
    static async Task<IResult> SaveCount(CountRequest req, [AsParameters] Ctx c, CancellationToken ct)
    {
        if (await c.Me.LevelAtAsync(Modules.Keys, req.HotelId, ct) < AccessLevel.Edit) return Results.StatusCode(StatusCodes.Status403Forbidden);
        if (!Enum.IsDefined(req.Shift)) return Bad("Choose the shift.");
        var keys = await c.Db.Keys.Where(k => k.HotelId == req.HotelId && k.IsActive).ToDictionaryAsync(k => k.Id, ct);
        if (keys.Count == 0) return Bad("There are no keys to count yet.");
        var lines = req.Lines ?? [];
        if (lines.Select(l => l.KeyId).Distinct().Count() != lines.Count || !lines.Select(l => l.KeyId).ToHashSet().SetEquals(keys.Keys))
            return Bad("The count must list every key once. Reload the page and count again.");
        var me = await c.Me.GetUserAsync(ct);
        if (me is null) return Bad("Your account is not set up yet.");
        var count = new KeyCount
        {
            HotelId = req.HotelId, ShiftDate = req.ShiftDate, Shift = req.Shift, CountedById = me.Id, CountedUtc = DateTime.UtcNow,
            Notes = Clean(req.Notes, 1000),
            Lines = lines.Select(l => new KeyCountLine { KeyId = l.KeyId, ExpectedStatus = keys[l.KeyId].Status, OnHand = l.OnHand, Note = Clean(l.Note, 400) }).ToList(),
        };
        c.Db.KeyCounts.Add(count);
        await c.Db.SaveChangesAsync(ct);
        // Keys the app thinks are in, but that were not on hand, are the ones to chase.
        var problems = count.Lines.Count(l => l.ExpectedStatus == KeyStatus.In && !l.OnHand);
        await c.Log.LogAsync("KeyCount", Modules.Keys, "KeyCount", count.Id.ToString(), new { count.ShiftDate, count.Shift, keys = count.Lines.Count, problems }, ct);
        return Results.Ok(new { count.Id, problems });
    }

    // --- Helpers -----------------------------------------------------------------

    static async Task<(HotelKey?, AccessLevel)> Load(int id, Ctx c, CancellationToken ct)
    {
        var k = await c.Db.Keys.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (k is null) return (null, AccessLevel.None);
        var level = await c.Me.LevelAtAsync(Modules.Keys, k.HotelId, ct);
        return level >= AccessLevel.View ? (k, level) : (null, AccessLevel.None);
    }

    // Signatures come from the browser as "data:image/png;base64,...".
    public static (byte[]? Bytes, string? Error) ParseSignature(string? dataUrl)
    {
        if (string.IsNullOrWhiteSpace(dataUrl)) return (null, null);
        const string prefix = "data:image/png;base64,";
        if (!dataUrl.StartsWith(prefix, StringComparison.Ordinal)) return (null, "The signature could not be read. Sign again.");
        try
        {
            var bytes = Convert.FromBase64String(dataUrl[prefix.Length..]);
            return KeyRules.IsPng(bytes) ? (bytes, null) : (null, "The signature could not be read. Sign again.");
        }
        catch (FormatException) { return (null, "The signature could not be read. Sign again."); }
    }

    static DateTime Minute(DateTime t) => new(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0);
}
