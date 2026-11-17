using Apex.Hrms.Application.Performance.Scorecards;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Domain.Common;
using static Apex.Hrms.Application.Tests.Fakes.ScorecardFixtures;

namespace Apex.Hrms.Application.Tests.Performance;

/// <summary>
/// Separation of duties on the plan: HR never plans their own scorecard. Every plan write refuses the caller's own employee (409) and the
/// bulk assignment skips it, while the built-in administrator (no linked employee) and plans of other employees are unaffected.
/// </summary>
public sealed class ScorecardOwnPlanTests : IDisposable
{
    private const string OwnPlan = "You cannot plan your own scorecard.";

    private readonly TestHost host = new();

    public void Dispose() => host.Dispose();

    private sealed record Seed(PlanSeed Plan, int Hr, int Other, int First, int Second, ScorecardDetailDto Own);

    /// <summary>Hana works in HR (EMP-0001) and Ben does not (EMP-0002). The administrator assigned Hana's own scorecard (A-1, 60%), still in Planning.</summary>
    private async Task<Seed> SeedAsync()
    {
        var plan = await host.SeedPlanAsync();
        var hr = await host.CreateStaffAsync(plan.Org, "EMP-0001", first: "Hana", last: "Staff");
        var other = await host.CreateStaffAsync(plan.Org, "EMP-0002", first: "Ben", last: "Report");
        var first = (await host.CreateTemplateAsync(plan.Category.Id, "A-1")).Id;
        var second = (await host.CreateTemplateAsync(plan.Category.Id, "A-2")).Id;
        var own = await host.AssignedAsync(hr.Id, plan.Period.Id, Item(first, weight: 60m));
        return new Seed(plan, hr.Id, other.Id, first, second, own);
    }

    /// <summary>Signs in as the HR person: every grant of the host, linked to their own employee record.</summary>
    private void AsHrPerson(Seed seed)
    {
        host.User.EmployeeId = seed.Hr;
        host.User.UserId = 30;
    }

    private void AsAdministrator()
    {
        host.User.EmployeeId = null;
        host.User.UserId = 1;
    }

    private int ItemCountOf(int scorecardId) => host.Repository<Apex.Hrms.Domain.Performance.ScorecardItem>().Items.Count(item => item.ScorecardId == scorecardId);

    [Fact]
    public async Task Assigning_kpis_to_ones_own_scorecard_is_a_409_and_writes_nothing_while_another_employee_is_assigned()
    {
        var seed = await SeedAsync();
        AsHrPerson(seed);

        var own = ResultAssert.Failed(await host.Assign(seed.Hr, seed.Plan.Period.Id, Item(seed.Second, weight: 40m)), ErrorType.Conflict);
        var other = await host.Assign(seed.Other, seed.Plan.Period.Id, Item(seed.Second, weight: 40m));

        Assert.Equal(OwnPlan, own.Message);
        Assert.Equal(1, ItemCountOf(seed.Own.Id));
        Assert.True(other.IsSuccess);
    }

    [Fact]
    public async Task A_new_own_scorecard_cannot_be_created_either()
    {
        var plan = await host.SeedPlanAsync();
        var hr = await host.CreateStaffAsync(plan.Org, "EMP-0001");
        var template = (await host.CreateTemplateAsync(plan.Category.Id, "A-1")).Id;
        host.User.EmployeeId = hr.Id;

        Assert.Equal(OwnPlan, ResultAssert.Failed(await host.Assign(hr.Id, plan.Period.Id, Item(template, weight: 100m)), ErrorType.Conflict).Message);
        Assert.Empty(host.Repository<Apex.Hrms.Domain.Performance.Scorecard>().Items);
    }

