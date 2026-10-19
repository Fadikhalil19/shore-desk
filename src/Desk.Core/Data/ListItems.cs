namespace Desk.Core.Data;

// The pick-lists used on forms (incident types, injury types, colours, ...), all in
// one table and told apart by ListKey. Admins add, rename and switch off entries in
// Settings > Report lists; nothing is deleted, so old reports keep their names.
public class ListItem
{
    public int Id { get; set; }
    public required string ListKey { get; set; }
    public required string Name { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public record ListInfo(string Key, string Name, string Description);

public static class ListKeys
{
    public const string IncidentType = "incident-type";
    public const string HighLevelType = "highlevel-type";
    public const string InjuryType = "injury-type";
    public const string BodyPart = "body-part";
    public const string BodyPartLocation = "body-part-location";
    public const string ContributingFactor = "contributing-factor";
    public const string PropertyCategory = "property-category";
    public const string Color = "color";
    public const string VehicleCircumstance = "vehicle-circumstance";
    public const string EvidenceStatus = "evidence-status";

    public static readonly IReadOnlyList<ListInfo> All =
    [
        new(IncidentType, "Incident types", "Type on Incident reports."),
        new(HighLevelType, "High Level types", "Type on High Level reports."),
        new(InjuryType, "Injury types", "Guest and employee accident/illness reports."),
        new(BodyPart, "Body parts", "Guest and employee accident/illness reports."),
        new(BodyPartLocation, "Body part side/area", "Left, right, upper, ... on accident/illness reports."),
        new(ContributingFactor, "Contributing factors", "Main cause on accident/illness reports."),
        new(PropertyCategory, "Property categories", "Items on Property Missing/Damaged and High Level reports."),
        new(Color, "Colors", "Item and vehicle colors."),
        new(VehicleCircumstance, "Vehicle circumstances", "Vehicle section of a report."),
        new(EvidenceStatus, "Evidence status", "Evidence section of a report."),
    ];

    public static bool IsKnown(string key) => All.Any(l => l.Key == key);
}
