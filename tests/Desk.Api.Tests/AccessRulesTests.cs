using Desk.Core.Access;

namespace Desk.Api.Tests;

public class AccessRulesTests
{
    static readonly ModuleInfo Keys = Modules.Find(Modules.Keys)!;
    static readonly ModuleInfo Inventory = Modules.Find(Modules.Inventory)!;
    const int Safety = 18, Engineering = 4, Sho = 1;

    static GrantSpec G(AccessLevel l, int? hotel = null, DepartmentLimit d = DepartmentLimit.All, int? dept = null) => new(l, hotel, d, dept);

    [Fact]
    public void Nothing_set_means_no_access() =>
        Assert.Equal(AccessLevel.None, AccessRules.Resolve(Keys, false, Safety, null, null).Level);

    [Fact]
    public void Admin_manages_everything_everywhere()
    {
        var a = AccessRules.Resolve(Inventory, true, null, G(AccessLevel.None), null);
        Assert.Equal((AccessLevel.Manage, (int?)null, (int?)null, "Admin"), (a.Level, a.HotelId, a.DepartmentId, a.Source));
    }

    [Fact]
    public void Department_default_applies()
    {
        var a = AccessRules.Resolve(Keys, false, Safety, null, G(AccessLevel.Edit));
        Assert.Equal((AccessLevel.Edit, "Department"), (a.Level, a.Source));
    }

    [Fact]
    public void Personal_setting_can_give_less_than_the_department()
    {
        var a = AccessRules.Resolve(Keys, false, Safety, G(AccessLevel.None), G(AccessLevel.Edit));
        Assert.Equal((AccessLevel.None, "Person"), (a.Level, a.Source));
    }

    [Fact]
    public void Personal_setting_can_give_more_than_the_department() =>
        Assert.Equal(AccessLevel.Manage, AccessRules.Resolve(Keys, false, Safety, G(AccessLevel.Manage), G(AccessLevel.View)).Level);

    [Fact]
    public void Own_department_limit_follows_the_person()
    {
        var a = AccessRules.Resolve(Inventory, false, Engineering, null, G(AccessLevel.Edit, d: DepartmentLimit.Own));
        Assert.Equal(Engineering, a.DepartmentId);
        Assert.True(a.Allows(AccessLevel.Edit, departmentId: Engineering));
        Assert.False(a.Allows(AccessLevel.View, departmentId: Safety));
    }

    [Fact]
    public void Own_department_limit_without_a_department_gives_nothing() =>
        Assert.Equal(AccessLevel.None, AccessRules.Resolve(Inventory, false, null, G(AccessLevel.Edit, d: DepartmentLimit.Own), null).Level);

    [Fact]
    public void Department_limit_is_ignored_on_modules_not_split_by_department() =>
        Assert.Null(AccessRules.Resolve(Keys, false, Safety, G(AccessLevel.Edit, d: DepartmentLimit.Own), null).DepartmentId);

    [Fact]
    public void Hotel_limit_is_kept()
    {
        var a = AccessRules.Resolve(Keys, false, Safety, G(AccessLevel.View, hotel: Sho), null);
        Assert.True(a.Allows(AccessLevel.View, hotelId: Sho));
        Assert.False(a.Allows(AccessLevel.View, hotelId: 2));
        Assert.False(a.Allows(AccessLevel.Edit, hotelId: Sho));
    }

    [Fact]
    public void Department_limit_on_a_module_not_split_by_department_is_refused() =>
        Assert.NotNull(AccessRules.Validate(Keys, G(AccessLevel.Edit, d: DepartmentLimit.Own)));

    [Fact]
    public void Specific_department_needs_a_department() =>
        Assert.NotNull(AccessRules.Validate(Inventory, G(AccessLevel.Edit, d: DepartmentLimit.Specific)));

    [Fact]
    public void Every_module_key_is_unique_and_findable()
    {
        Assert.Equal(Modules.All.Count, Modules.All.Select(m => m.Key).Distinct().Count());
        Assert.All(Modules.All, m => Assert.Same(m, Modules.Find(m.Key.ToUpperInvariant())));
    }
}
