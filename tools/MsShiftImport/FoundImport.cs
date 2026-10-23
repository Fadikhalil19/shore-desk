using System.Text.Json;
using Desk.Core.Data;
using Desk.Core.LostFound;
using Microsoft.EntityFrameworkCore;

namespace MsShiftImport;

// One parsed MSShift found item (see tools/msshift-import/parse_found.py).
public class MsFound
{
    public string? Number { get; set; }
    public string? Site { get; set; }
    public string? Category { get; set; }
    public string? Detail { get; set; }
    public string? Color { get; set; }
    public string? BrandModel { get; set; }
    public string? Description { get; set; }
    public string? Found { get; set; }
    public string? Purge { get; set; }
    public string? LocationFound { get; set; }
    public string? LocationDetail { get; set; }
    public string? RegisteredGuest { get; set; }
    public string? FoundByName { get; set; }
    public string? FoundByDepartment { get; set; }
    public string? WantsItem { get; set; }
    public string? Received { get; set; }
    public string? ReceivedBy { get; set; }
    public string? ReceivedByDepartment { get; set; }
    public string? EnteredBy { get; set; }
    public string? Storage { get; set; }
    public string? FoundNarrative { get; set; }
    public string? ReturnTo { get; set; }
    public string? Disposition { get; set; }
    public string? ReturnToType { get; set; }
    public string? Room { get; set; }
    public string? ReturnToDepartment { get; set; }
    public string? Phone { get; set; }
    public string? Arrival { get; set; }
    public string? Departure { get; set; }
    public string? Email { get; set; }
    public string? Returned { get; set; }
    public string? ReturnedBy { get; set; }
    public string? ReturnedByDepartment { get; set; }
    public string? ReturnNarrative { get; set; }
    public string? Carrier { get; set; }
    public string? ShippedBy { get; set; }
    public string? Shipped { get; set; }
    public string? ShipAddress { get; set; }
    public string? ShipCity { get; set; }
    public string? ShipState { get; set; }
    public string? ShipCountry { get; set; }
}

public class FoundImport(Importer imp)
{
    public class Result
    {
        public int Read, Added, AlreadyThere, NoNumber, NoDate, NoArea;
        public Dictionary<string, int> ByStatus = [], ByYear = [];

        public void Print()
        {
            Console.WriteLine($"Found items: read {Read}, added {Added}, already imported {AlreadyThere}, " +
                              $"skipped (no number {NoNumber}, no date {NoDate})");
            Console.WriteLine("  by year:   " + string.Join(", ", ByYear.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key} {kv.Value}")));
            Console.WriteLine("  by status: " + string.Join(", ", ByStatus.Select(kv => $"{kv.Key} {kv.Value}")));
            Console.WriteLine($"  no location area matched: {NoArea}");
        }
    }

    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result> RunAsync(string file)
    {
        var res = new Result();
        var existing = (await imp.Db.FoundItems.Where(f => f.LegacyRef != null).Select(f => f.LegacyRef!).ToListAsync()).ToHashSet();
        imp.Db.ChangeTracker.AutoDetectChangesEnabled = false;

        foreach (var m in JsonSerializer.Deserialize<List<MsFound>>(await File.ReadAllTextAsync(file), Json)!)
        {
            res.Read++;
            if (string.IsNullOrWhiteSpace(m.Number)) { res.NoNumber++; continue; }
            var legacy = $"{Importer.RefPrefix}F {m.Number.Trim()}";
            if (!existing.Add(legacy)) { res.AlreadyThere++; continue; }
            if ((Importer.Local(m.Found) ?? Importer.Local(m.Received)) is not { } found) { res.NoDate++; continue; }

            var f = Build(m, legacy, found, res);
            imp.Db.FoundItems.Add(f);
            res.Added++;
            Bump(res.ByStatus, f.Status.ToString());
            Bump(res.ByYear, found.Year.ToString());
            if (res.Added % 500 == 0) await SaveAsync();
        }
        await SaveAsync();

        imp.Db.ActivityLog.Add(new ActivityLog
        {
            Utc = DateTime.UtcNow, UserId = imp.ImportUserId, UserName = "MSShift import", Action = "Import", Module = "lostfound",
            EntityType = "FoundItem", Details = JsonSerializer.Serialize(new { res.Read, res.Added, res.AlreadyThere, res.ByStatus }),
        });
        await SaveAsync();
        return res;
    }

    async Task SaveAsync()
    {
        imp.Db.ChangeTracker.DetectChanges();
        await imp.Db.SaveChangesAsync();
        imp.Db.ChangeTracker.Clear();
    }

