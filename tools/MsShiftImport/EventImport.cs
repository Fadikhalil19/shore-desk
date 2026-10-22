using System.Text.Json;
using Desk.Core.Data;
using Desk.Core.Events;
using Desk.Core.Incidents;
using Microsoft.EntityFrameworkCore;

namespace MsShiftImport;

// One parsed MSShift event (see tools/msshift-import/parse_events.py).
public class MsEvent
{
    public string? Number { get; set; }
    public string? Category { get; set; }
    public string? LocationType { get; set; }
    public string? LocationDetail { get; set; }
    public string? Shift { get; set; }
    public string? Priority { get; set; }
    public string? Started { get; set; }
    public string? Completed { get; set; }
    public string? EnteredBy { get; set; }
    public string? AssignedTo { get; set; }
    public string? Requested { get; set; }
    public string? RequestedByName { get; set; }
    public string? RequestedByType { get; set; }
    public string? RequestedByDetail { get; set; }
    public string? RelatesToName { get; set; }
    public string? RelatesToType { get; set; }
    public string? RelatesToDetail { get; set; }
    public string? Narrative { get; set; }
}

public class EventImport(Importer imp)
{
    public class Result
    {
        public int Files, Read, Added, AlreadyThere, NoNumber, NoStart, Open;
        public Dictionary<string, int> ByHotel = [], ByYear = [];
        public int NoArea;
        public Dictionary<string, int> NewListItems = [];

        public void Print()
        {
            Console.WriteLine($"Events: {Files} month files, read {Read}, added {Added}, already imported {AlreadyThere}, " +
                              $"skipped (no number {NoNumber}, no start time {NoStart})");
            Console.WriteLine("  by year:  " + string.Join(", ", ByYear.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key} {kv.Value}")));
            Console.WriteLine("  by hotel: " + string.Join(", ", ByHotel.Select(kv => $"{kv.Key} {kv.Value}")));
            Console.WriteLine($"  open in MSShift (closed by the import): {Open}; no location area matched: {NoArea}");
            Console.WriteLine("  list entries added (switched off): " +
                              (NewListItems.Count == 0 ? "none" : string.Join(", ", NewListItems.Select(kv => $"{kv.Key} {kv.Value}"))));
        }
    }

    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result> RunAsync(string folder)
    {
        var res = new Result();
        var existing = (await imp.Db.Events.Where(e => e.LegacyRef != null).Select(e => e.LegacyRef!).ToListAsync()).ToHashSet();
        var hotelCodes = imp.Hotels.ToDictionary(kv => kv.Value, kv => kv.Key);
        imp.Db.ChangeTracker.AutoDetectChangesEnabled = false;

        foreach (var file in Directory.GetFiles(folder, "events-*.json").Order())
        {
            res.Files++;
            var events = JsonSerializer.Deserialize<List<MsEvent>>(await File.ReadAllTextAsync(file), Json)!;
            foreach (var m in events)
            {
                res.Read++;
                if (string.IsNullOrWhiteSpace(m.Number)) { res.NoNumber++; continue; }
                var legacy = $"{Importer.RefPrefix}{m.Number.Replace(" ", "")}";
                if (!existing.Add(legacy)) { res.AlreadyThere++; continue; }
                if (Importer.Local(m.Started) is not { } started) { res.NoStart++; continue; }

                var e = Build(m, legacy, started, res);
                imp.Db.Events.Add(e);
                res.Added++;
                Bump(res.ByHotel, hotelCodes[e.HotelId]);
                Bump(res.ByYear, started.Year.ToString());
                if (Importer.Local(m.Completed) is null) res.Open++;
            }
            imp.Db.ChangeTracker.DetectChanges();
            await imp.Db.SaveChangesAsync();
            imp.Db.ChangeTracker.Clear();
        }

        imp.Db.ActivityLog.Add(new ActivityLog
        {
            Utc = DateTime.UtcNow, UserId = imp.ImportUserId, UserName = "MSShift import", Action = "Import", Module = "events",
            EntityType = "SecurityEvent", Details = JsonSerializer.Serialize(new { res.Files, res.Read, res.Added, res.AlreadyThere, res.ByYear }),
        });
        imp.Db.ChangeTracker.DetectChanges();
        await imp.Db.SaveChangesAsync();
        res.NewListItems = imp.NewListItems;
        return res;
    }

