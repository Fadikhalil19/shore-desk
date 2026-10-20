namespace Desk.Core.Spaces;

// Lockers and parking spaces work the same way: a numbered space, vacant or
// assigned to one person, with the history of who had it.
public enum SpaceKind { Locker, Parking }

public class Space
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public SpaceKind Kind { get; set; }
    public required string Section { get; set; }        // Lockers: Men's / Women's / Admin; Parking: Employee / Motorcycle
    public required string Number { get; set; }
    public string? LockCode { get; set; }               // lockers; shown only to people who may assign
    public string? LockSerial { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public int? CurrentAssignmentId { get; set; }
}

public class SpaceAssignment
{
    public int Id { get; set; }
    public int SpaceId { get; set; }
    public Space Space { get; set; } = null!;
    public int HotelId { get; set; }
    public int? UserId { get; set; }
    public required string Name { get; set; }
    public int? DepartmentId { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly? ToDate { get; set; }               // set when released
    // Parking only.
    public string? VehicleMake { get; set; }
    public string? VehicleModel { get; set; }
    public string? VehicleYear { get; set; }
    public string? VehicleColor { get; set; }
    public string? Plate { get; set; }
    public string? PlateState { get; set; }
    public string? StickerNumber { get; set; }
    public string? Notes { get; set; }
    public int AssignedById { get; set; }
    public int? ReleasedById { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public static class SpaceRules
{
    public static string Module(SpaceKind kind) => kind == SpaceKind.Locker ? Access.Modules.Lockers : Access.Modules.Parking;

    public static string[] Sections(SpaceKind kind) => kind == SpaceKind.Locker
        ? ["Men's", "Women's", "Admin"]
        : ["Employee", "Motorcycle", "Visitor", "Reserved"];
}