    FoundItem Build(MsFound m, string legacy, DateTime found, Result res)
    {
        var place = imp.PlaceOf(m.LocationFound);
        if (place.AreaId is null) res.NoArea++;
        var createdUtc = Importer.ToUtc(Importer.Local(m.Received) ?? found);

        // MSShift fields with no place of their own go into the notes, labelled.
        var notes = new List<string>();
        if (!string.IsNullOrWhiteSpace(m.FoundNarrative)) notes.Add(m.FoundNarrative.Trim());
        if (m.ReceivedBy is not null) notes.Add($"Received by (MSShift): {WithDept(m.ReceivedBy, m.ReceivedByDepartment)}");
        if (m.EnteredBy is not null) notes.Add($"Entered by (MSShift): {m.EnteredBy}");
        if (m.WantsItem is not null) notes.Add($"Finder wants the item: {m.WantsItem}");
        if (m.Arrival is not null || m.Departure is not null) notes.Add($"Guest stay: {Day(m.Arrival)} - {Day(m.Departure)}");

        var item = new FoundItem
        {
            Number = $"MS-{m.Number!.Trim()}",
            LegacyRef = legacy,
            HotelId = place.HotelId,
            // Category text may run category and detail together: match the start, keep the detail.
            CategoryId = imp.ListIdByStart(ListKeys.PropertyCategory, m.Category),
            Detail = Importer.Clean(m.Detail, 200),
            Description = Importer.Clean(m.Description, 2000),
            ColorId = imp.ListId(ListKeys.Color, m.Color),
            Brand = Importer.Clean(m.BrandModel, 100),
            FoundLocal = found,
            AreaId = place.AreaId,
            LocationDetail = Importer.Clean(Join(", ", place.Detail, m.LocationDetail), 200),
            Room = place.Room,
            FoundByType = m.FoundByDepartment is not null ? "Employee" : null,
            FoundByName = Importer.Clean(m.FoundByName, 200),
            FoundByDepartmentId = imp.DepartmentId(m.FoundByDepartment),
            StorageLocation = Importer.Clean(m.Storage, 100),
            PurgeDate = Importer.Day(m.Purge) ?? DateOnly.FromDateTime(found).AddDays(LostFoundRules.DefaultKeepDays),
            Status = FoundStatus.InStorage,
            OwnerName = Importer.Clean(m.ReturnTo ?? m.RegisteredGuest, 200),
            OwnerContact = Importer.Clean(Join(", ", m.Phone, m.Email), 200),
            CreatedById = imp.ImportUserId,
            CreatedUtc = createdUtc,
            UpdatedUtc = createdUtc,
        };
        if (m.FoundByDepartment is not null && item.FoundByDepartmentId is null)
            notes.Add($"Finder department (MSShift): {m.FoundByDepartment}");

        // Closed in MSShift: returned in person, shipped, destroyed or donated.
        if ((Importer.Local(m.Returned) ?? Importer.Local(m.Shipped)) is { } closed)
        {
            item.ClosedLocal = closed;
            item.ClosedById = imp.ImportUserId;
            var close = new List<string>();
            var disposition = m.Disposition?.Trim();
            if (disposition is "Destroyed" or "Donated" or "Discarded")
            {
                item.Status = FoundStatus.Disposed;
                item.DisposalMethod = disposition;
            }
            else if (m.Carrier is not null || m.ShipAddress is not null)
            {
                item.Status = FoundStatus.Shipped;
                close.Add($"Shipped by {m.Carrier ?? "?"} to {Join(", ", m.ReturnTo, m.ShipAddress, m.ShipCity, m.ShipState, m.ShipCountry)}");
                if (m.ShippedBy is not null) close.Add($"Shipped by (MSShift): {m.ShippedBy}");
            }
            else
            {
                item.Status = FoundStatus.ReturnedToOwner;
                item.ReturnedToName = Importer.Clean(WithDept(m.ReturnTo, m.ReturnToDepartment) ?? m.ReturnToType, 200);
                if (disposition is not null) close.Add($"Disposition (MSShift): {disposition}");
            }
            if (m.ReturnToType is not null && item.Status != FoundStatus.ReturnedToOwner) close.Add($"Return to: {WithDept(m.ReturnTo, m.ReturnToType)}");
            if (m.ReturnedBy is not null) close.Add($"Returned by (MSShift): {WithDept(m.ReturnedBy, m.ReturnedByDepartment)}");
            if (m.Room is not null) close.Add($"Room: {m.Room}");
            if (!string.IsNullOrWhiteSpace(m.ReturnNarrative)) close.Add(m.ReturnNarrative.Trim());
            item.CloseNote = Importer.Clean(string.Join("\n", close), 1000);
        }
        notes.Add("Imported from MSShift.");
        item.Notes = Importer.Clean(string.Join("\n", notes), 2000);
        return item;
    }

    static string? WithDept(string? name, string? dept) =>
        name is null ? null : dept is null ? name.Trim() : $"{name.Trim()} ({dept.Trim()})";

    static string? Join(string sep, params string?[] parts)
    {
        var s = string.Join(sep, parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));
        return s.Length == 0 ? null : s;
    }

    static string Day(string? iso) => Importer.Day(iso)?.ToString("MM/dd/yyyy") ?? "?";

    static void Bump(Dictionary<string, int> d, string k) => d[k] = d.GetValueOrDefault(k) + 1;
}
