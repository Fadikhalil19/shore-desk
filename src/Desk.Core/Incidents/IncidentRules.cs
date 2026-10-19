using Desk.Core.Access;
using Desk.Core.Data;

namespace Desk.Core.Incidents;

// The incident-report rules in one place, as pure functions so they can be tested.
public static class IncidentRules
{
    // High Level and employee injury reports are behind their own access levels.
    public static string ModuleFor(IncidentForm form) => form switch
    {
        IncidentForm.HighLevel => Modules.IncidentsHighLevel,
        IncidentForm.EmployeeInjury => Modules.IncidentsEmployee,
        _ => Modules.Incidents,
    };

    public static string FormName(IncidentForm form) => form switch
    {
        IncidentForm.Incident => "Incident",
        IncidentForm.GuestInjury => "Guest Accident/Illness",
        IncidentForm.EmployeeInjury => "Employee Accident/Illness",
        IncidentForm.PropertyMissingDamaged => "Property Missing/Damaged",
        IncidentForm.HighLevel => "High Level",
        _ => form.ToString(),
    };

    // Which list the Type comes from, or which fixed kinds the form offers instead.
    public static string? TypeList(IncidentForm form) => form switch
    {
        IncidentForm.Incident => ListKeys.IncidentType,
        IncidentForm.HighLevel => ListKeys.HighLevelType,
        _ => null,
    };

    public static string[] Kinds(IncidentForm form) => form switch
    {
        IncidentForm.GuestInjury or IncidentForm.EmployeeInjury => ["Accident", "Illness"],
        IncidentForm.PropertyMissingDamaged => ["Missing", "Damaged"],
        _ => [],
    };

    // Shift times as in MSShift's shift summary: Morning 5:00-13:00,
    // Afternoon 13:00-21:00, Overnight 21:00-5:00.
    public static Shift ShiftFor(DateTime local) => local.Hour switch
    {
        >= 5 and < 13 => Shift.Morning,
        >= 13 and < 21 => Shift.Afternoon,
        _ => Shift.Overnight,
    };

    // What is still missing before a report can go to the supervisor. A draft can be
    // saved with anything missing (officers often finish a report later).
    public static List<string> MissingForSubmit(IncidentReport r, IncidentDetails d)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(r.Heading)) missing.Add("Heading");
        if (TypeList(r.Form) is not null && r.TypeId is null) missing.Add("Type");
        if (Kinds(r.Form).Length > 0 && string.IsNullOrWhiteSpace(r.Kind)) missing.Add("Type");
        if (r.OccurredLocal is null) missing.Add("Date and time it happened");
        if (r.AreaId is null) missing.Add("Location");
        if (string.IsNullOrWhiteSpace(r.Narrative)) missing.Add("Statement of facts");
        if (r.SupervisorId is null) missing.Add("Supervisor");
        switch (r.Form)
        {
            case IncidentForm.GuestInjury or IncidentForm.EmployeeInjury:
                if (d.Subjects.Count == 0) missing.Add(r.Form == IncidentForm.EmployeeInjury ? "Injured employee" : "Injured person");
                if (d.Medical?.BodyPartId is null) missing.Add("Body part");
                if (d.Medical?.BodyPartLocationId is null) missing.Add("Body part side/area");
                break;
            case IncidentForm.PropertyMissingDamaged:
                if (d.PropertyItems.Count == 0) missing.Add("At least one item");
                break;
        }
        if (r.Form == IncidentForm.EmployeeInjury && d.Subjects.FirstOrDefault() is { } e
            && e.UserId is null && string.IsNullOrWhiteSpace(e.LastName))
            missing.Add("Employee name");
        return missing;
    }

    // Size limits on the optional sections, so one report cannot grow without end.
    public static string? CheckDetails(IncidentDetails d)
    {
        if (d.Subjects.Count > 10 || d.Victims.Count > 10 || d.Witnesses.Count > 10)
            return "At most 10 subjects, victims or witnesses on one report.";
        if (d.LockInterrogation.Count > 10) return "At most 10 lock interrogation rows.";
        if (d.PropertyItems.Count > 20) return "At most 20 items on one report.";
        if (d.PropertyItems.Any(i => i.ClaimValue is < 0 or > 10_000_000)) return "Check the claim values.";
        foreach (var p in d.Subjects.Concat(d.Victims).Concat(d.Witnesses).Append(d.ReportedBy))
            if (p?.Type is { } t && !PersonTypes.All.Contains(t)) return $"Unknown person type '{t}'.";
        return null;
    }

    // Names of everyone on the report, for searching ("Smith" finds the report).
    public static string PeopleSearch(IncidentDetails d)
    {
        var names = d.Subjects.Concat(d.Victims).Concat(d.Witnesses).Append(d.ReportedBy)
            .Where(p => p is not null)
            .Select(p => $"{p!.FirstName} {p.LastName} {p.Room}".Trim())
            .Concat([d.Vehicle?.Plate, d.Police?.CaseNumber, d.Claim?.ClaimNumber])
            .Where(s => !string.IsNullOrWhiteSpace(s));
        var text = string.Join(" | ", names);
        return text.Length > 400 ? text[..400] : text;
    }

    // --- Who may do what ------------------------------------------------------
    // Level is the person's level in the report's module, already limited to the
    // report's hotel by the caller (None when their hotel limit excludes it).

    public static bool CanEdit(IncidentStatus status, AccessLevel level, bool isWriter) => status switch
    {
        IncidentStatus.Draft => level >= AccessLevel.Manage || (level >= AccessLevel.Edit && isWriter),
        IncidentStatus.AwaitingReview => level >= AccessLevel.Manage,   // supervisor fixes before approving
        _ => false,                                                      // approved: reopen first
    };

    public static bool CanSubmit(IncidentStatus status, AccessLevel level, bool isWriter) =>
        status == IncidentStatus.Draft && CanEdit(status, level, isWriter);

    // A supervisor reviews someone else's report; only an admin may approve their own.
    public static bool CanReview(IncidentStatus status, AccessLevel level, bool isWriter, bool isAdmin) =>
        status == IncidentStatus.AwaitingReview && level >= AccessLevel.Manage && (!isWriter || isAdmin);

    public static bool CanReopen(IncidentStatus status, AccessLevel level) =>
        status == IncidentStatus.Approved && level >= AccessLevel.Manage;

    public static bool CanVoid(IncidentStatus status, AccessLevel level) =>
        status != IncidentStatus.Voided && level >= AccessLevel.Manage;
}
