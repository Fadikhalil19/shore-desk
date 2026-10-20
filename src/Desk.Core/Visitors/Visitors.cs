using Desk.Core.Data;

namespace Desk.Core.Visitors;

// A visitor on property: checked in at the desk, checked out when they leave.
// Never deleted; a mistaken entry is voided with a reason.
public class Visit
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public required string VisitorName { get; set; }
    public string? Company { get; set; }
    public required string VisitorType { get; set; }     // VisitorRules.Types
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public int? AreaId { get; set; }                     // where they are going
    public LocationArea? Area { get; set; }
    public string? AreaDetail { get; set; }
    public string? HostName { get; set; }                // who they are visiting
    public int? HostUserId { get; set; }
    public string? Purpose { get; set; }
    public string? BadgeColor { get; set; }
    public string? BadgeNumber { get; set; }
    public DateTime InLocal { get; set; }
    public DateTime? ExpectedOutLocal { get; set; }
    public DateTime? OutLocal { get; set; }
    public string? Notes { get; set; }
    public string? CompanionsJson { get; set; }          // List<Companion>
    public string? PhotoStoredName { get; set; }         // C:\DeskData\files\visitors\<id>\...
    public int? ScheduledVisitId { get; set; }
    public int CreatedById { get; set; }
    public int? CheckedOutById { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public DateTime? VoidedUtc { get; set; }
    public string? VoidReason { get; set; }
}

public record Companion(string Name, string? BadgeNumber);

// A visit booked ahead ("Schedule a Visit"); checking the visitor in links it.
public class ScheduledVisit
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public required string VisitorName { get; set; }
    public string? Company { get; set; }
    public required string VisitorType { get; set; }
    public DateTime ExpectedLocal { get; set; }
    public string? HostName { get; set; }
    public int? HostUserId { get; set; }
    public string? Purpose { get; set; }
    public string? Notes { get; set; }
    public int CreatedById { get; set; }
    public DateTime CreatedUtc { get; set; }
    public int? VisitId { get; set; }                    // set when they arrive
    public DateTime? CancelledUtc { get; set; }
}

public static class VisitorRules
{
    public const int AlertHours = 8;                     // MSShift "Visitors Alert": in more than 8 hours
    public const int MaxCompanions = 5;
    public static readonly string[] Types = ["Visitor", "Contractor", "Vendor", "Owner", "Member", "Interview", "Other"];
    public static readonly string[] BadgeColors = ["Red", "Blue", "Green", "Yellow", "White", "Orange"];

    public static bool Overdue(DateTime inLocal, DateTime? expectedOut, DateTime nowLocal) =>
        expectedOut is { } e ? nowLocal > e : nowLocal - inLocal > TimeSpan.FromHours(AlertHours);
}
