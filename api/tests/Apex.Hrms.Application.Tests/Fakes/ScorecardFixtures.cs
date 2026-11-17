using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Performance.Categories;
using Apex.Hrms.Application.Performance.Periods;
using Apex.Hrms.Application.Performance.Scorecards;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Tests.Fakes;

internal sealed record PlanSeed(OrgSeed Org, KpiCategoryDto Category, EvaluationPeriodDto Period);

/// <summary>Seeding and the handler calls of the scorecard tests. The fake clock says today is 2026-11-09; the seeded period is 2026-10-01 to 2026-12-31.</summary>
internal static class ScorecardFixtures
{
    /// <summary>An organization, a category and an Active period (or a Draft one).</summary>
    public static async Task<PlanSeed> SeedPlanAsync(this TestHost host, bool activate = true)
    {
        var org = await host.SeedOrgAsync();
        var category = await host.CreateCategoryAsync();
        var period = await host.CreatePeriodAsync(org.Company.Id);
        if (activate)
        {
            period = ResultAssert.Succeeded(await host.MoveAsync<ActivatePeriodCommand>(period));
        }

        return new PlanSeed(org, category, period);
    }

    public static async Task<EmployeeDto> CreateStaffAsync(this TestHost host, OrgSeed org, string code, int? managerId = null, string first = "Mia", string last = "Hartley") =>
        await host.CreateEmployeeAsync(EmployeeFixtures.NewEmployee(org, code, $"{code.ToLowerInvariant()}@example.test") with { FirstName = first, LastName = last, ManagerId = managerId });

    public static AssignmentItemRequest Item(int templateId, decimal? target = null, decimal? weight = null, DateOnly? start = null, DateOnly? end = null, int? sortOrder = null) =>
        new(templateId, target, weight, start, end, sortOrder);

    public static Task<Result<AssignmentResult>> Assign(this TestHost host, int employeeId, int periodId, params AssignmentItemRequest[] items) =>
        host.Get<IAssignKpiHandler>().HandleAsync(new AssignKpiRequest(employeeId, periodId, items), default);

    public static async Task<ScorecardDetailDto> AssignedAsync(this TestHost host, int employeeId, int periodId, params AssignmentItemRequest[] items) =>
        ResultAssert.Succeeded(await host.Assign(employeeId, periodId, items)).Scorecard;

    public static Task<Result<BulkAssignmentResult>> AssignBulk(this TestHost host, BulkAssignKpiRequest request) =>
        host.Get<IBulkAssignKpiHandler>().HandleAsync(request, default);

    public static BulkAssignKpiRequest BulkOf(int periodId, int[] employeeIds, params AssignmentItemRequest[] items) =>
        new(periodId, employeeIds, null, null, null, null, items);

    public static Task<Result<ScorecardDetailDto>> FinalizePlan(this TestHost host, ScorecardDetailDto scorecard, string? rowVersion = null) =>
        host.Command<FinalizePlanCommand, ScorecardDetailDto>(new FinalizePlanCommand(scorecard.Id, rowVersion ?? Requests.Version(scorecard.RowVersion)));

    public static Task<Result<ScorecardDetailDto>> GetScorecard(this TestHost host, int id) =>
        host.Get<IGetByIdHandler<ScorecardDetailDto>>().HandleAsync(id, default);

    public static Task<Result<PagedResult<ScorecardDto>>> QueryScorecards(
        this TestHost host,
        ScorecardFilter? filter = null,
        string? search = null,
        string? sort = null,
        string direction = "asc",
        int pageIndex = 0,
        int pageSize = 50) =>
        host.Get<IQueryHandler<ScorecardDto, ScorecardFilter>>().HandleAsync(
            new PageRequest<ScorecardFilter>(pageIndex, pageSize, sort is null ? null : new SortState(sort, direction), search, filter ?? new ScorecardFilter()),
            default);

    public static ScorecardItemUpdateRequest ItemUpdateOf(
        ScorecardItemDto item,
        decimal? target = null,
        decimal? weight = null,
        DateOnly? start = null,
        DateOnly? end = null,
        int? sortOrder = null,
        string? rowVersion = null) => new(
        target ?? item.Target,
        weight ?? item.Weight,
        start ?? item.StartDate,
        end ?? item.EndDate,
        sortOrder ?? item.SortOrder,
        rowVersion ?? Requests.Version(item.RowVersion));

    public static Task<Result<ScorecardItemDto>> UpdateItem(this TestHost host, int itemId, ScorecardItemUpdateRequest request) =>
        host.Get<IUpdateHandler<ScorecardItemUpdateRequest, ScorecardItemDto>>().HandleAsync(itemId, request, default);

    public static Task<Result> DeleteItem(this TestHost host, ScorecardItemDto item, string? rowVersion = null) =>
        host.Get<IDeleteHandler<ScorecardItemDto>>().HandleAsync(item.Id, rowVersion ?? Requests.Version(item.RowVersion), default);

    public static Task<Result> DeleteScorecard(this TestHost host, ScorecardDetailDto scorecard, string? rowVersion = null) =>
        host.Get<IDeleteHandler<ScorecardDto>>().HandleAsync(scorecard.Id, rowVersion ?? Requests.Version(scorecard.RowVersion), default);
}
