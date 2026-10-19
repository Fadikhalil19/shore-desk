using Desk.Core.Incidents;

namespace Desk.Core.Handover;

// A note from one shift to the next (MSShift "general comments & notifications").
// A shift is a hotel + shift date + Morning/Afternoon/Overnight. Removing only hides.
public class HandoverNote
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public DateOnly ShiftDate { get; set; }
    public Shift Shift { get; set; }
    public required string Text { get; set; }
    public int CreatedById { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public DateTime? RemovedUtc { get; set; }
    public int? RemovedById { get; set; }
}

// "I have read this handover", by someone on the incoming shift.
public class HandoverRead
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public DateOnly ShiftDate { get; set; }
    public Shift Shift { get; set; }
    public int UserId { get; set; }
    public DateTime ReadUtc { get; set; }
}

public static class HandoverRules
{
    // The shift a moment belongs to. Overnight runs 21:00 to 05:00 and belongs to the
    // date it started on, so 02:00 on the 5th is the Overnight shift of the 4th.
    public static (DateOnly Date, Shift Shift) ShiftOf(DateTime local)
    {
        var shift = IncidentRules.ShiftFor(local);
        var date = DateOnly.FromDateTime(local);
        if (shift == Shift.Overnight && local.Hour < 5) date = date.AddDays(-1);
        return (date, shift);
    }

    // Start (inclusive) and end (exclusive) of a shift, in hotel time.
    public static (DateTime Start, DateTime End) Window(DateOnly date, Shift shift)
    {
        var day = date.ToDateTime(TimeOnly.MinValue);
        return shift switch
        {
            Shift.Morning => (day.AddHours(5), day.AddHours(13)),
            Shift.Afternoon => (day.AddHours(13), day.AddHours(21)),
            _ => (day.AddHours(21), day.AddDays(1).AddHours(5)),
        };
    }

    // The shift before this one (the one that hands over to it).
    public static (DateOnly Date, Shift Shift) Previous(DateOnly date, Shift shift) => shift switch
    {
        Shift.Morning => (date.AddDays(-1), Shift.Overnight),
        Shift.Afternoon => (date, Shift.Morning),
        _ => (date, Shift.Afternoon),
    };
}
