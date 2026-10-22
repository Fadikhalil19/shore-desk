using System.Globalization;
using System.Text.RegularExpressions;
using Desk.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace MsShiftImport;

// What every MSShift import needs: the "MSShift import" user, hotels, and name lookups
// for lists, departments and location areas (MSShift's misspelled and prefixed names
// are matched loosely; anything new is added switched off so old records keep it).
public partial class Importer(DeskDbContext db)
{
    public const string RefPrefix = "MSShift ";
    public static readonly Guid ImportUserGuid = new("6d5e1f3a-0000-4000-8000-4d5353686966");
    static readonly TimeZoneInfo Pacific = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");

    public DeskDbContext Db { get; } = db;
    public int ImportUserId { get; private set; }
    public Dictionary<string, int> Hotels { get; } = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, Dictionary<string, int>> lists = [];
    readonly Dictionary<string, List<(string Key, int Id)>> activeNames = [];
    readonly Dictionary<string, int> departments = [];
    readonly List<(int Id, int? HotelId, string Key)> areas = [];
    readonly Dictionary<(int AreaId, string Key), int> spots = [];
    public Dictionary<string, int> NewListItems { get; } = [];
    public int NewSpots { get; private set; }

    public async Task PrepareAsync()
    {
        var user = await Db.Users.FirstOrDefaultAsync(u => u.EntraObjectId == ImportUserGuid);
        if (user is null)
        {
            var now = DateTime.UtcNow;
            // Blocked from the start: it only stands in as the author of imported records.
            user = new AppUser
            {
                EntraObjectId = ImportUserGuid, DisplayName = "MSShift import", FirstSeenUtc = now, LastSeenUtc = now,
                BlockedUtc = now,
            };
            Db.Users.Add(user);
            await Db.SaveChangesAsync();
        }
        ImportUserId = user.Id;

        foreach (var h in await Db.Hotels.ToListAsync()) Hotels[h.Code] = h.Id;
        foreach (var item in await Db.ListItems.ToListAsync())
        {
            if (!lists.TryGetValue(item.ListKey, out var map)) lists[item.ListKey] = map = [];
            map.TryAdd(Key(item.Name), item.Id);
            if (item.IsActive)
            {
                if (!activeNames.TryGetValue(item.ListKey, out var names)) activeNames[item.ListKey] = names = [];
                names.Add((Key(item.Name), item.Id));
            }
        }
        foreach (var d in await Db.Departments.ToListAsync()) departments.TryAdd(Key(d.Name), d.Id);
        foreach (var a in await Db.LocationAreas.ToListAsync()) areas.Add((a.Id, a.HotelId, Key(a.Name)));
        foreach (var l in await Db.Locations.ToListAsync()) spots.TryAdd((l.AreaId, Key(l.Name)), l.Id);
    }

    // Lowercase letters and digits only: "Trip/Fall " and "trip fall" are the same entry.
    public static string Key(string s) => NonWord().Replace(s.ToLowerInvariant(), "");

    [GeneratedRegex("[^a-z0-9]")]
    private static partial Regex NonWord();

    public int? ListId(string listKey, string? name)
    {
        name = Clean(name, 200);
        if (name is null) return null;
        if (!lists.TryGetValue(listKey, out var map)) lists[listKey] = map = [];
        if (map.TryGetValue(Key(name), out var id)) return id;
        var item = new ListItem { ListKey = listKey, Name = name, SortOrder = 9000, IsActive = false };
        Db.ListItems.Add(item);
        Db.SaveChanges();
        map[Key(name)] = item.Id;
        NewListItems[listKey] = NewListItems.GetValueOrDefault(listKey) + 1;
        return item.Id;
    }

    // The active entry whose name the text starts with ("Guest Assist Luggage" -> "Guest Assist"),
    // longest name first; otherwise the text itself, added switched off.
    public int? ListIdByStart(string listKey, string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var key = Key(text);
        var best = activeNames.TryGetValue(listKey, out var names)
            ? names.Where(n => n.Key.Length > 0 && key.StartsWith(n.Key)).OrderByDescending(n => n.Key.Length).Select(n => (int?)n.Id).FirstOrDefault()
            : null;
        return best ?? ListId(listKey, text);
    }

    // MSShift department names carried a hotel prefix ("SHO-Housekeeping").
    static readonly Dictionary<string, string> DepartmentAliases = new()
    {
        ["aaaparkingvalet"] = "Parking and Valet", ["adminownership"] = "Ownership", ["reservation"] = "Reservations",
        ["shoconcierge"] = "Concierge", ["shoengineering"] = "Engineering", ["shofbkitchen"] = "F&B Kitchen",
        ["shofbminibar"] = "F&B Mini Bar", ["shofbrestaurant"] = "F&B Restaurant", ["shoguestservices"] = "Guest Services",
        ["shohousekeeping"] = "Housekeeping", ["sholaundry"] = "Laundry",
    };