    [Fact]
    public async Task Changing_or_removing_a_kpi_of_ones_own_scorecard_is_a_409_and_changes_nothing()
    {
        var seed = await SeedAsync();
        var item = seed.Own.Items[0];
        AsHrPerson(seed);

        var updated = ResultAssert.Failed(await host.UpdateItem(item.Id, ItemUpdateOf(item, weight: 100m)), ErrorType.Conflict);
        var deleted = ResultAssert.Failed(await host.DeleteItem(item), ErrorType.Conflict);

        Assert.Equal((OwnPlan, OwnPlan), (updated.Message, deleted.Message));
        var stored = host.Repository<Apex.Hrms.Domain.Performance.ScorecardItem>().Items.Single(row => row.Id == item.Id);
        Assert.Equal(60m, stored.Weight);
    }

    [Fact]
    public async Task Deleting_or_finalizing_ones_own_plan_is_a_409_and_the_plan_stays_in_planning()
    {
        var seed = await SeedAsync();
        AsAdministrator();
        ResultAssert.Succeeded(await host.UpdateItem(seed.Own.Items[0].Id, ItemUpdateOf(seed.Own.Items[0], weight: 100m)));
        var own = host.Fresh(seed.Own);
        AsHrPerson(seed);

        var deleted = ResultAssert.Failed(await host.DeleteScorecard(own), ErrorType.Conflict);
        var finalized = ResultAssert.Failed(await host.FinalizePlan(own), ErrorType.Conflict);

        Assert.Equal((OwnPlan, OwnPlan), (deleted.Message, finalized.Message));
        Assert.Equal(Apex.Hrms.Domain.Performance.ScorecardStatus.Planning, host.Repository<Apex.Hrms.Domain.Performance.Scorecard>().Items.Single(card => card.Id == own.Id).Status);
    }

    [Fact]
    public async Task The_bulk_assignment_skips_the_callers_own_employee_by_id_or_by_filter_and_assigns_the_others()
    {
        var seed = await SeedAsync();
        AsHrPerson(seed);

        var listed = ResultAssert.Succeeded(await host.AssignBulk(BulkOf(seed.Plan.Period.Id, [seed.Hr, seed.Other], Item(seed.Second, weight: 40m))));
        var byDepartment = ResultAssert.Succeeded(await host.AssignBulk(
            new BulkAssignKpiRequest(seed.Plan.Period.Id, null, seed.Plan.Org.Department.Id, null, null, null, [Item(seed.Second, weight: 10m)])));

        Assert.Equal([seed.Other], listed.Assigned.Select(entry => entry.EmployeeId));
        var skipped = Assert.Single(listed.Skipped);
        Assert.Equal((seed.Hr, "EMP-0001", OwnPlan), (skipped.EmployeeId, skipped.EmployeeCode, skipped.Reason));
        Assert.Contains(byDepartment.Skipped, entry => entry.EmployeeId == seed.Hr && entry.Reason == OwnPlan);
        Assert.DoesNotContain(byDepartment.Assigned, entry => entry.EmployeeId == seed.Hr);
        Assert.Equal(1, ItemCountOf(seed.Own.Id));
    }

    [Fact]
    public async Task The_built_in_administrator_with_no_employee_plans_every_scorecard()
    {
        var seed = await SeedAsync();
        AsAdministrator();

        var added = ResultAssert.Succeeded(await host.Assign(seed.Hr, seed.Plan.Period.Id, Item(seed.Second, weight: 40m)));
        var finalized = ResultAssert.Succeeded(await host.FinalizePlan(added.Scorecard));

        Assert.Equal("Draft", finalized.Status);
    }

    [Fact]
    public async Task Another_hr_person_plans_the_hr_persons_scorecard()
    {
        var seed = await SeedAsync();
        host.User.EmployeeId = seed.Other;
        host.User.UserId = 31;

        var item = ResultAssert.Succeeded(await host.UpdateItem(seed.Own.Items[0].Id, ItemUpdateOf(seed.Own.Items[0], weight: 100m)));
        var finalized = ResultAssert.Succeeded(await host.FinalizePlan(host.Fresh(seed.Own)));

        Assert.Equal(100m, item.Weight);
        Assert.Equal("Draft", finalized.Status);
    }
}
