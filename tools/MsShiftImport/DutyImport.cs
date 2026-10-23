using System.Text.Json;
using Desk.Core.Data;
using Desk.Core.Incidents;
using Desk.Core.OnDuty;
using Microsoft.EntityFrameworkCore;

namespace MsShiftImport;

// Parsed MSShift On Duty and Call Out rows (see tools/msshift-import/parse_duty.py).
public class MsDuty
{
    public string? Number { get; set; }
    public string? Name { get; set; }
    public string? Shift { get; set; }
    public string? Radio { get; set; }
    public string? Post { get; set; }
    public string? Start { get; set; }
    public string? End { get; set; }
    public string? EnteredBy { get; set; }
}

public class MsCallOut
{
    public string? Number { get; set; }
    public string? Date { get; set; }
    public string? Shift { get; set; }
    public string? Name { get; set; }
    public string? Department { get; set; }
    public string? Reason { get; set; }
    public string? Called { get; set; }
    public string? TakenBy { get; set; }
}

// MSShift kept no hotel on these lists: everything goes to SHO (the only hotel using them).
public class DutyImport(Importer imp)
{
    public class Result
    {
        public int DutyRead, DutyAdded, DutyThere, DutySkipped, CallsRead, CallsAdded, CallsThere, CallsSkipped;
        public Dictionary<string, int> DutyByYear = [], Reasons = [];

        public void Print()
        {
            Console.WriteLine($"On duty: read {DutyRead}, added {DutyAdded}, already imported {DutyThere}, skipped (no number/times) {DutySkipped}");
            Console.WriteLine("  by year: " + string.Join(", ", DutyByYear.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key} {kv.Value}")));
            Console.WriteLine($"Call outs: read {CallsRead}, added {CallsAdded}, already imported {CallsThere}, skipped {CallsSkipped}");
            Console.WriteLine("  reasons: " + string.Join(", ", Reasons.Select(kv => $"{kv.Key} {kv.Value}")));
        }
    }

    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // MSShift reason -> Shore Desk reason (OnDutyRules.Reasons); anything else is Other with the original kept.
    static readonly Dictionary<string, string> ReasonMap = new()
    {
        ["sick"] = "Sick", ["familyemergency"] = "Family emergency", ["personal"] = "Personal",
        ["transportation"] = "Transportation", ["nocallnoshow"] = "No call / no show", ["noshow"] = "No call / no show",
    };

    public async Task<Result> RunAsync(string dutyFile, string callFile)
    {
        var res = new Result();
        var hotel = imp.Hotels["SHO"];
        imp.Db.ChangeTracker.AutoDetectChangesEnabled = false;

        var have = (await imp.Db.DutyAssignments.Where(d => d.LegacyRef != null).Select(d => d.LegacyRef!).ToListAsync()).ToHashSet();
        foreach (var m in JsonSerializer.Deserialize<List<MsDuty>>(await File.ReadAllTextAsync(dutyFile), Json)!)
        {
            res.DutyRead++;
            if (m.Number is null || Importer.Local(m.Start) is not { } start || Importer.Local(m.End) is not { } end) { res.DutySkipped++; continue; }
            var legacy = $"{Importer.RefPrefix}{m.Number}";
            if (!have.Add(legacy)) { res.DutyThere++; continue; }
            var shift = ShiftOf(m.Shift, start);
            var created = Importer.ToUtc(start);
            imp.Db.DutyAssignments.Add(new DutyAssignment
            {
                LegacyRef = legacy, HotelId = hotel, ShiftDate = ShiftDate(shift, start), Shift = shift,
                Name = Importer.Clean(m.Name, 200), Radio = Importer.Clean(m.Radio, 40),
                // MSShift's Post column mostly repeats the shift name; keep it only when it says something else.
                Post = Enum.TryParse<Shift>(m.Post, true, out _) ? null : Importer.Clean(m.Post, 100),
                StartLocal = start, EndLocal = end > start ? end : start,
                CreatedById = imp.ImportUserId, CreatedUtc = created, UpdatedUtc = created,
            });
            res.DutyAdded++;
            res.DutyByYear[start.Year.ToString()] = res.DutyByYear.GetValueOrDefault(start.Year.ToString()) + 1;
            if (res.DutyAdded % 2000 == 0) await SaveAsync();
        }
        await SaveAsync();

        have = (await imp.Db.CallOuts.Where(c => c.LegacyRef != null).Select(c => c.LegacyRef!).ToListAsync()).ToHashSet();
        foreach (var m in JsonSerializer.Deserialize<List<MsCallOut>>(await File.ReadAllTextAsync(callFile), Json)!)
        {
            res.CallsRead++;
            var called = Importer.Local(m.Called);
            if (m.Number is null || (Importer.Day(m.Date) ?? (called is { } c0 ? DateOnly.FromDateTime(c0) : null)) is not { } date) { res.CallsSkipped++; continue; }
            var legacy = $"{Importer.RefPrefix}{m.Number}";
            if (!have.Add(legacy)) { res.CallsThere++; continue; }
            var reason = ReasonMap.GetValueOrDefault(Importer.Key(m.Reason ?? ""), "Other");
            var notes = new List<string>();
            if (reason == "Other" && m.Reason is not null) notes.Add($"Reason in MSShift: {m.Reason}");
            if (m.TakenBy is not null) notes.Add($"Taken by (MSShift): {m.TakenBy}");
            var deptId = imp.DepartmentId(m.Department);
            if (deptId is null && m.Department is not null) notes.Add($"Department (MSShift): {m.Department}");
            var when = called ?? date.ToDateTime(TimeOnly.MinValue);
            var created = Importer.ToUtc(when);
            imp.Db.CallOuts.Add(new CallOut
            {
                LegacyRef = legacy, HotelId = hotel, ShiftDate = date,
                Shift = Enum.TryParse<Shift>(m.Shift, true, out var s) ? s : IncidentRules.ShiftFor(when),
                Name = Importer.Clean(m.Name, 200), DepartmentId = deptId, CalledLocal = when, Reason = reason,
                Notes = notes.Count == 0 ? null : Importer.Clean(string.Join("\n", notes), 1000),
                CreatedById = imp.ImportUserId, CreatedUtc = created, UpdatedUtc = created,
            });
            res.CallsAdded++;
            res.Reasons[reason] = res.Reasons.GetValueOrDefault(reason) + 1;
        }
        await SaveAsync();

        imp.Db.ActivityLog.Add(new ActivityLog
        {
            Utc = DateTime.UtcNow, UserId = imp.ImportUserId, UserName = "MSShift import", Action = "Import", Module = "onduty",
            EntityType = "DutyAssignment", Details = JsonSerializer.Serialize(new { res.DutyAdded, res.CallsAdded }),
        });
        await SaveAsync();
        return res;
    }

    static Shift ShiftOf(string? text, DateTime start) =>
        Enum.TryParse<Shift>(text, true, out var s) ? s : IncidentRules.ShiftFor(start);

    // An overnight shift belongs to the date it started (21:00); a start after midnight is the day before.
    static DateOnly ShiftDate(Shift shift, DateTime start)
    {
        var day = DateOnly.FromDateTime(start);
        return shift == Shift.Overnight && start.Hour < 12 ? day.AddDays(-1) : day;
    }

    async Task SaveAsync()
    {
        imp.Db.ChangeTracker.DetectChanges();
        await imp.Db.SaveChangesAsync();
        imp.Db.ChangeTracker.Clear();
    }
}