    public int? DepartmentId(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var key = Key(name);
        if (DepartmentAliases.TryGetValue(key, out var alias)) key = Key(alias);
        if (departments.TryGetValue(key, out var id)) return id;
        if (key.StartsWith("sho") && departments.TryGetValue(key[3..], out id)) return id;
        return null;
    }

    // MSShift location: "<category>, <spot>", e.g. "SH A Building 1st Floor, Ice machine".
    // Guest Room spots are room numbers and go to Room instead of becoming spots.
    public record Place(int HotelId, int? AreaId, int? LocationId, string? Room, string? Detail);

    static readonly Dictionary<string, string> AreaAliases = new()
    {
        ["property"] = "Property (grounds)", ["ovh"] = "Ocean View Hotel", ["smh"] = "Santa Monica Hotel",
        ["smm"] = "Santa Monica Hotel",
    };

    public Place PlaceOf(string? text)
    {
        var sho = Hotels["SHO"];
        if (string.IsNullOrWhiteSpace(text)) return new(sho, null, null, null, null);
        var cut = text.IndexOf(',');
        var areaText = (cut >= 0 ? text[..cut] : text).Trim();
        var spotText = cut >= 0 ? text[(cut + 1)..].Trim() : "";
        if (areaText.StartsWith("SH ", StringComparison.OrdinalIgnoreCase)) areaText = areaText[3..];
        var key = Key(areaText);
        if (AreaAliases.TryGetValue(key, out var alias)) key = Key(alias);
        var area = areas.FirstOrDefault(a => a.Key == key);
        var lower = text.ToLowerInvariant();
        var hotel = area.HotelId
            ?? (lower.Contains("ovh") || lower.Contains("ocean view") ? Hotels["OVH"]
                : lower.Contains("smh") || lower.Contains("santa monica hotel") ? Hotels["SMH"] : sho);
        if (area.Id == 0) return new(hotel, null, null, null, Clean(text, 200));
        if (key == "guestroom") return new(hotel, area.Id, null, Clean(spotText, 40), null);
        if (spotText.Length == 0) return new(hotel, area.Id, null, null, null);
        if (!spots.TryGetValue((area.Id, Key(spotText)), out var spotId))
        {
            var spot = new Location { AreaId = area.Id, Name = Clean(spotText, 200)!, SortOrder = 9000 };
            Db.Locations.Add(spot);
            Db.SaveChanges();
            spots[(area.Id, Key(spotText))] = spotId = spot.Id;
            NewSpots++;
        }
        return new(hotel, area.Id, spotId, null, null);
    }

    static readonly string[] DateFormats = ["yyyy-MM-ddTHH:mm", "yyyy-MM-dd", "M/d/yyyy", "MM/dd/yyyy", "M/d/yyyy H:mm", "M/d/yy"];

    public static DateTime? Local(string? iso) =>
        iso is not null && DateTime.TryParseExact(iso.Trim(), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d : null;

    public static DateOnly? Day(string? text) => Local(text) is { } d ? DateOnly.FromDateTime(d) : null;

    public static DateTime ToUtc(DateTime local)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        return Pacific.IsInvalidTime(unspecified) ? TimeZoneInfo.ConvertTimeToUtc(unspecified.AddHours(1), Pacific)
            : TimeZoneInfo.ConvertTimeToUtc(unspecified, Pacific);
    }

    public static string? Clean(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();
        return s.Length > max ? s[..max] : s;
    }

    public static (string? First, string? Last) SplitName(string? name)
    {
        name = Clean(name, 200);
        if (name is null) return (null, null);
        var cut = name.LastIndexOf(' ');
        return cut < 0 ? (null, name) : (name[..cut].Trim(), name[(cut + 1)..].Trim());
    }

    public static bool IsYes(string? s) => s?.Trim().StartsWith("Y", StringComparison.OrdinalIgnoreCase) == true;

    public static async Task<(int incidents, int events)> RemoveAsync(DeskDbContext db)
    {
        var incidents = await db.Incidents.Where(r => r.LegacyRef != null && r.LegacyRef.StartsWith(RefPrefix)).ExecuteDeleteAsync();
        var events = await db.Events.Where(e => e.LegacyRef != null && e.LegacyRef.StartsWith(RefPrefix)).ExecuteDeleteAsync();
        return (incidents, events);
    }
}