    SecurityEvent Build(MsEvent m, string legacy, DateTime started, Result res)
    {
        // MSShift: location type = the area ("SH A Building 1st Floor"), detail = the spot.
        var place = imp.PlaceOf(m.LocationType is null ? m.LocationDetail
            : m.LocationDetail is null ? m.LocationType : $"{m.LocationType}, {m.LocationDetail}");
        if (place.AreaId is null) res.NoArea++;
        var startedUtc = Importer.ToUtc(started);

        // Names MSShift kept that have no field here go under the narrative.
        var notes = new List<string>();
        if (!string.IsNullOrWhiteSpace(m.Narrative)) notes.Add(m.Narrative.Trim());
        if (!string.IsNullOrWhiteSpace(m.AssignedTo)) notes.Add($"Assigned to (MSShift): {m.AssignedTo.Trim()}");
        if (!string.IsNullOrWhiteSpace(m.EnteredBy)) notes.Add($"Entered by (MSShift): {m.EnteredBy.Trim()}");
        // Left open in MSShift: closed here so years-old events do not sit on today's open list.
        var completed = Importer.Local(m.Completed);
        if (completed is null) notes.Add("Still open in MSShift when imported; closed by the import.");

        return new SecurityEvent
        {
            LegacyRef = legacy,
            HotelId = place.HotelId,
            // MSShift's category text often runs category and detail together ("Guest Assist Luggage"):
            // match the start to our list and keep the full text as the detail.
            CategoryId = imp.ListIdByStart(ListKeys.EventCategory, m.Category ?? "Other")!.Value,
            Detail = Importer.Clean(m.Category, 200),
            AreaId = place.AreaId,
            LocationId = place.LocationId,
            LocationDetail = place.Detail,
            Room = place.Room,
            RequestedByType = PersonType(m.RequestedByType),
            RequestedByName = NameWithDetail(m.RequestedByName, m.RequestedByDetail,
                PersonType(m.RequestedByType) is null ? m.RequestedByType : null),
            RequestedLocal = Importer.Local(m.Requested) ?? started,
            RelatesToType = PersonType(m.RelatesToType),
            RelatesToName = NameWithDetail(m.RelatesToName, m.RelatesToDetail,
                PersonType(m.RelatesToType) is null ? m.RelatesToType : null),
            Priority = m.Priority?.Trim() switch
            {
                "Highest" => Priority.Highest, "High" => Priority.High, "Medium" => Priority.Medium, _ => Priority.Low,
            },
            StartedLocal = started,
            CompletedLocal = completed ?? started,
            Shift = Enum.TryParse<Shift>(m.Shift, true, out var shift) ? shift : IncidentRules.ShiftFor(started),
            Narrative = notes.Count == 0 ? null : string.Join("\n\n", notes),
            CreatedById = imp.ImportUserId,
            CreatedUtc = startedUtc,
            UpdatedUtc = startedUtc,
        };
    }

    static string? PersonType(string? t)
    {
        if (string.IsNullOrWhiteSpace(t)) return null;
        return EventRules.RequesterTypes.FirstOrDefault(x => Importer.Key(x) == Importer.Key(t));
    }

    static string? NameWithDetail(string? name, string? detail, string? otherType)
    {
        var extra = string.Join(", ", new[] { otherType, detail }.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()));
        var text = string.IsNullOrWhiteSpace(name) ? extra : extra.Length == 0 ? name.Trim() : $"{name.Trim()} ({extra})";
        return Importer.Clean(text, 200);
    }

    static void Bump(Dictionary<string, int> d, string key) => d[key] = d.GetValueOrDefault(key) + 1;
}
