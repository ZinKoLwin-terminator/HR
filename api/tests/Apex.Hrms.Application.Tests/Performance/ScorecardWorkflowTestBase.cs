using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Performance.Periods;
using Apex.Hrms.Application.Performance.Scorecards;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;
using static Apex.Hrms.Application.Tests.Fakes.ReviewFixtures;
using static Apex.Hrms.Application.Tests.Fakes.ScorecardFixtures;

namespace Apex.Hrms.Application.Tests.Performance;

/// <summary>The people, the period and the scorecard the submission and review tests share, and the callers they act as.</summary>
public abstract class ScorecardWorkflowTestBase : IDisposable
{
    private protected TestHost Host { get; } = new();

    public void Dispose()
    {
        Host.Dispose();
        GC.SuppressFinalize(this);
    }

    private protected sealed record World(
        PlanSeed Plan,
        EmployeeDto Anna,
        EmployeeDto Ben,
        EmployeeDto Dan,
        EmployeeDto Hr,
        ScorecardDetailDto Card);

    /// <summary>Anna manages Ben; Dan and Hr report to nobody. Ben's scorecard (A-1 60%, A-2 40%, both with target 100) is in Draft; the clock says 2026-11-09 and the period is Active.</summary>
    private protected async Task<World> SeedAsync(bool hrReview = false, bool activate = true)
    {
        var org = await Host.SeedOrgAsync();
        var category = await Host.CreateCategoryAsync();
        var period = await Host.CreatePeriodAsync(org.Company.Id, hr: hrReview);
        if (activate)
        {
            period = ResultAssert.Succeeded(await Host.MoveAsync<ActivatePeriodCommand>(period));
        }

        var plan = new PlanSeed(org, category, period);
        var anna = await Host.CreateStaffAsync(org, "EMP-0001", first: "Anna", last: "Lead");
        var ben = await Host.CreateStaffAsync(org, "EMP-0002", anna.Id, "Ben", "Report");
        var dan = await Host.CreateStaffAsync(org, "EMP-0003", first: "Dan", last: "Other");
        var hr = await Host.CreateStaffAsync(org, "EMP-0004", first: "Hana", last: "Staff");
        var first = (await Host.CreateTemplateAsync(category.Id, "A-1")).Id;
        var second = (await Host.CreateTemplateAsync(category.Id, "A-2")).Id;
        var card = await Host.AssignedAsync(ben.Id, period.Id, Item(first, weight: 60m), Item(second, weight: 40m));
        card = ResultAssert.Succeeded(await Host.FinalizePlan(card));
        return new World(plan, anna, ben, dan, hr, card);
    }

    private protected void AsEmployee(EmployeeDto employee, int userId)
    {
        Host.User.EmployeeId = employee.Id;
        Host.User.UserId = userId;
        Host.User.DisplayName = $"{employee.FirstName} {employee.LastName}";
        Host.Permissions.Only("kpi:View", "kpi:Create");
        Host.Team.Set([]);
    }

    private protected void AsManager(EmployeeDto employee, int userId, params int[] reports)
    {
        Host.User.EmployeeId = employee.Id;
        Host.User.UserId = userId;
        Host.User.DisplayName = $"{employee.FirstName} {employee.LastName}";
        Host.Permissions.Only("kpi:View", "kpi:Create", "kpi:Approve");
        Host.Team.Set(reports);
    }

    private protected void AsHr(EmployeeDto? employee, int userId)
    {
        Host.User.EmployeeId = employee?.Id;
        Host.User.UserId = userId;
        Host.User.DisplayName = "HR Test";
        Host.Permissions.Only("kpi:View", "kpi:Create", "kpi:Approve", "employees:Edit");
        Host.Team.Set([]);
    }

    private protected static ScorecardItemDto Of(ScorecardDetailDto card, string code) => card.Items.Single(item => item.KpiCode == code);

