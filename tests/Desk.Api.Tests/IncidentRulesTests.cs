using Desk.Core.Access;
using Desk.Core.Incidents;

namespace Desk.Api.Tests;

public class IncidentRulesTests
{
    [Theory]
    [InlineData(4, Shift.Overnight)]
    [InlineData(5, Shift.Morning)]
    [InlineData(12, Shift.Morning)]
    [InlineData(13, Shift.Afternoon)]
    [InlineData(20, Shift.Afternoon)]
    [InlineData(21, Shift.Overnight)]
    public void Shift_follows_the_clock(int hour, Shift expected) =>
        Assert.Equal(expected, IncidentRules.ShiftFor(new DateTime(2026, 10, 1, hour, 30, 0)));

    [Fact]
    public void Sensitive_forms_have_their_own_modules()
    {
        Assert.Equal(Modules.IncidentsHighLevel, IncidentRules.ModuleFor(IncidentForm.HighLevel));
        Assert.Equal(Modules.IncidentsEmployee, IncidentRules.ModuleFor(IncidentForm.EmployeeInjury));
        Assert.Equal(Modules.Incidents, IncidentRules.ModuleFor(IncidentForm.GuestInjury));
        Assert.Equal(Modules.Incidents, IncidentRules.ModuleFor(IncidentForm.PropertyMissingDamaged));
    }

    [Theory]
    // status, level, writer -> can edit
    [InlineData(IncidentStatus.Draft, AccessLevel.Edit, true, true)]
    [InlineData(IncidentStatus.Draft, AccessLevel.Edit, false, false)]     // someone else's draft
    [InlineData(IncidentStatus.Draft, AccessLevel.View, true, false)]
    [InlineData(IncidentStatus.Draft, AccessLevel.Manage, false, true)]
    [InlineData(IncidentStatus.AwaitingReview, AccessLevel.Edit, true, false)]
    [InlineData(IncidentStatus.AwaitingReview, AccessLevel.Manage, false, true)]
    [InlineData(IncidentStatus.Approved, AccessLevel.Manage, false, false)] // reopen first
    [InlineData(IncidentStatus.Voided, AccessLevel.Manage, false, false)]
    public void Who_may_edit(IncidentStatus status, AccessLevel level, bool writer, bool expected) =>
        Assert.Equal(expected, IncidentRules.CanEdit(status, level, writer));

    [Fact]
    public void Own_report_is_approved_by_someone_else_unless_admin()
    {
        Assert.False(IncidentRules.CanReview(IncidentStatus.AwaitingReview, AccessLevel.Manage, isWriter: true, isAdmin: false));
        Assert.True(IncidentRules.CanReview(IncidentStatus.AwaitingReview, AccessLevel.Manage, isWriter: true, isAdmin: true));
        Assert.True(IncidentRules.CanReview(IncidentStatus.AwaitingReview, AccessLevel.Manage, isWriter: false, isAdmin: false));
        Assert.False(IncidentRules.CanReview(IncidentStatus.Draft, AccessLevel.Manage, isWriter: false, isAdmin: false));
    }

    [Fact]
    public void Injury_reports_need_the_body_part()
    {
        var r = new IncidentReport { Number = "x", Form = IncidentForm.GuestInjury, Heading = "Fall", Kind = "Accident",
            OccurredLocal = DateTime.Now, AreaId = 1, Narrative = "n", SupervisorId = 1 };
        var d = new IncidentDetails { Subjects = [new PersonEntry { LastName = "Doe" }] };
        Assert.Equal(["Body part", "Body part side/area"], IncidentRules.MissingForSubmit(r, d));
        d.Medical = new MedicalEntry { BodyPartId = 1, BodyPartLocationId = 2 };
        Assert.Empty(IncidentRules.MissingForSubmit(r, d));
    }

    [Fact]
    public void Property_report_needs_an_item()
    {
        var r = new IncidentReport { Number = "x", Form = IncidentForm.PropertyMissingDamaged, Heading = "Lost ring", Kind = "Missing",
            OccurredLocal = DateTime.Now, AreaId = 1, Narrative = "n", SupervisorId = 1 };
        Assert.Equal(["At least one item"], IncidentRules.MissingForSubmit(r, new IncidentDetails()));
    }

    [Fact]
    public void Unknown_person_type_is_refused() =>
        Assert.NotNull(IncidentRules.CheckDetails(new IncidentDetails { Witnesses = [new PersonEntry { Type = "Alien" }] }));
}
