using Apex.Hrms.Application.Grievances;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Grievances;

namespace Apex.Hrms.Application.Tests.Grievances;

/// <summary>HR records a grievance for an employee (<c>POST /api/grievances/cases</c>). Fictional data only.</summary>
public sealed class GrievanceIntakeTests : GrievanceTestBase
{
    private IGrievanceIntake Intake => Host.Get<IGrievanceIntake>();

    private GrievanceIntakeRequest Request(int? employeeId = null, string? receivedVia = "InPerson", bool declaration = true) =>
        new(
            employeeId ?? W.Sam.Id,
            W.Category.Id,
            "TST Repeated unfair shift changes",
            "TST My shifts are changed without notice every single week.",
            null,
            "TST Floor 2",
            null,
            null,
            receivedVia,
            declaration);

    [Fact]
    public async Task Management_records_a_submitted_named_case_that_the_employee_sees_as_their_own()
    {
        await SeedAsync();
        AsHana();

        var recorded = ResultAssert.Succeeded(await Intake.RecordAsync(Request(receivedVia: "Phone"), default));

        Assert.Equal("Submitted", recorded.Status);
        Assert.True(GrievanceCase.IsValidCaseNo(recorded.CaseNo));
        var stored = Stored(recorded.Id);
        Assert.False(stored.IsAnonymous);
        Assert.NotNull(stored.DepartmentId);

        var entry = Assert.Single(Host.Repository<GrievanceStatusHistoryEntry>().Items, item => item.CaseId == recorded.Id);
        Assert.Equal(GrievanceHistoryAction.Submit, entry.Action);
        Assert.Equal(GrievanceActorRole.Management, entry.ActorRole);
        Assert.Equal(HanaUser, entry.ActorUserId);
        Assert.Equal("Recorded by HR on behalf of the employee (received by phone).", entry.Reason);
        Assert.True(entry.VisibleToSubmitter);

        var logged = Audit.Entries.Last();
        Assert.Equal(GrievanceAuditAction.StatusChange, logged.Action);
        Assert.Equal("intake:Draft>Submitted", logged.Detail);

        Assert.Contains(Notifications.Sent, sent => sent.UserId == SamUser && sent.Content.Link == $"/grievances/my/{recorded.Id}");
        Assert.DoesNotContain(Notifications.Sent, sent => sent.UserId == HanaUser);

        AsSam();
        var mine = ResultAssert.Succeeded(await My.GetAsync(recorded.Id, default));
        Assert.Equal(recorded.CaseNo, mine.CaseNo);
        Assert.False(mine.IsAnonymous);
        var shown = Assert.Single(mine.Timeline);
        Assert.Equal(("Submit", "HR team"), (shown.Action, shown.Actor));
        Assert.Equal("Recorded by HR on behalf of the employee (received by phone).", shown.Reason);
    }

    [Fact]
    public async Task Hr_raises_their_own_grievance_under_my_grievances()
    {
        await SeedAsync();
        AsHana();

        ResultAssert.FailedOnField(await Intake.RecordAsync(Request(W.Hana.Id), default), "employeeId", "Use My Grievances to raise your own grievance.");
        Assert.Empty(Host.Repository<GrievanceCase>().Items);
    }

    [Fact]
    public async Task An_unknown_employee_is_refused()
    {
        await SeedAsync();
        AsHana();

        ResultAssert.FailedOnField(await Intake.RecordAsync(Request(99_999), default), "employeeId", "Choose an active employee.");
    }

    [Fact]
    public async Task Without_management_create_the_intake_is_forbidden()
    {
        await SeedAsync();
        As(W.Hana, HanaUser, "Hana Manager", [.. SelfGrants, "grievance-management:View", "grievance-management:Edit"]);

        ResultAssert.Failed(await Intake.RecordAsync(Request(), default), ErrorType.Forbidden);
        Assert.Empty(Host.Repository<GrievanceCase>().Items);
    }

    [Fact]
    public async Task The_channel_and_the_declaration_are_required_and_nothing_is_created()
    {
        await SeedAsync();
        AsHana();

        ResultAssert.FailedOnField(await Intake.RecordAsync(Request(receivedVia: "Fax"), default), "receivedVia");
        ResultAssert.FailedOnField(await Intake.RecordAsync(Request(declaration: false), default), "declarationAccepted");
        ResultAssert.FailedOnField(
            await Intake.RecordAsync(Request() with { Description = "TST too short" }, default), "description");
        Assert.Empty(Host.Repository<GrievanceCase>().Items);
    }
}
