using Desk.Core.Access;

namespace Desk.Core.Data;

// Lists below are never hard-deleted: they are switched off (IsActive = false)
// so old reports keep pointing at the right names.

public class Hotel
{
    public int Id { get; set; }
    public required string Code { get; set; }   // SHO, SMH, OVH
    public required string Name { get; set; }
    public string? Address { get; set; }        // printed on report headers
    public bool IsActive { get; set; } = true;
}

public class Department
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
}

// Locations have two levels, as in MSShift: an area ("A Building 1st Floor")
// and the spots inside it ("Room 101", "Ice machine"). An area with no hotel
// applies to every hotel (e.g. "Guest Room").
public class LocationArea
{
    public int Id { get; set; }
    public int? HotelId { get; set; }
    public Hotel? Hotel { get; set; }
    public required string Name { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public List<Location> Locations { get; set; } = [];
}

public class Location
{
    public int Id { get; set; }
    public int AreaId { get; set; }
    public LocationArea Area { get; set; } = null!;
    public required string Name { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class AppUser
{
    public int Id { get; set; }
    public Guid EntraObjectId { get; set; }
    public required string DisplayName { get; set; }
    public string? Email { get; set; }
    public string? JobTitle { get; set; }

    // Their team and home hotel. The department decides their default access.
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public int? HotelId { get; set; }
    public Hotel? Hotel { get; set; }

    public DateTime FirstSeenUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }

    // In DESK-Admins at their last sign-in (Entra decides; this copy lets the app list
    // admins, e.g. as report supervisors, while they are not signed in).
    public bool IsAdmin { get; set; }

    // First real Microsoft sign-in (people can be added before they ever sign in).
    // The scheduler emails the admins once, then sets FirstSignInNotifiedUtc.
    public DateTime? FirstSignInUtc { get; set; }
    public DateTime? FirstSignInNotifiedUtc { get; set; }

    // Admin switched this person off (Settings > People): sign-in refused and any open
    // session ends. Removed = also hidden from the list and personal access cleared; history is kept.
    public DateTime? BlockedUtc { get; set; }
    public DateTime? RemovedUtc { get; set; }
}

// Access for one module: a department default, or a personal setting that
// replaces the default for that module. One row per holder and module.
public abstract class AccessGrantBase
{
    public int Id { get; set; }
    public required string Module { get; set; }
    public AccessLevel Level { get; set; }
    public int? LimitHotelId { get; set; }           // null = every hotel
    public DepartmentLimit DepartmentLimit { get; set; }
    public int? LimitDepartmentId { get; set; }      // only with DepartmentLimit.Specific
    public DateTime UpdatedUtc { get; set; }

    public GrantSpec ToSpec() => new(Level, LimitHotelId, DepartmentLimit, LimitDepartmentId);
}

public class DepartmentAccess : AccessGrantBase
{
    public int DepartmentId { get; set; }
}

public class UserAccess : AccessGrantBase
{
    public int AppUserId { get; set; }
}

public class ActivityLog
{
    public long Id { get; set; }
    public DateTime Utc { get; set; }
    public int? UserId { get; set; }
    public required string UserName { get; set; }
    public required string Action { get; set; }     // View, Create, Edit, Download, SignIn, Denied, ...
    public string? Module { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? Details { get; set; }            // JSON, e.g. before/after values
    public string? IpAddress { get; set; }
}
