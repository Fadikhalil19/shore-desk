namespace Desk.Core.Access;

// A level plus its limits, as stored for a department default or a person.
public record GrantSpec(AccessLevel Level, int? HotelId, DepartmentLimit DepartmentLimit, int? DepartmentId);

// What a person may actually do in one module, after department defaults and
// personal settings are combined.
//   HotelId null      = every hotel
//   DepartmentId null = every department (only matters for department-split modules)
//   Source            = Admin | Person | Department | None (why they have it)
public record EffectiveAccess(
    string Module,
    AccessLevel Level,
    int? HotelId,
    int? DepartmentId,
    string Source,
    string? Note = null)
{
    public bool Allows(AccessLevel needed, int? hotelId = null, int? departmentId = null) =>
        Level >= needed
        && (HotelId is null || hotelId is null || HotelId == hotelId)
        && (DepartmentId is null || departmentId is null || DepartmentId == departmentId);
}

// The access rules in one place, as pure functions so they can be tested:
//  1. Admins (DESK-Admins group) may do everything, everywhere.
//  2. A personal setting for a module replaces the department default for that
//     module completely (so a person can be given more OR less than their team).
//  3. Otherwise the person's department default applies.
//  4. Otherwise: no access (default deny).
public static class AccessRules
{
    public static EffectiveAccess Resolve(ModuleInfo module, bool isAdmin, int? userDepartmentId,
        GrantSpec? personal, GrantSpec? departmentDefault)
    {
        if (isAdmin) return new(module.Key, AccessLevel.Manage, null, null, "Admin");

        var (grant, source) = personal is not null ? (personal, "Person")
            : departmentDefault is not null ? (departmentDefault, "Department")
            : (null, "None");
        if (grant is null || grant.Level == AccessLevel.None)
            return new(module.Key, AccessLevel.None, null, null, source);

        int? department = null;
        if (module.DepartmentScoped)
        {
            switch (grant.DepartmentLimit)
            {
                case DepartmentLimit.Own when userDepartmentId is null:
                    return new(module.Key, AccessLevel.None, null, null, source,
                        "Limited to their own department, but they have no department set.");
                case DepartmentLimit.Own:
                    department = userDepartmentId;
                    break;
                case DepartmentLimit.Specific when grant.DepartmentId is null:
                    return new(module.Key, AccessLevel.None, null, null, source, "Department limit has no department chosen.");
                case DepartmentLimit.Specific:
                    department = grant.DepartmentId;
                    break;
            }
        }
        return new(module.Key, grant.Level, grant.HotelId, department, source);
    }

    // Problems with a grant before it is saved, or null when it is fine.
    public static string? Validate(ModuleInfo module, GrantSpec g)
    {
        if (!Enum.IsDefined(g.Level)) return "Unknown access level.";
        if (!Enum.IsDefined(g.DepartmentLimit)) return "Unknown department limit.";
        if (!module.DepartmentScoped && g.DepartmentLimit != DepartmentLimit.All)
            return $"{module.Name} is not split by department, so it cannot be limited to one.";
        if (g.DepartmentLimit == DepartmentLimit.Specific && g.DepartmentId is null)
            return "Choose the department.";
        if (g.DepartmentLimit != DepartmentLimit.Specific && g.DepartmentId is not null)
            return "A department is only chosen when the limit is 'one department'.";
        return null;
    }
}
