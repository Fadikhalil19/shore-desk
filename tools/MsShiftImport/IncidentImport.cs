using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Desk.Core.Data;
using Desk.Core.Incidents;
using Microsoft.EntityFrameworkCore;

namespace MsShiftImport;

// One parsed MSShift report page (see tools/msshift-import/parse_incidents.py).
public class MsReport
{
    public string Form { get; set; } = "";
    public string? Number { get; set; }
    public string? Status { get; set; }
    public string? Type { get; set; }
    public string? Occurred { get; set; }
    public string? Reported { get; set; }
    public string? Written { get; set; }
    public string? Location { get; set; }
    public string? Narrative { get; set; }
    public string? FollowUp { get; set; }
    public string? DistributedTo { get; set; }
    public string? Claims { get; set; }
    public string? LockInterrogation { get; set; }
    public string? ReportedBy { get; set; }
    public string? WrittenBy { get; set; }
    public string? ReviewedBy { get; set; }
    public Dictionary<string, string>? Injury { get; set; }
    public Dictionary<string, string>? Medical { get; set; }
    public MsPerson? Subject { get; set; }
    public MsPerson? Victim { get; set; }
    public Dictionary<string, string>? Police { get; set; }
    public List<Dictionary<string, string>> Witnesses { get; set; } = [];
    public Dictionary<string, string>? Vehicle { get; set; }
    public Dictionary<string, string>? Media { get; set; }
    public Dictionary<string, string>? Evidence { get; set; }
    public List<Dictionary<string, string?>> PropertyItems { get; set; } = [];
    public string? TotalClaim { get; set; }
    public string? HotelProperty { get; set; }
}

public class MsPerson
{
    public string? Name { get; set; }
    public string? Type { get; set; }
    public string? TypeDetail { get; set; }
    public string? Department { get; set; }
    public string? Position { get; set; }
    public string? EmployeeNumber { get; set; }
    public string? DateOfBirth { get; set; }
    public string? DateOfHire { get; set; }
    public string? Room { get; set; }
    public string? Stay { get; set; }
    public string? Gender { get; set; }
    public string? Company { get; set; }
    public string? Email { get; set; }
    public string? Race { get; set; }
    public string? Height { get; set; }
    public string? Weight { get; set; }
    public string? Eyes { get; set; }
    public string? Hair { get; set; }
    public string? License { get; set; }
    public string? LicenseState { get; set; }
    public string? Marks { get; set; }
    public Dictionary<string, string>? Home { get; set; }
    public Dictionary<string, string>? Work { get; set; }
    public string? HomePhone { get; set; }
    public string? WorkPhone { get; set; }
    public List<List<string?>>? Extra { get; set; }
}

public partial class IncidentImport(Importer imp)
{
    public class Result
    {
        public int Read, Added, AlreadyThere;
        public Dictionary<string, int> ByForm = [], ByStatus = [], ByHotel = [];
        public int NoArea, NoOccurred, UnknownDepartment;
        public Dictionary<string, int> NewListItems = [];
        public int NewSpots;

        public void Print()
        {
            Console.WriteLine($"Incident reports: read {Read}, added {Added}, already imported {AlreadyThere}");
            Console.WriteLine("  by form:   " + string.Join(", ", ByForm.Select(kv => $"{kv.Key} {kv.Value}")));
            Console.WriteLine("  by status: " + string.Join(", ", ByStatus.Select(kv => $"{kv.Key} {kv.Value}")));
            Console.WriteLine("  by hotel:  " + string.Join(", ", ByHotel.Select(kv => $"{kv.Key} {kv.Value}")));
            Console.WriteLine($"  no location area matched (kept as text): {NoArea}; no incident date: {NoOccurred}; " +
                              $"employee department not matched: {UnknownDepartment}");
            Console.WriteLine("  list entries added (switched off): " +
                              (NewListItems.Count == 0 ? "none" : string.Join(", ", NewListItems.Select(kv => $"{kv.Key} {kv.Value}"))));
            Console.WriteLine($"  location spots added: {NewSpots}");
        }
    }

