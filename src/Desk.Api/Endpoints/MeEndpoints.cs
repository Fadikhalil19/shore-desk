using Desk.Api.Auth;
using Desk.Core.Access;
using Desk.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Desk.Api.Endpoints;

// Who am I, what may I see, and the shared lists every screen needs.
public static class MeEndpoints
{
    public record ModuleAccessDto(string Key, string Name, string Group, int Phase, string Description,
        bool DepartmentScoped, bool Sensitive, string Level, int? HotelId, int? DepartmentId, string Source);
    public record MeDto(int? Id, string Name, string? Email, bool IsAdmin, int? DepartmentId, int? HotelId,
        List<ModuleAccessDto> Modules);

    public record HotelDto(int Id, string Code, string Name, string? Address, bool IsActive);
    public record DepartmentDto(int Id, string Name, bool IsActive);
    public record LocationDto(int Id, string Name, int SortOrder, bool IsActive);
    public record AreaDto(int Id, int? HotelId, string Name, int SortOrder, bool IsActive, List<LocationDto> Locations);
    public record ModuleDto(string Key, string Name, string Group, int Phase, string Description, bool DepartmentScoped, bool Sensitive);
    public record ListItemDto(int Id, string ListKey, string Name, int SortOrder, bool IsActive);
    public record ListInfoDto(string Key, string Name, string Description);
    public record LookupsDto(List<HotelDto> Hotels, List<DepartmentDto> Departments, List<AreaDto> Areas, List<ModuleDto> Modules,
        List<ListInfoDto> Lists, List<ListItemDto> ListItems);
    public record PersonDto(int Id, string Name, int? DepartmentId, int? HotelId);

    public static void MapMeEndpoints(this WebApplication app)
    {
        app.MapGet("/api/me", async (CurrentUser me, CancellationToken ct) =>
        {
            var u = await me.GetUserAsync(ct);
            var access = await me.GetAccessAsync(ct);
            return new MeDto(u?.Id, u?.DisplayName ?? me.Name, u?.Email, me.IsAdmin, u?.DepartmentId, u?.HotelId,
                Modules.All.Select(m => ToDto(m, access[m.Key])).ToList());
        });

        // Lists for drop-downs. Switched-off entries are included (marked) so old
        // records still show their names; screens offer only active ones for new entries.
        app.MapGet("/api/lookups", async (DeskDbContext db, CancellationToken ct) => await LoadLookupsAsync(db, ct));

        // Colleagues for "who" pickers on forms (written by, associate, employee).
        // Names only; removed and disabled people are left out.
        app.MapGet("/api/people", async (DeskDbContext db, CancellationToken ct) =>
            await db.Users.Where(u => u.BlockedUtc == null && u.RemovedUtc == null).OrderBy(u => u.DisplayName)
                .Select(u => new PersonDto(u.Id, u.DisplayName, u.DepartmentId, u.HotelId)).ToListAsync(ct));
    }

    public static ModuleAccessDto ToDto(ModuleInfo m, EffectiveAccess a) =>
        new(m.Key, m.Name, m.Group, m.Phase, m.Description, m.DepartmentScoped, m.Sensitive,
            a.Level.ToString(), a.HotelId, a.DepartmentId, a.Source);

    public static async Task<LookupsDto> LoadLookupsAsync(DeskDbContext db, CancellationToken ct) => new(
        await db.Hotels.OrderBy(h => h.Id).Select(h => new HotelDto(h.Id, h.Code, h.Name, h.Address, h.IsActive)).ToListAsync(ct),
        await db.Departments.OrderBy(d => d.Name).Select(d => new DepartmentDto(d.Id, d.Name, d.IsActive)).ToListAsync(ct),
        await db.LocationAreas.OrderBy(a => a.SortOrder).ThenBy(a => a.Name)
            .Select(a => new AreaDto(a.Id, a.HotelId, a.Name, a.SortOrder, a.IsActive,
                a.Locations.OrderBy(l => l.SortOrder).ThenBy(l => l.Name)
                    .Select(l => new LocationDto(l.Id, l.Name, l.SortOrder, l.IsActive)).ToList()))
            .ToListAsync(ct),
        Modules.All.Select(m => new ModuleDto(m.Key, m.Name, m.Group, m.Phase, m.Description, m.DepartmentScoped, m.Sensitive)).ToList(),
        ListKeys.All.Select(l => new ListInfoDto(l.Key, l.Name, l.Description)).ToList(),
        await db.ListItems.OrderBy(i => i.ListKey).ThenBy(i => i.SortOrder).ThenBy(i => i.Name)
            .Select(i => new ListItemDto(i.Id, i.ListKey, i.Name, i.SortOrder, i.IsActive)).ToListAsync(ct));
}
