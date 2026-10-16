using Desk.Api.Services;
using Desk.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Desk.Api.Endpoints;

// Admin-only: hotels, departments and locations (area > spot). Nothing is deleted
// here: entries are switched off so old records keep their names.
public static class ListEndpoints
{
    public record HotelRequest(string Code, string Name, string? Address, bool IsActive = true);
    public record NameRequest(string Name, bool IsActive = true);
    public record AreaRequest(int? HotelId, string Name, int SortOrder, bool IsActive = true);
    public record LocationRequest(string Name, int SortOrder, bool IsActive = true);

    public static void MapListEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/admin/lists").RequireAuthorization(Policies.Admin);

        g.MapPost("/hotels", (HotelRequest r, DeskDbContext db, ActivityLogger log, CancellationToken ct) =>
            SaveHotel(null, r, db, log, ct));
        g.MapPut("/hotels/{id:int}", (int id, HotelRequest r, DeskDbContext db, ActivityLogger log, CancellationToken ct) =>
            SaveHotel(id, r, db, log, ct));

        g.MapPost("/departments", (NameRequest r, DeskDbContext db, ActivityLogger log, CancellationToken ct) =>
            SaveDepartment(null, r, db, log, ct));
        g.MapPut("/departments/{id:int}", (int id, NameRequest r, DeskDbContext db, ActivityLogger log, CancellationToken ct) =>
            SaveDepartment(id, r, db, log, ct));

        g.MapPost("/areas", (AreaRequest r, DeskDbContext db, ActivityLogger log, CancellationToken ct) =>
            SaveArea(null, r, db, log, ct));
        g.MapPut("/areas/{id:int}", (int id, AreaRequest r, DeskDbContext db, ActivityLogger log, CancellationToken ct) =>
            SaveArea(id, r, db, log, ct));

        g.MapPost("/areas/{areaId:int}/locations", (int areaId, LocationRequest r, DeskDbContext db, ActivityLogger log, CancellationToken ct) =>
            SaveLocation(areaId, null, r, db, log, ct));
        g.MapPut("/locations/{id:int}", async (int id, LocationRequest r, DeskDbContext db, ActivityLogger log, CancellationToken ct) =>
        {
            var areaId = await db.Locations.Where(l => l.Id == id).Select(l => (int?)l.AreaId).FirstOrDefaultAsync(ct);
            return areaId is int a ? await SaveLocation(a, id, r, db, log, ct) : Results.NotFound();
        });
    }

    static IResult Bad(string message) => Results.BadRequest(new { message });

    static async Task<IResult> SaveHotel(int? id, HotelRequest r, DeskDbContext db, ActivityLogger log, CancellationToken ct)
    {
        var code = r.Code?.Trim().ToUpperInvariant() ?? "";
        var name = r.Name?.Trim() ?? "";
        if (code.Length is < 2 or > 10 || name.Length == 0) return Bad("A code (2-10 letters) and a name are required.");
        if (await db.Hotels.AnyAsync(h => h.Code == code && h.Id != id, ct)) return Bad($"Another hotel already uses the code {code}.");
        var h = id is int i ? await db.Hotels.FindAsync([i], ct) : db.Hotels.Add(new Hotel { Code = code, Name = name }).Entity;
        if (h is null) return Results.NotFound();
        h.Code = code; h.Name = name; h.Address = Clean(r.Address); h.IsActive = r.IsActive;
        await db.SaveChangesAsync(ct);
        await log.LogAsync(id is null ? "AddHotel" : "EditHotel", null, "Hotel", h.Id.ToString(), r, ct);
        return Results.Ok(new { h.Id });
    }

    static async Task<IResult> SaveDepartment(int? id, NameRequest r, DeskDbContext db, ActivityLogger log, CancellationToken ct)
    {
        var name = r.Name?.Trim() ?? "";
        if (name.Length == 0) return Bad("A name is required.");
        if (await db.Departments.AnyAsync(d => d.Name == name && d.Id != id, ct)) return Bad($"There is already a department called {name}.");
        var d = id is int i ? await db.Departments.FindAsync([i], ct) : db.Departments.Add(new Department { Name = name }).Entity;
        if (d is null) return Results.NotFound();
        d.Name = name; d.IsActive = r.IsActive;
        await db.SaveChangesAsync(ct);
        await log.LogAsync(id is null ? "AddDepartment" : "EditDepartment", null, "Department", d.Id.ToString(), r, ct);
        return Results.Ok(new { d.Id });
    }

    static async Task<IResult> SaveArea(int? id, AreaRequest r, DeskDbContext db, ActivityLogger log, CancellationToken ct)
    {
        var name = r.Name?.Trim() ?? "";
        if (name.Length == 0) return Bad("A name is required.");
        if (r.HotelId is int hid && !await db.Hotels.AnyAsync(h => h.Id == hid, ct)) return Bad("Unknown hotel.");
        if (await db.LocationAreas.AnyAsync(a => a.HotelId == r.HotelId && a.Name == name && a.Id != id, ct))
            return Bad($"There is already an area called {name} for that hotel.");
        var a = id is int i ? await db.LocationAreas.FindAsync([i], ct) : db.LocationAreas.Add(new LocationArea { Name = name }).Entity;
        if (a is null) return Results.NotFound();
        a.HotelId = r.HotelId; a.Name = name; a.SortOrder = r.SortOrder; a.IsActive = r.IsActive;
        await db.SaveChangesAsync(ct);
        await log.LogAsync(id is null ? "AddArea" : "EditArea", null, "LocationArea", a.Id.ToString(), r, ct);
        return Results.Ok(new { a.Id });
    }

    static async Task<IResult> SaveLocation(int areaId, int? id, LocationRequest r, DeskDbContext db, ActivityLogger log, CancellationToken ct)
    {
        var name = r.Name?.Trim() ?? "";
        if (name.Length == 0) return Bad("A name is required.");
        if (!await db.LocationAreas.AnyAsync(a => a.Id == areaId, ct)) return Results.NotFound();
        if (await db.Locations.AnyAsync(l => l.AreaId == areaId && l.Name == name && l.Id != id, ct))
            return Bad($"There is already a spot called {name} in that area.");
        var l = id is int i ? await db.Locations.FindAsync([i], ct) : db.Locations.Add(new Location { AreaId = areaId, Name = name }).Entity;
        if (l is null) return Results.NotFound();
        l.Name = name; l.SortOrder = r.SortOrder; l.IsActive = r.IsActive;
        await db.SaveChangesAsync(ct);
        await log.LogAsync(id is null ? "AddLocation" : "EditLocation", null, "Location", l.Id.ToString(), new { areaId, r.Name, r.SortOrder, r.IsActive }, ct);
        return Results.Ok(new { l.Id });
    }

    static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
