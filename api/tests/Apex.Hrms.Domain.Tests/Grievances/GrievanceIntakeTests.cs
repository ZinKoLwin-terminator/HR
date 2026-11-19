using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Grievances;

namespace Apex.Hrms.Domain.Tests.Grievances;

/// <summary>HR records a grievance for an employee (<see cref="GrievanceCase.RecordForEmployee"/>). Fictional data only.</summary>
public sealed class GrievanceIntakeTests
{
    private static readonly DateOnly Today = GrievanceScenario.Today;
    private static readonly DateTime Now = GrievanceScenario.Now;
    private static readonly string CaseNo = GrievanceCase.FormatCaseNo(2026, 7);

    private static GrievanceCase NamedDraft() => GrievanceScenario.NewDraft().Case;

    [Fact]
    public void The_intake_channel_texts()
    {
        GrievanceIntakeChannel[] values = Enum.GetValues<GrievanceIntakeChannel>();
        Assert.Equal(["InPerson", "Phone", "Email", "Letter", "Other"], values.Select(GrievanceText.IntakeChannel.ToText));
        Assert.False(GrievanceText.IntakeChannel.TryParse("inperson", out _));
        Assert.False(GrievanceText.IntakeChannel.TryParse("0", out _));
    }

    [Fact]
    public void Management_records_a_submitted_named_case_with_a_visible_entry_naming_the_hr_user()
    {
        var grievance = NamedDraft();

        var step = grievance.RecordForEmployee(
            GrievanceScenario.Category(), true, CaseNo, GrievanceScenario.Placement, GrievanceIntakeChannel.Phone, GrievanceScenario.Management, Today, Now);

        Assert.True(step.IsSuccess);
        Assert.Equal(GrievanceStatus.Submitted, grievance.Status);
        Assert.Equal(CaseNo, grievance.CaseNo);
        Assert.Equal(Now, grievance.SubmittedAtUtc);
        Assert.Equal(Now, grievance.DeclarationAcceptedAtUtc);
        Assert.Equal((1, 2, 3, 4), (grievance.CompanyId, grievance.BranchId, grievance.DepartmentId, grievance.PositionId));

        var entry = step.Value.Entry;
        Assert.Equal(GrievanceHistoryAction.Submit, entry.Action);
        Assert.Equal((GrievanceStatus.Draft, GrievanceStatus.Submitted), (entry.FromStatus, entry.ToStatus));
        Assert.Equal(GrievanceActorRole.Management, entry.ActorRole);
        Assert.Equal(GrievanceScenario.Management.UserId, entry.ActorUserId);
        Assert.Equal("TST HR Manager", entry.ActorName);
        Assert.Equal("Recorded by HR on behalf of the employee (received by phone).", entry.Reason);
        Assert.True(entry.VisibleToSubmitter);
    }

    [Theory]
    [InlineData(GrievanceIntakeChannel.InPerson, "in person")]
    [InlineData(GrievanceIntakeChannel.Email, "by email")]
    [InlineData(GrievanceIntakeChannel.Letter, "by letter")]
    [InlineData(GrievanceIntakeChannel.Other, "another way")]
    public void The_reason_says_how_the_grievance_was_received(GrievanceIntakeChannel channel, string how) =>
        Assert.Equal($"Recorded by HR on behalf of the employee (received {how}).", GrievanceCase.IntakeReason(channel));

    [Fact]
    public void Recording_needs_the_full_submission_rules_and_the_declaration()
    {
        var unfinished = GrievanceCase.CreateDraft(
            GrievanceScenario.DraftDetails() with { Subject = "Shif", Description = "Too short" }, GrievanceScenario.Category(), Today).Value;

        var step = unfinished.RecordForEmployee(
            GrievanceScenario.Category(), false, CaseNo, GrievanceScenario.Placement, GrievanceIntakeChannel.InPerson, GrievanceScenario.Management, Today, Now);

        Assert.True(step.IsFailure);
        Assert.Equal(["declarationAccepted", "description", "subject"], step.Error.FieldErrors!.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(GrievanceStatus.Draft, unfinished.Status);
        Assert.Null(unfinished.CaseNo);
    }

    [Fact]
    public void An_inactive_category_is_refused()
    {
        var grievance = NamedDraft();

        var step = grievance.RecordForEmployee(
            GrievanceScenario.Category(active: false), true, CaseNo, GrievanceScenario.Placement, GrievanceIntakeChannel.InPerson, GrievanceScenario.Management, Today, Now);

        Assert.True(step.IsFailure);
        Assert.Contains("categoryId", step.Error.FieldErrors!.Keys);
    }

    [Fact]
    public void Only_a_draft_is_recorded()
    {
        var scenario = GrievanceScenario.Reach(new ScenarioOptions(GrievanceStatus.Submitted));

        var step = scenario.Case.RecordForEmployee(
            GrievanceScenario.Category(), true, CaseNo, GrievanceScenario.Placement, GrievanceIntakeChannel.InPerson, GrievanceScenario.Management, Today, Now);

        Assert.True(step.IsFailure);
        Assert.Equal(ErrorType.Conflict, step.Error.Type);
    }

    [Fact]
    public void Only_a_signed_in_management_user_records_and_never_an_anonymous_case()
    {
        Assert.Throws<ArgumentException>(() => NamedDraft().RecordForEmployee(
            GrievanceScenario.Category(), true, CaseNo, GrievanceScenario.Placement, GrievanceIntakeChannel.InPerson, GrievanceScenario.Submitter, Today, Now));
        Assert.Throws<ArgumentException>(() => NamedDraft().RecordForEmployee(
            GrievanceScenario.Category(), true, CaseNo, GrievanceScenario.Placement, GrievanceIntakeChannel.InPerson, GrievanceScenario.Investigator, Today, Now));

        var anonymous = GrievanceScenario.NewDraft(isAnonymous: true).Case;
        Assert.Throws<InvalidOperationException>(() => anonymous.RecordForEmployee(
            GrievanceScenario.Category(), true, CaseNo, GrievanceScenario.Placement, GrievanceIntakeChannel.InPerson, GrievanceScenario.Management, Today, Now));
    }
}