    static readonly Dictionary<string, IncidentForm> Forms = new()
    {
        ["IR"] = IncidentForm.Incident, ["GAI"] = IncidentForm.GuestInjury, ["EAI"] = IncidentForm.EmployeeInjury,
        ["MD"] = IncidentForm.PropertyMissingDamaged, ["HI"] = IncidentForm.HighLevel,
    };

    public async Task<Result> RunAsync(string path)
    {
        var reports = JsonSerializer.Deserialize<List<MsReport>>(await File.ReadAllTextAsync(path),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var res = new Result { Read = reports.Count };
        var existing = (await imp.Db.Incidents.Where(r => r.LegacyRef != null).Select(r => r.LegacyRef!).ToListAsync()).ToHashSet();
        var hotelCodes = imp.Hotels.ToDictionary(kv => kv.Value, kv => kv.Key);

        foreach (var m in reports)
        {
            var legacy = $"{Importer.RefPrefix}{m.Form} {m.Number}";
            if (existing.Contains(legacy)) { res.AlreadyThere++; continue; }
            var r = Build(m, legacy, res);
            imp.Db.Incidents.Add(r);
            res.Added++;
            Bump(res.ByForm, m.Form);
            Bump(res.ByStatus, r.Status.ToString());
            Bump(res.ByHotel, hotelCodes[r.HotelId]);
            if (res.Added % 100 == 0) await imp.Db.SaveChangesAsync();
        }
        await imp.Db.SaveChangesAsync();

        imp.Db.ActivityLog.Add(new ActivityLog
        {
            Utc = DateTime.UtcNow, UserId = imp.ImportUserId, UserName = "MSShift import", Action = "Import", Module = "incidents",
            EntityType = "IncidentReport",
            Details = JsonSerializer.Serialize(new { res.Read, res.Added, res.AlreadyThere, res.ByForm, res.ByStatus }),
        });
        await imp.Db.SaveChangesAsync();
        res.NewListItems = imp.NewListItems;
        res.NewSpots = imp.NewSpots;
        return res;
    }

    IncidentReport Build(MsReport m, string legacy, Result res)
    {
        var form = Forms[m.Form];
        var occurred = Importer.Local(m.Occurred);
        var written = Importer.Local(m.Written) ?? Importer.Local(m.Reported) ?? occurred ?? new DateTime(2020, 1, 1);
        var writtenUtc = Importer.ToUtc(written);
        var place = imp.PlaceOf(m.Location);
        if (place.AreaId is null) res.NoArea++;
        if (occurred is null) res.NoOccurred++;

        var r = new IncidentReport
        {
            Number = $"MS-{m.Number}",
            LegacyRef = legacy,
            Form = form,
            HotelId = place.HotelId,
            Heading = Importer.Clean(m.Type, 200) ?? IncidentRules.FormName(form),
            OccurredLocal = occurred,
            ReportedLocal = Importer.Local(m.Reported),
            Shift = occurred is { } o ? IncidentRules.ShiftFor(o) : null,
            AreaId = place.AreaId,
            LocationId = place.LocationId,
            LocationDetail = place.Detail,
            Room = place.Room ?? Importer.Clean(m.Subject?.Room, 40),
            Narrative = m.Narrative,
            FollowUp = FollowUp(m),
            DistributedTo = Importer.Clean(m.DistributedTo, 1000),
            WrittenById = imp.ImportUserId,
            CreatedUtc = writtenUtc,
            UpdatedUtc = writtenUtc,
        };

        // Type: a list entry for Incident / High Level, a fixed kind for the others.
        if (IncidentRules.TypeList(form) is { } listKey) r.TypeId = imp.ListId(listKey, m.Type);
        else
        {
            var t = (m.Type ?? "").ToLowerInvariant();
            r.Kind = form == IncidentForm.PropertyMissingDamaged
                ? (t.Contains("missing") ? "Missing" : "Damaged")
                : (t.Contains("illness") ? "Illness" : "Accident");
            if (!string.Equals(m.Type?.Trim(), r.Kind, StringComparison.OrdinalIgnoreCase)) r.OtherType = Importer.Clean(m.Type, 200);
        }

        // Pending -> Draft, Closed / Locked -> Approved, Deleted -> Voided.
        switch (m.Status)
        {
            case "Pending":
                r.Status = IncidentStatus.Draft;
                break;
            case "Deleted":
                r.Status = IncidentStatus.Voided;
                r.SubmittedUtc = writtenUtc;
                r.VoidedUtc = writtenUtc;
                r.VoidReason = "Deleted in MSShift (imported).";
                break;
            default:
                r.Status = IncidentStatus.Approved;
                r.SubmittedUtc = writtenUtc;
                r.ReviewedUtc = writtenUtc;
                r.ReviewNote = $"Imported from MSShift (status {m.Status}).";
                break;
        }

        var d = Details(m, form, res);
        r.DetailsJson = d.ToJson();
        r.PeopleSearch = IncidentRules.PeopleSearch(d);
        return r;
    }

    // Items MSShift kept that have no field of their own here go to the follow-up text.
    static string? FollowUp(MsReport m)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(m.FollowUp)) parts.Add(m.FollowUp.Trim());
        if (!string.IsNullOrWhiteSpace(m.LockInterrogation)) parts.Add($"Lock interrogation (MSShift): {m.LockInterrogation.Trim()}");
        if (!string.IsNullOrWhiteSpace(m.HotelProperty)) parts.Add($"Hotel property involved (MSShift): {m.HotelProperty.Trim()}");
        if (!string.IsNullOrWhiteSpace(m.TotalClaim)) parts.Add($"Total claim value (MSShift): {m.TotalClaim.Trim()}");
        return parts.Count == 0 ? null : string.Join("\n\n", parts);
    }

    IncidentDetails Details(MsReport m, IncidentForm form, Result res)
    {
        var d = new IncidentDetails
        {
            WrittenByOther = Importer.Clean(m.WrittenBy, 200),
            SupervisorOther = Importer.Clean(m.ReviewedBy, 200),
        };
        if (Importer.Clean(m.ReportedBy, 200) is { } rb)
        {
            var (first, last) = Importer.SplitName(rb);
            d.ReportedBy = new PersonEntry { FirstName = first, LastName = last };
        }

        if (Person(m.Subject, form, res) is { } subject) d.Subjects.Add(subject);
        if (Person(m.Victim, form, res) is { } victim) d.Victims.Add(victim);
        foreach (var w in m.Witnesses)
        {
            var (first, last) = Importer.SplitName(w.GetValueOrDefault("name"));
            d.Witnesses.Add(new PersonEntry
            {
                FirstName = first, LastName = last, Phone = Importer.Clean(w.GetValueOrDefault("phone"), 60),
                Home = w.GetValueOrDefault("address") is { } a ? new Address { Street = Importer.Clean(a, 300) } : null,
            });
        }

        if (m.Injury is not null || m.Medical is not null)
        {
            var i = m.Injury ?? [];
            var md = m.Medical ?? [];
            d.Medical = new MedicalEntry
            {
                InjuryTypeId = imp.ListId(ListKeys.InjuryType, i.GetValueOrDefault("injuryType")),
                BodyPartId = imp.ListId(ListKeys.BodyPart, i.GetValueOrDefault("bodyPart")),
                BodyPartLocationId = imp.ListId(ListKeys.BodyPartLocation, i.GetValueOrDefault("bodyPartLocation")),
                EmsCalled = Importer.IsYes(md.GetValueOrDefault("ems")),
                EmsArrivedLocal = Importer.Local(md.GetValueOrDefault("emsArrived")),
                EmsName = Importer.Clean(md.GetValueOrDefault("emsName"), 200),
                EmsNumber = Importer.Clean(md.GetValueOrDefault("emsNumber"), 60),
                Hospital = Importer.Clean(md.GetValueOrDefault("hospital"), 200),
                RefusedMedicalAttention = Importer.IsYes(md.GetValueOrDefault("rma")),
                ContributingFactorId = imp.ListId(ListKeys.ContributingFactor, md.GetValueOrDefault("factor")),
                ConsumedLocal = Importer.Local(md.GetValueOrDefault("consumed")),
                Symptoms = md.GetValueOrDefault("symptoms"),
            };
        }

        if (m.Police is { } p)
            d.Police = new PoliceEntry
            {
                CalledLocal = Importer.Local(p.GetValueOrDefault("called")), Agency = Importer.Clean(p.GetValueOrDefault("agency"), 200),
                OfficerName = Importer.Clean(p.GetValueOrDefault("officer"), 200),
                CaseNumber = Importer.Clean(p.GetValueOrDefault("caseNumber"), 60),
            };

        if (m.Vehicle is { } v)
        {
            var operatorType = v.GetValueOrDefault("operatorType");
            d.Vehicle = new VehicleEntry
            {
                CircumstanceId = imp.ListId(ListKeys.VehicleCircumstance, v.GetValueOrDefault("circumstance")),
                Make = Importer.Clean(v.GetValueOrDefault("make"), 100), Model = Importer.Clean(v.GetValueOrDefault("model"), 100),
                Year = Importer.Clean(v.GetValueOrDefault("year"), 10), ColorId = imp.ListId(ListKeys.Color, v.GetValueOrDefault("color")),
                Plate = Importer.Clean(v.GetValueOrDefault("plate"), 20), PlateState = Importer.Clean(v.GetValueOrDefault("plateState"), 40),
                Vin = Importer.Clean(v.GetValueOrDefault("vin"), 40), ValetTicket = Importer.Clean(v.GetValueOrDefault("valetTicket"), 40),
                OperatorName = Importer.Clean(v.GetValueOrDefault("operatorName"), 200),
                OperatorDateOfBirth = Importer.Day(v.GetValueOrDefault("operatorDob")),
                OperatorLicense = Importer.Clean(v.GetValueOrDefault("operatorLicense"), 60),
                OperatorPhone = Importer.Clean(v.GetValueOrDefault("operatorPhone"), 60),
                OwnerName = Importer.Clean(v.GetValueOrDefault("ownerName"), 200),
                OwnerAddress = Importer.Clean(v.GetValueOrDefault("ownerAddress"), 300),
                OwnerPhone = Importer.Clean(v.GetValueOrDefault("ownerPhone"), 60),
                InsuranceCompany = Importer.Clean(v.GetValueOrDefault("insurance"), 200),
                RentalCompany = Importer.Clean(v.GetValueOrDefault("rental"), 200),
                Damage = v.GetValueOrDefault("damage"),
                Description = operatorType is null ? null : $"Operator: {operatorType}",
            };
        }

        if (m.Media is { } media && (Importer.IsYes(media.GetValueOrDefault("pictures")) || Importer.IsYes(media.GetValueOrDefault("video"))))
            d.Media = new MediaEntry
            {
                PictureCount = LeadingNumber(media.GetValueOrDefault("pictureCount")),
                PicturesTakenLocal = Importer.Local(media.GetValueOrDefault("picturesTaken")),
                TakenBy = Importer.Clean(media.GetValueOrDefault("takenBy"), 200),
                VideoLocal = Importer.Local(media.GetValueOrDefault("videoTaken")),
                VideoDescription = media.GetValueOrDefault("videoDescription"),
            };

        if (m.Evidence is { } e)
            d.Evidence = new EvidenceEntry
            {
                Items = e.GetValueOrDefault("items"), StoredLocation = Importer.Clean(e.GetValueOrDefault("stored"), 200),
                Date = Importer.Day(e.GetValueOrDefault("date")), Disposition = Importer.Clean(e.GetValueOrDefault("disposition"), 200),
            };

        if (Importer.Clean(m.Claims, 60) is { } claim) d.Claim = new ClaimEntry { ClaimNumber = claim };

        foreach (var item in m.PropertyItems.Take(20))
            d.PropertyItems.Add(new PropertyItem
            {
                CategoryId = imp.ListId(ListKeys.PropertyCategory, item.GetValueOrDefault("item")),
                Description = item.GetValueOrDefault("description"),
                ColorId = imp.ListId(ListKeys.Color, item.GetValueOrDefault("color")),
                Brand = Importer.Clean(item.GetValueOrDefault("brand"), 100),
                ModelSerial = Importer.Clean(item.GetValueOrDefault("modelSerial"), 100),
                ClaimValue = Money(item.GetValueOrDefault("claimValue")),
            });
        return d;
    }

    PersonEntry? Person(MsPerson? s, IncidentForm form, Result res)
    {
        if (s is null || string.IsNullOrWhiteSpace(s.Name)) return null;
        var (first, last) = Importer.SplitName(s.Name);
        var type = form == IncidentForm.EmployeeInjury ? PersonTypes.Employee : PersonType(s.Type);
        var departmentId = imp.DepartmentId(s.Department);
        var position = Importer.Clean(s.Position, 100);
        if (s.Department is not null && departmentId is null)
        {
            res.UnknownDepartment++;
            position = Importer.Clean($"{position} ({s.Department})".Trim(), 100);
        }
        // High Level: "Type Detail" (and the unlabelled box next to it) say more about the type.
        var detail = string.Join(" / ", new[] { s.TypeDetail }.Concat((s.Extra ?? []).Select(x => x.ElementAtOrDefault(1)))
            .Where(x => !string.IsNullOrWhiteSpace(x)));
        if (detail.Length > 0) position = Importer.Clean(position is null ? detail : $"{position} / {detail}", 100);
        var (arrival, departure) = Stay(s.Stay);

        var p = new PersonEntry
        {
            Type = type, FirstName = first, LastName = last, DepartmentId = departmentId, Position = position,
            EmployeeNumber = Importer.Clean(s.EmployeeNumber, 40), DateOfHire = Importer.Day(s.DateOfHire),
            Room = Importer.Clean(s.Room, 40), ArrivalDate = arrival, DepartureDate = departure,
            Company = Importer.Clean(s.Company, 200), Phone = Importer.Clean(s.HomePhone ?? s.WorkPhone, 60),
            Email = Importer.Clean(s.Email, 200), Gender = Importer.Clean(s.Gender, 20), DateOfBirth = Importer.Day(s.DateOfBirth),
            Home = Addr(s.Home), Work = Addr(s.Work),
        };
        if (form == IncidentForm.HighLevel)
            p.Description = new PersonDescription
            {
                Race = Importer.Clean(s.Race, 60), Height = Importer.Clean(s.Height, 20), Weight = Importer.Clean(s.Weight, 20),
                Eyes = Importer.Clean(s.Eyes, 30), Hair = Importer.Clean(s.Hair, 30), LicenseNumber = Importer.Clean(s.License, 40),
                LicenseState = Importer.Clean(s.LicenseState, 40), Marks = Importer.Clean(s.Marks, 300),
            };
        return p;
    }

    static string? PersonType(string? t)
    {
        if (string.IsNullOrWhiteSpace(t)) return null;
        var match = PersonTypes.All.FirstOrDefault(x => Importer.Key(x) == Importer.Key(t));
        return match ?? (Importer.Key(t).Contains("employee") ? PersonTypes.Employee
            : Importer.Key(t).Contains("nonguest") ? PersonTypes.NonGuest : Importer.Key(t).Contains("guest") ? PersonTypes.Guest : null);
    }

    static Address? Addr(Dictionary<string, string>? a) => a is null ? null : new Address
    {
        Street = Importer.Clean(a.GetValueOrDefault("Street"), 200), City = Importer.Clean(a.GetValueOrDefault("City"), 100),
        State = Importer.Clean(a.GetValueOrDefault("State"), 60), Zip = Importer.Clean(a.GetValueOrDefault("Zip"), 20),
        Country = Importer.Clean(a.GetValueOrDefault("Country"), 60),
    };

    // "MM/DD/YYYY - MM/DD/YYYY"
    static (DateOnly?, DateOnly?) Stay(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return (null, null);
        var parts = s.Split('-', 2, StringSplitOptions.TrimEntries);
        return (Importer.Day(parts[0]), parts.Length > 1 ? Importer.Day(parts[1]) : null);
    }

    static int? LeadingNumber(string? s) =>
        s is not null && Digits().Match(s) is { Success: true } m && int.TryParse(m.Value, out var n) && n < 10_000 ? n : null;

    static decimal? Money(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var t = s.Replace("$", "").Replace(",", "").Trim();
        return decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) && v is >= 0 and <= 10_000_000 ? v : null;
    }

    [GeneratedRegex(@"\d+")]
    private static partial Regex Digits();

    static void Bump(Dictionary<string, int> d, string key) => d[key] = d.GetValueOrDefault(key) + 1;
}
