using Apex.Hrms.Application.Grievances;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Tests.Grievances;

/// <summary>Who sees which case: the submitter, management, investigators and reviewers, and the conflict-of-interest exclusions.</summary>
public sealed class GrievanceScopeTests : GrievanceTestBase
{
    private static PageRequest<GrievanceCaseFilter> All(bool? archived = null) => new(0, 50, null, null, new GrievanceCaseFilter(Archived: archived));

    private static PageRequest<MyGrievanceFilter> Mine() => new(0, 50, null, null, new MyGrievanceFilter());

    [Fact]
    public async Task The_submitter_finds_named_and_anonymous_cases_and_nobody_else_does()
    {
        await SeedAsync();
        var named = await SubmittedAsync();
        var anonymous = await SubmittedAsync(anonymous: true);
        var draft = await DraftAsync(anonymous: true);

        AsSam();
        var mine = ResultAssert.Succeeded(await My.QueryAsync(Mine(), default));
        Assert.Equal(new[] { named.Id, anonymous.Id, draft.Id }.Order(), mine.Items.Select(item => item.Id).Order());
        Assert.True(ResultAssert.Succeeded(await My.GetAsync(anonymous.Id, default)).IsAnonymous);

        AsOlga();
        Assert.Empty(ResultAssert.Succeeded(await My.QueryAsync(Mine(), default)).Items);
        ResultAssert.Failed(await My.GetAsync(named.Id, default), ErrorType.NotFound);
        ResultAssert.Failed(await My.GetAsync(anonymous.Id, default), ErrorType.NotFound);
    }

    [Fact]
    public async Task Management_sees_every_submitted_case_but_never_a_draft()
    {
        await SeedAsync();
        var submitted = await SubmittedAsync(anonymous: true);
        var draft = await DraftAsync();

        AsHana();
        var cases = ResultAssert.Succeeded(await Cases.QueryAsync(All(), default));
        Assert.Equal([submitted.Id], cases.Items.Select(item => item.Id));
        ResultAssert.Failed(await Cases.GetAsync(draft.Id, default), ErrorType.NotFound);
        Assert.Equal(submitted.Id, (await TeamGetAsync(submitted.Id)).Id);
    }

    [Fact]
    public async Task The_subject_of_a_case_never_sees_it_even_with_management_grants()
    {
        await SeedAsync();
        var acknowledged = await AcknowledgedAsync();
        AsHana();
        await Ok(new TriageGrievanceCommand(acknowledged.Id, V(Version(acknowledged.Id)), W.Category.Id, "Medium", acknowledged.TargetResolutionDate, W.Sid.Id, null, false));

        AsSid();
        Assert.Empty(ResultAssert.Succeeded(await Cases.QueryAsync(All(), default)).Items);
        ResultAssert.Failed(await Cases.GetAsync(acknowledged.Id, default), ErrorType.NotFound);
        ResultAssert.Failed(await Cases.HistoryAsync(acknowledged.Id, default), ErrorType.NotFound);
    }

    [Fact]
    public async Task A_case_team_member_never_sees_a_case_they_submitted_named_or_anonymous()
    {
        await SeedAsync();
        var named = await SubmittedAsync();
        var anonymous = await SubmittedAsync(anonymous: true);

        // Sam with management grants: his own cases do not exist for him as case team, only as submitter.
        As(W.Sam, SamUser, "Sam Submitterson", [.. SelfGrants, .. ManagementGrants]);
        Assert.Empty(ResultAssert.Succeeded(await Cases.QueryAsync(All(), default)).Items);
        ResultAssert.Failed(await Cases.GetAsync(named.Id, default), ErrorType.NotFound);
        ResultAssert.Failed(await Cases.GetAsync(anonymous.Id, default), ErrorType.NotFound);
        Assert.Equal(2, ResultAssert.Succeeded(await My.QueryAsync(Mine(), default)).Total);
    }

    [Fact]
    public async Task An_investigator_sees_only_cases_with_an_active_assignment()
    {
        await SeedAsync();
        var assigned = await AssignedAsync();
        var other = await AcknowledgedAsync();

        AsIvan();
        Assert.Equal([assigned.Id], ResultAssert.Succeeded(await Cases.InvestigationsAsync(All(), default)).Items.Select(item => item.Id));
        Assert.True((await TeamGetAsync(assigned.Id)).IsLeadInvestigator);
        ResultAssert.Failed(await Cases.GetAsync(other.Id, default), ErrorType.NotFound);

        AsOlga();
        ResultAssert.Failed(await Cases.GetAsync(assigned.Id, default), ErrorType.NotFound);
        Assert.Empty(ResultAssert.Succeeded(await Cases.InvestigationsAsync(All(), default)).Items);
    }

