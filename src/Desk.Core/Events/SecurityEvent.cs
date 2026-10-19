using Desk.Core.Access;
using Desk.Core.Data;
using Desk.Core.Incidents;

namespace Desk.Core.Events;

public enum Priority { Low, Medium, High, Highest }

// One entry in the daily security log (MSShift "Event"). Open until it has a
// completed time. Times are Pacific wall-clock times as the officer typed them.
public class SecurityEvent
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public Hotel Hotel { get; set; } = null!;

    public int CategoryId { get; set; }                 // ListKeys.EventCategory
    public ListItem Category { get; set; } = null!;
    public string? Detail { get; set; }

    public int? AreaId { get; set; }
    public LocationArea? Area { get; set; }
    public int? LocationId { get; set; }
    public Location? Location { get; set; }
    public string? LocationDetail { get; set; }
    public string? Room { get; set; }

    // Who asked (a guest at the desk, a housekeeper on the radio, ...).
    public string? RequestedByType { get; set; }        // PersonTypes + Visitor
    public string? RequestedByName { get; set; }
    public int? RequestedByUserId { get; set; }
    public int? RequestedByDepartmentId { get; set; }
    public DateTime RequestedLocal { get; set; }

    // Who or what it is about.
    public string? RelatesToType { get; set; }
    public string? RelatesToName { get; set; }
    public string? RelatesToRoom { get; set; }

    public Priority Priority { get; set; }
    public int? AssignedDepartmentId { get; set; }
    public int? AssignedToId { get; set; }
    public AppUser? AssignedTo { get; set; }
    public DateTime StartedLocal { get; set; }
    public DateTime? CompletedLocal { get; set; }
    public Shift Shift { get; set; }

    public string? Narrative { get; set; }

    public int CreatedById { get; set; }
    public AppUser CreatedBy { get; set; } = null!;
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public DateTime? VoidedUtc { get; set; }
    public string? VoidReason { get; set; }
}

// A task an officer must not forget ("check the pool gate at 22:00"). Shown on the
// event log until done; a daily one comes back the next day.
public class EventReminder
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public required string Title { get; set; }
    public string? Notes { get; set; }
    public DateTime DueLocal { get; set; }
    public bool RepeatsDaily { get; set; }
    public int? AssignedDepartmentId { get; set; }
    public int CreatedById { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? LastDoneUtc { get; set; }
    public int? LastDoneById { get; set; }
    public DateTime? ClosedUtc { get; set; }            // done (one-off) or cancelled
}

public static class EventRules
{
    public static readonly string[] RequesterTypes = [.. PersonTypes.All.Where(t => t != PersonTypes.Pedestrian), "Visitor"];

    // Creator or assignee may change an open or closed event; a manager may change any.
    public static bool CanEdit(bool voided, AccessLevel level, bool isCreatorOrAssignee) =>
        !voided && (level >= AccessLevel.Manage || (level >= AccessLevel.Edit && isCreatorOrAssignee));

    // Anyone on the team (Edit) may close an open event, e.g. the next shift.
    public static bool CanComplete(bool voided, bool open, AccessLevel level) => !voided && open && level >= AccessLevel.Edit;

    public static bool CanVoid(bool voided, AccessLevel level) => !voided && level >= AccessLevel.Manage;

    public static string? Check(SecurityEvent e)
    {
        if (e.CompletedLocal is { } done && done < e.StartedLocal.AddMinutes(-1)) return "It cannot be completed before it started.";
        if (e.StartedLocal < e.RequestedLocal.AddDays(-1)) return "Check the request and start times.";
        if (e.RequestedByType is { } t && !RequesterTypes.Contains(t)) return $"Unknown requester type '{t}'.";
        if (e.RelatesToType is { } r && !RequesterTypes.Contains(r)) return $"Unknown type '{r}'.";
        return null;
    }

    // Next time a daily reminder is due after it was done now.
    public static DateTime NextDue(DateTime due, DateTime nowLocal)
    {
        var next = due;
        while (next <= nowLocal) next = next.AddDays(1);
        return next;
    }
}
