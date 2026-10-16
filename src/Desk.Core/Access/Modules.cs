namespace Desk.Core.Access;

// How much someone may do in a module. Higher includes everything below it.
// None = the module is hidden from their menu.
// Manage = also assigns, closes, edits the module's lists and runs its reports.
public enum AccessLevel { None = 0, View = 1, Edit = 2, Manage = 3 }

// Which departments' records a grant covers, for modules split by department
// (department inventory, IT tickets). Other modules ignore it.
public enum DepartmentLimit { All = 0, Own = 1, Specific = 2 }

// One module of the app. The list lives in code (not the database) because each
// module is a piece of the program; the access screen shows them all.
public record ModuleInfo(
    string Key,
    string Name,
    string Group,
    int Phase,
    string Description,
    bool DepartmentScoped = false,
    bool Sensitive = false);

public static class Modules
{
    public const string Incidents = "incidents";
    public const string IncidentsHighLevel = "incidents-highlevel";
    public const string IncidentsEmployee = "incidents-employee";
    public const string EventLog = "eventlog";
    public const string Handover = "handover";
    public const string OnDuty = "onduty";
    public const string SafetyReports = "safety-reports";
    public const string Keys = "keys";
    public const string Visitors = "visitors";
    public const string Lockers = "lockers";
    public const string Parking = "parking";
    public const string LostFound = "lostfound";
    public const string Packages = "packages";
    public const string Patrol = "patrol";
    public const string ItTickets = "it-tickets";
    public const string ItIncidents = "it-incidents";
    public const string ItEquipment = "it-equipment";
    public const string Inventory = "inventory";

    public const string GroupSafety = "Safety and security";
    public const string GroupDesk = "Desk tools";
    public const string GroupIt = "IT";
    public const string GroupDepartments = "All departments";

    public static readonly IReadOnlyList<ModuleInfo> All =
    [
        new(Incidents, "Incident reports", GroupSafety, 2,
            "Incident, Guest Accident/Illness and Property Missing/Damaged reports."),
        new(IncidentsHighLevel, "High Level incident reports", GroupSafety, 2,
            "Assaults, arrests, robberies and other High Level reports. Holds personal details.", Sensitive: true),
        new(IncidentsEmployee, "Employee injury reports", GroupSafety, 2,
            "Employee Accident/Illness reports. Holds medical details (workers' comp).", Sensitive: true),
        new(EventLog, "Event log", GroupSafety, 2, "Daily security log and task reminders."),
        new(Handover, "Handover", GroupSafety, 2, "Notes for the next shift and items flagged from every module."),
        new(OnDuty, "On duty", GroupSafety, 2, "Shift roster and call-out (sick) log."),
        new(SafetyReports, "Safety reports", GroupSafety, 2, "Shift summary and incident reports by type, shift, officer, quarter."),

        new(Keys, "Keys", GroupDesk, 3, "Sign keys out and in, shift key count, keys out too long."),
        new(Visitors, "Visitors", GroupDesk, 3, "Visitor check in/out, badges, scheduled visits."),
        new(Lockers, "Lockers", GroupDesk, 3, "Employee lockers and lock codes."),
        new(Parking, "Parking", GroupDesk, 3, "Employee and motorcycle parking spaces."),
        new(LostFound, "Lost and found", GroupDesk, 3, "Found and lost items, matches, storage, shipping to guests."),
        new(Packages, "Packages", GroupDesk, 3, "Incoming and outgoing packages."),
        new(Patrol, "Patrol", GroupDesk, 3, "Patrol routes, checkpoint tags, missed checkpoints."),

        new(ItTickets, "IT tickets", GroupIt, 5, "Support tickets opened by email or in the app.", DepartmentScoped: true),
        new(ItIncidents, "IT security incidents", GroupIt, 5, "Security incidents, containment, root cause, lessons learned."),
        new(ItEquipment, "IT equipment", GroupIt, 5, "Laptops, PCs, printers, phones, network gear and warranties."),

        new(Inventory, "Department inventory", GroupDepartments, 6,
            "Each department's own stock, low-stock alerts, monthly count.", DepartmentScoped: true),
    ];

    static readonly Dictionary<string, ModuleInfo> ByKey = All.ToDictionary(m => m.Key, StringComparer.OrdinalIgnoreCase);

    public static ModuleInfo? Find(string key) => ByKey.GetValueOrDefault(key);
}
