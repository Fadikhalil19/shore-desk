using Desk.Core.Data;

namespace Desk.Core.Incidents;

// The five MSShift report forms are one record here; Form says which one.
public enum IncidentForm { Incident, GuestInjury, EmployeeInjury, PropertyMissingDamaged, HighLevel }

// Draft          = being written (also after a supervisor sends it back)
// AwaitingReview = sent to the supervisor
// Approved       = signed off; locked until a manager reopens it
// Voided         = cancelled with a reason (never deleted)
public enum IncidentStatus { Draft, AwaitingReview, Approved, Voided }

public enum Shift { Morning, Afternoon, Overnight }

public class IncidentReport
{
    public int Id { get; set; }
    public required string Number { get; set; }          // 2026-0001, per calendar year
    public IncidentForm Form { get; set; }
    public IncidentStatus Status { get; set; }
    public int HotelId { get; set; }
    public Hotel Hotel { get; set; } = null!;

    // Type: a list entry (Incident / High Level forms) or a fixed kind
    // (Accident / Illness, Missing / Damaged) for the other forms.
    public int? TypeId { get; set; }
    public ListItem? Type { get; set; }
    public string? Kind { get; set; }
    public string? OtherType { get; set; }
    public string Heading { get; set; } = "";

    // Hotel (Pacific) wall-clock times, as the officer typed them.
    public DateTime? OccurredLocal { get; set; }
    public DateTime? ReportedLocal { get; set; }
    public Shift? Shift { get; set; }

    public int? AreaId { get; set; }
    public LocationArea? Area { get; set; }
    public int? LocationId { get; set; }
    public Location? Location { get; set; }
    public string? LocationDetail { get; set; }
    public string? Room { get; set; }

    public string? Narrative { get; set; }               // statement of facts
    public string? FollowUp { get; set; }                // investigation / follow up

    // The optional sections (people, vehicle, police, injury, items, ...) as JSON,
    // shaped by IncidentDetails. PeopleSearch holds their names for searching.
    public string DetailsJson { get; set; } = "{}";
    public string? PeopleSearch { get; set; }

    public int WrittenById { get; set; }
    public AppUser WrittenBy { get; set; } = null!;
    public int? SupervisorId { get; set; }
    public AppUser? Supervisor { get; set; }
    public string? DistributedTo { get; set; }

    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public DateTime? SubmittedUtc { get; set; }
    public int? ReviewedById { get; set; }
    public AppUser? ReviewedBy { get; set; }
    public DateTime? ReviewedUtc { get; set; }
    public string? ReviewNote { get; set; }              // why it was sent back / reopened
    public DateTime? VoidedUtc { get; set; }
    public string? VoidReason { get; set; }

    public List<IncidentAttachment> Attachments { get; set; } = [];
}

// Photos, video and documents on a report. The file itself is under
// C:\DeskData\files\incidents\<report id>\. Removing one only hides it.
public class IncidentAttachment
{
    public int Id { get; set; }
    public int IncidentId { get; set; }
    public required string FileName { get; set; }        // as uploaded
    public required string StoredName { get; set; }      // random name on disk
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public string? Caption { get; set; }
    public int UploadedById { get; set; }
    public AppUser UploadedBy { get; set; } = null!;
    public DateTime UploadedUtc { get; set; }
    public DateTime? RemovedUtc { get; set; }
    public int? RemovedById { get; set; }
}