    [Fact]
    public async Task An_ended_assignment_ends_the_investigators_access()
    {
        await SeedAsync();
        var assigned = await AssignedAsync();
        AsHana();
        var withMember = await Ok(new AssignGrievanceInvestigatorCommand(assigned.Id, V(Version(assigned.Id)), W.Olga.Id, "Member", null));
        var member = withMember.Assignments.Single(assignment => assignment.InvestigatorEmployeeId == W.Olga.Id);

        AsOlga();
        Assert.Equal(assigned.Id, (await TeamGetAsync(assigned.Id)).Id);

        AsHana();
        await Ok(new UnassignGrievanceInvestigatorCommand(assigned.Id, V(Version(assigned.Id)), member.Id, "TST workload"));

        AsOlga();
        ResultAssert.Failed(await Cases.GetAsync(assigned.Id, default), ErrorType.NotFound);
    }

    [Fact]
    public async Task A_reviewer_sees_escalated_cases_pending_proposals_and_cases_they_decided()
    {
        await SeedAsync();
        var plain = await AcknowledgedAsync();
        var critical = await AcknowledgedAsync(priority: "Critical");
        var proposed = await ProposedAsync();

        AsRita();
        ResultAssert.Failed(await Cases.GetAsync(plain.Id, default), ErrorType.NotFound);
        Assert.Equal(critical.Id, (await TeamGetAsync(critical.Id)).Id);
        Assert.Equal(new[] { critical.Id, proposed.Id }.Order(), ResultAssert.Succeeded(await Cases.PendingReviewsAsync(All(), default)).Items.Select(item => item.Id).Order());

        await Ok(new ApproveGrievanceResolutionCommand(proposed.Id, V(Version(proposed.Id)), true, null));
        var decided = await TeamGetAsync(proposed.Id);
        Assert.Equal(["Reviewer"], decided.ViewerRoles);
        Assert.DoesNotContain(proposed.Id, ResultAssert.Succeeded(await Cases.PendingReviewsAsync(All(), default)).Items.Select(item => item.Id));
    }

    [Fact]
    public async Task Archived_cases_are_listed_for_management_only_on_request_and_never_for_investigators()
    {
        await SeedAsync();
        var closed = await ClosedAsync();
        AsHana();
        await Ok(new ArchiveGrievanceCommand(closed.Id, V(Version(closed.Id)), null));

        Assert.DoesNotContain(closed.Id, ResultAssert.Succeeded(await Cases.QueryAsync(All(), default)).Items.Select(item => item.Id));
        Assert.Equal([closed.Id], ResultAssert.Succeeded(await Cases.QueryAsync(All(archived: true), default)).Items.Select(item => item.Id));
        Assert.NotNull((await TeamGetAsync(closed.Id)).ArchivedAtUtc);

        AsIvan();
        ResultAssert.Failed(await Cases.GetAsync(closed.Id, default), ErrorType.NotFound);
        Assert.Empty(ResultAssert.Succeeded(await Cases.InvestigationsAsync(All(archived: true), default)).Items);
    }

    [Fact]
    public async Task The_built_in_administrator_cannot_raise_a_grievance()
    {
        await SeedAsync();
        AsAdmin();

        ResultAssert.Failed(await My.CreateDraftAsync(DraftRequest(W.Category.Id), default), ErrorType.Forbidden);
        Assert.Empty(ResultAssert.Succeeded(await My.QueryAsync(Mine(), default)).Items);
    }

    [Fact]
    public async Task Case_lists_filter_overdue_department_and_anonymity()
    {
        await SeedAsync();
        var named = await AcknowledgedAsync();
        var anonymous = await AcknowledgedAsync(anonymous: true);

        AsHana();
        Host.Clock.Today = Host.Clock.Today.AddDays(30);
        var overdue = ResultAssert.Succeeded(await Cases.QueryAsync(new(0, 50, null, null, new GrievanceCaseFilter(Overdue: true)), default));
        Assert.Equal(2, overdue.Total);
        Assert.All(overdue.Items, item => Assert.True(item.IsOverdue));
        var byDepartment = ResultAssert.Succeeded(await Cases.QueryAsync(new(0, 50, null, null, new GrievanceCaseFilter(DepartmentId: W.Org.Department.Id)), default));
        Assert.Equal([named.Id], byDepartment.Items.Select(item => item.Id));
        var onlyAnonymous = ResultAssert.Succeeded(await Cases.QueryAsync(new(0, 50, null, null, new GrievanceCaseFilter(IsAnonymous: true)), default));
        Assert.Equal([anonymous.Id], onlyAnonymous.Items.Select(item => item.Id));
        ResultAssert.FailedOnField(await Cases.QueryAsync(new(0, 50, new SortState("subject", "asc"), null, new GrievanceCaseFilter()), default), "sort");
        ResultAssert.FailedOnField(await Cases.QueryAsync(new(0, 50, null, null, new GrievanceCaseFilter(Status: "Open")), default), "status");
    }
}
