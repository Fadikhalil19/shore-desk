using Desk.Core.Access;
using Desk.Core.Incidents;

namespace Desk.Core.Keys;

public enum KeyStatus { In, SignedOut, Missing, Broken }

// A physical key or key ring held by security (Officer Ring 1, Master Cabinet 2, ...).
// Never deleted: switched off when it leaves service.
public class HotelKey
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public required string Name { get; set; }
    public string? Barcode { get; set; }                 // what a scanner types; unique per hotel
    public string? Description { get; set; }            // what it opens
    public KeyStatus Status { get; set; }
    public int? CurrentCheckoutId { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

// One sign-out (and its sign-in). Signatures are small PNGs drawn with a finger.
public class KeyCheckout
{
    public int Id { get; set; }
    public int KeyId { get; set; }
    public HotelKey Key { get; set; } = null!;
    public int HotelId { get; set; }
    public required string ToType { get; set; }          // KeyRules.HolderTypes
    public int? UserId { get; set; }
    public required string Name { get; set; }
    public string? Company { get; set; }                 // contractor
    public int? DepartmentId { get; set; }
    public DateTime OutLocal { get; set; }
    public DateTime? DueBackLocal { get; set; }
    public int OutById { get; set; }
    public byte[]? OutSignature { get; set; }
    public string? OutNotes { get; set; }
    public DateTime? InLocal { get; set; }
    public int? InById { get; set; }
    public byte[]? InSignature { get; set; }
    public string? InNotes { get; set; }
    public DateTime CreatedUtc { get; set; }
}

// Missing / broken / back in service, with who and why.
public class KeyStatusChange
{
    public int Id { get; set; }
    public int KeyId { get; set; }
    public int HotelId { get; set; }
    public KeyStatus From { get; set; }
    public KeyStatus To { get; set; }
    public string? Note { get; set; }
    public int ById { get; set; }
    public DateTime AtLocal { get; set; }
}

// A shift key count: every active key ticked as on hand or not.
public class KeyCount
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public DateOnly ShiftDate { get; set; }
    public Shift Shift { get; set; }
    public int CountedById { get; set; }
    public DateTime CountedUtc { get; set; }
    public string? Notes { get; set; }
    public List<KeyCountLine> Lines { get; set; } = [];
}

public class KeyCountLine
{
    public int Id { get; set; }
    public int KeyCountId { get; set; }
    public int KeyId { get; set; }
    public KeyStatus ExpectedStatus { get; set; }        // what the app thought at the time
    public bool OnHand { get; set; }
    public string? Note { get; set; }
}

public static class KeyRules
{
    public const int AlertHours = 10;                    // MSShift "Keys Alert": out more than 10 hours
    public const int MaxSignatureBytes = 200_000;
    public static readonly string[] HolderTypes = ["Employee", "Contractor", "Vendor", "Other"];

    public static bool Overdue(DateTime outLocal, DateTime? dueBack, DateTime nowLocal) =>
        dueBack is { } due ? nowLocal > due : nowLocal - outLocal > TimeSpan.FromHours(AlertHours);

    // Officers (Edit) report a key missing or broken; only a manager puts it back in service.
    public static string? CheckStatusChange(KeyStatus from, KeyStatus to, AccessLevel level)
    {
        if (from == to) return "The key already has that status.";
        if (to == KeyStatus.SignedOut || from == KeyStatus.SignedOut && to == KeyStatus.In)
            return "Use sign out / sign in for that.";
        if (to == KeyStatus.In && level < AccessLevel.Manage) return "Only a manager can put a key back in service.";
        return level >= AccessLevel.Edit ? null : "You cannot change keys.";
    }

    // A signature must really be a PNG image (checked by its first bytes) and small.
    public static bool IsPng(byte[] b) =>
        b.Length is > 8 and <= MaxSignatureBytes && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47;
}
