using Desk.Core.Access;
using Desk.Core.Data;

namespace Desk.Core.LostFound;

// InStorage -> ReturnedToOwner (in person) | Shipped | Disposed
public enum FoundStatus { InStorage, ReturnedToOwner, Shipped, Disposed }
public enum LostStatus { Open, Matched, Closed }
public enum PaymentStatus { Waiting, Paid, NoCharge }

// An item found on property and kept by security. Never deleted.
public class FoundItem
{
    public int Id { get; set; }
    public required string Number { get; set; }          // F2026-0001
    public int HotelId { get; set; }
    public int? CategoryId { get; set; }                 // ListKeys.PropertyCategory
    public string? Detail { get; set; }
    public string? Description { get; set; }
    public int? ColorId { get; set; }                    // ListKeys.Color
    public string? Brand { get; set; }
    public string? ModelSerial { get; set; }
    public bool HighValue { get; set; }                  // cash, jewelry, phones, passports: kept in the safe
    public DateTime FoundLocal { get; set; }
    public int? AreaId { get; set; }
    public LocationArea? Area { get; set; }
    public string? LocationDetail { get; set; }
    public string? Room { get; set; }
    public string? FoundByType { get; set; }             // Guest / Employee / ...
    public string? FoundByName { get; set; }
    public int? FoundByUserId { get; set; }
    public int? FoundByDepartmentId { get; set; }
    public string? StorageLocation { get; set; }         // shelf / bin / safe
    public DateOnly PurgeDate { get; set; }
    public FoundStatus Status { get; set; }
    public string? OwnerName { get; set; }               // when known (guest in that room, name tag)
    public string? OwnerContact { get; set; }
    public string? Notes { get; set; }
    public string? PhotoStoredName { get; set; }         // C:\DeskData\files\lostfound\<id>\...
    // Closing: returned in person, shipped (see Shipment) or disposed.
    public DateTime? ClosedLocal { get; set; }
    public int? ClosedById { get; set; }
    public string? ReturnedToName { get; set; }
    public byte[]? ReturnSignature { get; set; }
    public string? DisposalMethod { get; set; }          // LostFoundRules.DisposalMethods
    public string? CloseNote { get; set; }
    public bool ForHandover { get; set; }
    public int CreatedById { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

// A guest (or anyone) reporting something lost.
public class LostReport
{
    public int Id { get; set; }
    public required string Number { get; set; }          // L2026-0001
    public int HotelId { get; set; }
    public DateTime ReportedLocal { get; set; }
    public required string OwnerName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Room { get; set; }
    public DateOnly? ArrivalDate { get; set; }
    public DateOnly? DepartureDate { get; set; }
    public int? CategoryId { get; set; }
    public string? Description { get; set; }
    public int? ColorId { get; set; }
    public string? Brand { get; set; }
    public string? LostWhere { get; set; }
    public DateOnly? LostFrom { get; set; }               // when it was lost, as a range
    public DateOnly? LostTo { get; set; }
    public LostStatus Status { get; set; }
    public int? FoundItemId { get; set; }
    public string? Notes { get; set; }
    public bool ForHandover { get; set; }
    public int CreatedById { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

// Sending a found item to its owner. Payment is only tracked as a status: the
// app never takes or stores card details (guests pay through a link or at the desk).
public class Shipment
{
    public int Id { get; set; }
    public int FoundItemId { get; set; }
    public FoundItem FoundItem { get; set; } = null!;
    public int HotelId { get; set; }
    public required string RecipientName { get; set; }
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Zip { get; set; }
    public string? Country { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Carrier { get; set; }
    public string? TrackingNumber { get; set; }
    public decimal? Cost { get; set; }
    public PaymentStatus Payment { get; set; }
    public string? PaymentNote { get; set; }             // e.g. "paid by link 10/21" - never card numbers
    public DateTime? ShippedLocal { get; set; }
    public DateTime? CancelledUtc { get; set; }
    public int CreatedById { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

public static class LostFoundRules
{
    public const int DefaultKeepDays = 90;
    public static readonly string[] FinderTypes = ["Guest", "Employee", "Non Guest", "Contractor", "Other"];
    public static readonly string[] DisposalMethods = ["Donated", "Discarded", "Given to finder", "Destroyed", "Turned over to police", "Other"];
    public static readonly string[] Carriers = ["UPS", "FedEx", "USPS", "DHL", "Other"];

    // Card numbers must never be stored: refuse any text with a run of 13-19 digits
    // (spaces or dashes between them allowed).
    public static bool LooksLikeCardNumber(string? text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        var digits = 0;
        foreach (var ch in text)
        {
            if (char.IsDigit(ch)) { if (++digits >= 13) return true; }
            else if (ch is not (' ' or '-')) digits = 0;
        }
        return false;
    }

    // How well a found item fits a lost report; 0 = not a candidate.
    public static int MatchScore(LostReport lost, FoundItem found)
    {
        if (found.Status != FoundStatus.InStorage || found.HotelId != lost.HotelId) return 0;
        var score = 0;
        if (lost.CategoryId is int c && found.CategoryId == c) score += 4;
        if (lost.ColorId is int col && found.ColorId == col) score += 2;
        if (!string.IsNullOrWhiteSpace(lost.Brand) && string.Equals(lost.Brand.Trim(), found.Brand?.Trim(), StringComparison.OrdinalIgnoreCase)) score += 3;
        if (!string.IsNullOrWhiteSpace(lost.Room) && string.Equals(lost.Room.Trim(), found.Room?.Trim(), StringComparison.OrdinalIgnoreCase)) score += 3;
        var day = DateOnly.FromDateTime(found.FoundLocal);
        var from = lost.LostFrom ?? lost.ArrivalDate;
        var to = lost.LostTo ?? lost.DepartureDate?.AddDays(2);
        if (from is { } f && to is { } t && day >= f.AddDays(-1) && day <= t.AddDays(3)) score += 2;
        else if (from is { } f2 && day < f2.AddDays(-1)) return 0;            // found before it was lost
        score += SharedWords(lost.Description, $"{found.Detail} {found.Description} {found.Brand}");
        return score >= 4 ? score : 0;
    }

    static int SharedWords(string? a, string b)
    {
        static HashSet<string> Words(string? s) => (s ?? "").ToLowerInvariant()
            .Split([' ', ',', '.', '/', '-', '(', ')'], StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length >= 4).ToHashSet();
        return Math.Min(3, Words(a).Intersect(Words(b)).Count());
    }

    // Kept until the purge date; after that a manager disposes of it (with method and reason).
    public static bool CanDispose(FoundItem item, AccessLevel level, DateOnly today) =>
        item.Status == FoundStatus.InStorage && level >= AccessLevel.Manage && today >= item.PurgeDate;
}