    private protected Scorecard Stored(int id) => Host.Repository<Apex.Hrms.Domain.Performance.Scorecard>().Items.Single(scorecard => scorecard.Id == id);

    private protected IEnumerable<ReviewAction> HistoryOf(int id) => Host.Repository<ReviewAction>().Items.Where(action => action.ScorecardId == id).OrderBy(action => action.Id);

    private protected async Task<ScorecardDetailDto> FilledAsync(World world, decimal first = 100m, decimal second = 50m)
    {
        AsEmployee(world.Ben, 20);
        return ResultAssert.Succeeded(await Host.SaveResults(Host.Fresh(world.Card), Actual(Of(world.Card, "A-1"), first), Actual(Of(world.Card, "A-2"), second)));
    }

    private protected async Task<ScorecardDetailDto> SubmittedAsync(World world)
    {
        var filled = await FilledAsync(world);
        return ResultAssert.Succeeded(await Host.Submit(filled));
    }

    /// <summary>A scorecard for <paramref name="employee"/> with one KPI of 100% (result 80), planned by the administrator and submitted by the employee (user <paramref name="userId"/>).</summary>
    private protected async Task<ScorecardDetailDto> SubmittedCardAsync(World world, EmployeeDto employee, int userId, string templateCode)
    {
        AsHr(null, 1);
        var template = (await Host.CreateTemplateAsync(world.Plan.Category.Id, templateCode)).Id;
        var card = ResultAssert.Succeeded(await Host.FinalizePlan(await Host.AssignedAsync(employee.Id, world.Plan.Period.Id, Item(template, weight: 100m))));
        AsEmployee(employee, userId);
        var saved = ResultAssert.Succeeded(await Host.SaveResults(card, Actual(card.Items[0], 80m)));
        return ResultAssert.Succeeded(await Host.Submit(saved));
    }


    /// <summary>Moves the seeded period to Completed (and to Closed when asked), as an administrator. Whoever the caller was is replaced by the administrator.</summary>
    private protected async Task MovePeriodAsync(World world, bool close)
    {
        Host.User.EmployeeId = null;
        Host.User.UserId = 1;
        Host.Permissions.Only("kpi:View", "kpi:Create", "kpi:Approve", "employees:Edit", "kpi-setup:Edit");
        var current = ResultAssert.Succeeded(await Host.Get<Apex.Hrms.Application.UseCases.IGetByIdHandler<EvaluationPeriodDto>>().HandleAsync(world.Plan.Period.Id, default));
        var completed = ResultAssert.Succeeded(await Host.MoveAsync<CompletePeriodCommand>(current));
        if (close)
        {
            ResultAssert.Succeeded(await Host.MoveAsync<ClosePeriodCommand>(completed));
        }
    }

    /// <summary>Asks to close the seeded period (which must be Completed), as an administrator. Whoever the caller was is replaced by the administrator.</summary>
    private protected async Task<Result<EvaluationPeriodDto>> ClosePeriodAsync(World world)
    {
        Host.User.EmployeeId = null;
        Host.User.UserId = 1;
        Host.Permissions.Only("kpi:View", "kpi:Create", "kpi:Approve", "employees:Edit", "kpi-setup:Edit");
        var current = ResultAssert.Succeeded(await Host.Get<Apex.Hrms.Application.UseCases.IGetByIdHandler<EvaluationPeriodDto>>().HandleAsync(world.Plan.Period.Id, default));
        return await Host.MoveAsync<ClosePeriodCommand>(current);
    }

    /// <summary>
    /// Closes the seeded Completed period straight in the store, past the close rule (which refuses while scorecards are in review): the
    /// state a period could only reach through direct database work, for the defence-in-depth checks of the scorecard steps.
    /// </summary>
    private protected void ForceClosePeriod(World world) =>
        Assert.True(Host.Repository<EvaluationPeriod>().Items.Single(period => period.Id == world.Plan.Period.Id).Close(scorecardsInReview: 0).IsSuccess);
}
