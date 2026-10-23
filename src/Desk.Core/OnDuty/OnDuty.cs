using Desk.Core.Incidents;

namespace Desk.Core.OnDuty;

// Someone on a shift's roster (MSShift "On Duty"): who, radio, post, start and end.
// A person who does not use Shore Desk is entered by name. Removing only hides.
public class DutyAssignment
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public DateOnly ShiftDate { get; set; }
    public Shift Shift { get; set; }
    public int? UserId { get; set; }
    public string? Name { get; set; }
    public int? DepartmentId { get; set; }
    public string? Radio { get; set; }
    public string? Post { get; set; }
    public DateTime StartLocal { get; set; }
    public DateTime EndLocal { get; set; }
    public int CreatedById { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public DateTime? RemovedUtc { get; set; }
    public string? LegacyRef { get; set; }               // set on rows brought over from MSShift ("MSShift OD#123")
}

// Someone calling out of a shift (sick call log).
public class CallOut
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public DateOnly ShiftDate { get; set; }
    public Shift Shift { get; set; }
    public int? UserId { get; set; }
    public string? Name { get; set; }
    public int? DepartmentId { get; set; }
    public DateTime CalledLocal { get; set; }           // when they called
    public required string Reason { get; set; }         // OnDutyRules.Reasons
    public string? Notes { get; set; }
    public string? CoveredBy { get; set; }
    public int CreatedById { get; set; }                // who took the call
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public DateTime? RemovedUtc { get; set; }
    public string? LegacyRef { get; set; }               // set on rows brought over from MSShift ("MSShift OD#123")
}

public static class OnDutyRules
{
    // Reasons only; no medical details are asked for or kept.
    public static readonly string[] Reasons = ["Sick", "Family emergency", "Personal", "Transportation", "No call / no show", "Other"];

    public static string? CheckTimes(DateOnly date, Shift shift, DateTime start, DateTime end)
    {
        if (end <= start) return "The end time must be after the start time.";
        if (end - start > TimeSpan.FromHours(16)) return "A shift cannot be longer than 16 hours.";
        var (from, to) = Handover.HandoverRules.Window(date, shift);
        if (start < from.AddHours(-8) || start > to.AddHours(2)) return "The start time does not fit this shift.";
        return null;
    }
}
