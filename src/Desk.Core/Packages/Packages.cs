namespace Desk.Core.Packages;

public enum PackageDirection { Incoming, Outgoing }

// Incoming: Waiting -> PickedUp | Delivered | ReturnedToSender
// Outgoing: Waiting (for the carrier) -> Shipped
public enum PackageStatus { Waiting, PickedUp, Delivered, ReturnedToSender, Shipped }

// A package held or sent by the desk. Never deleted.
public class Package
{
    public int Id { get; set; }
    public required string Number { get; set; }         // P2026-0001
    public int HotelId { get; set; }
    public PackageDirection Direction { get; set; }
    public DateTime ReceivedLocal { get; set; }         // incoming: arrived; outgoing: handed to the desk
    public required string Carrier { get; set; }
    public required string PackageType { get; set; }
    public string? TrackingNumber { get; set; }
    public int Count { get; set; } = 1;
    public string? Weight { get; set; }
    public decimal? HandlingFee { get; set; }
    // Recipient (incoming) or sender (outgoing): who at the hotel it belongs to.
    public required string PersonType { get; set; }     // PackageRules.PersonTypes
    public required string PersonName { get; set; }
    public int? PersonUserId { get; set; }
    public string? Room { get; set; }
    public string? Company { get; set; }
    public DateOnly? ArrivalDate { get; set; }
    public DateOnly? DepartureDate { get; set; }
    public string? GroupName { get; set; }              // convention / group
    public string? OtherParty { get; set; }             // incoming: sender; outgoing: addressee
    public string? StorageLocation { get; set; }
    public DateTime? NotifiedLocal { get; set; }
    public string? NotifiedHow { get; set; }
    public int? NotifiedById { get; set; }
    public PackageStatus Status { get; set; }
    public DateTime? ClosedLocal { get; set; }
    public string? ClosedToName { get; set; }           // who picked it up
    public byte[]? Signature { get; set; }
    public int? ClosedById { get; set; }
    public string? Notes { get; set; }
    public bool ForHandover { get; set; }
    public int CreatedById { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

public static class PackageRules
{
    public static readonly string[] Carriers = ["UPS", "FedEx", "USPS", "DHL", "Amazon", "OnTrac", "Courier", "Other"];
    public static readonly string[] Types = ["Box", "Envelope", "Bag", "Tube", "Pallet", "Flowers", "Food", "Other"];
    public static readonly string[] PersonTypes = ["Guest", "Employee", "Department", "Group", "Non Guest", "Other"];
    public static readonly string[] NotifyMethods = ["Phone", "Email", "Text", "Note under door", "In person", "Front desk"];

    // Waiting more than this many days shows as "old" on the list.
    public const int OldAfterDays = 7;
}
