using System.Text.Json;
using System.Text.Json.Serialization;

namespace Desk.Core.Incidents;

// The optional sections of a report, stored as JSON on IncidentReport.DetailsJson.
// A section that is present (or a list that is not empty) is a "Yes" on the form.
// Ids point at ListItem rows (injury type, colour, ...), Users or Departments.
// Unknown fields sent by a browser are dropped when the JSON is read back in.
public class IncidentDetails
{
    public PersonEntry? ReportedBy { get; set; }
    public List<PersonEntry> Subjects { get; set; } = [];     // first = main subject
    public List<PersonEntry> Victims { get; set; } = [];      // High Level
    public List<PersonEntry> Witnesses { get; set; } = [];
    public VehicleEntry? Vehicle { get; set; }
    public List<LockEntry> LockInterrogation { get; set; } = [];
    public MediaEntry? Media { get; set; }
    public EvidenceEntry? Evidence { get; set; }
    public ClaimEntry? Claim { get; set; }
    public PoliceEntry? Police { get; set; }
    public MedicalEntry? Medical { get; set; }                // guest / employee accident or illness
    public List<PropertyItem> PropertyItems { get; set; } = [];
    public string? WrittenByOther { get; set; }
    public string? SupervisorOther { get; set; }

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static IncidentDetails Parse(string? json) =>
        string.IsNullOrWhiteSpace(json) ? new() : JsonSerializer.Deserialize<IncidentDetails>(json, Json) ?? new();

    public string ToJson() => JsonSerializer.Serialize(this, Json);
}

public static class PersonTypes
{
    public const string Guest = "Guest", NonGuest = "Non Guest", Employee = "Employee",
        HotelProperty = "Hotel Property", Pedestrian = "Pedestrian", NotApplicable = "N/A";
    public static readonly string[] All = [Guest, NonGuest, Employee, HotelProperty, Pedestrian, NotApplicable];
}

public class PersonEntry
{
    public string? Type { get; set; }                  // PersonTypes
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public int? UserId { get; set; }                   // an employee who uses Shore Desk
    public int? DepartmentId { get; set; }
    public string? Position { get; set; }
    public string? EmployeeNumber { get; set; }
    public DateOnly? DateOfHire { get; set; }
    public string? Room { get; set; }
    public DateOnly? ArrivalDate { get; set; }
    public DateOnly? DepartureDate { get; set; }
    public string? Company { get; set; }               // group / company
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? HotelProperty { get; set; }         // when the subject is hotel property
    public Address? Home { get; set; }
    public Address? Work { get; set; }
    public PersonDescription? Description { get; set; } // High Level subject / victim
}

public class Address
{
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Zip { get; set; }
    public string? Country { get; set; }
}

public class PersonDescription
{
    public string? Race { get; set; }
    public string? Height { get; set; }
    public string? Weight { get; set; }
    public string? Eyes { get; set; }
    public string? Hair { get; set; }
    public string? Clothing { get; set; }
    public string? LicenseNumber { get; set; }
    public string? LicenseState { get; set; }
    public string? Marks { get; set; }                 // scars, marks, tattoos
}

public class VehicleEntry
{
    public int? CircumstanceId { get; set; }
    public string? OtherCircumstance { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public string? Year { get; set; }
    public int? ColorId { get; set; }
    public string? Plate { get; set; }
    public string? PlateState { get; set; }
    public string? Vin { get; set; }
    public string? ValetTicket { get; set; }
    public string? OperatorName { get; set; }
    public DateOnly? OperatorDateOfBirth { get; set; }
    public string? OperatorLicense { get; set; }
    public string? OperatorPhone { get; set; }
    public string? OwnerName { get; set; }
    public string? OwnerAddress { get; set; }
    public string? OwnerPhone { get; set; }
    public string? InsuranceCompany { get; set; }
    public string? RentalCompany { get; set; }
    public string? Damage { get; set; }                // nature and extent of damage
    public string? Description { get; set; }
}

public class LockEntry
{
    public string? UsedByType { get; set; }            // Guest / Employee
    public string? KeyName { get; set; }
    public DateTime? UsedLocal { get; set; }
    public int? DepartmentId { get; set; }
    public int? UserId { get; set; }
    public string? Name { get; set; }
}

public class MediaEntry
{
    public int? PictureCount { get; set; }
    public DateTime? PicturesTakenLocal { get; set; }
    public string? TakenBy { get; set; }
    public DateTime? VideoLocal { get; set; }
    public string? VideoDescription { get; set; }
}

public class EvidenceEntry
{
    public int? StatusId { get; set; }
    public string? Items { get; set; }
    public string? StoredLocation { get; set; }
    public DateOnly? Date { get; set; }
    public string? Disposition { get; set; }
}

public class ClaimEntry
{
    public DateTime? ReportedLocal { get; set; }
    public string? ClaimNumber { get; set; }
}

public class PoliceEntry
{
    public DateTime? CalledLocal { get; set; }
    public string? Agency { get; set; }
    public string? OfficerName { get; set; }
    public string? Shield { get; set; }
    public string? CaseNumber { get; set; }
}

public class MedicalEntry
{
    public string? Detail { get; set; }                // "Incident only" / "Medical"
    public int? InjuryTypeId { get; set; }
    public string? OtherInjury { get; set; }
    public int? BodyPartId { get; set; }
    public int? BodyPartLocationId { get; set; }
    public bool EmsCalled { get; set; }
    public DateTime? EmsArrivedLocal { get; set; }
    public string? EmsName { get; set; }
    public string? EmsNumber { get; set; }
    public string? Hospital { get; set; }
    public bool RefusedMedicalAttention { get; set; }
    public int? ContributingFactorId { get; set; }
    public string? OtherFactor { get; set; }
    public DateTime? ConsumedLocal { get; set; }       // food / drink illness
    public string? Symptoms { get; set; }
    public bool? SafetyShoes { get; set; }             // employee reports
    public string? SafetyShoesBrand { get; set; }
    public DateOnly? SafetyShoesPurchased { get; set; }
}

public class PropertyItem
{
    public int? CategoryId { get; set; }
    public string? Detail { get; set; }
    public string? Description { get; set; }
    public int? ColorId { get; set; }
    public string? Brand { get; set; }
    public string? ModelSerial { get; set; }
    public decimal? ClaimValue { get; set; }
}
