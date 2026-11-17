using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Performance;
using FluentValidation;

namespace Apex.Hrms.Application.Performance.Scorecards;

/// <summary>
/// Every write to the KPI plan of a scorecard (assign, change or remove a KPI, delete the scorecard, finalize the plan) runs
/// one at a time per evaluation period under this application lock (<c>kpi-plan:{periodId}</c>), taken for the length of its
/// transaction. A check made after the lock sees every earlier change of that period, so a finalize can never miss a KPI that is added
/// at the same moment, and two assignments cannot both pass the "total weight is at most 100%" check. Every rule a plan write checks
/// stays inside one period (a scorecard belongs to one period and never moves), so writes for different periods do not wait for each
/// other. HR writes are short and rare; the wait is at most 15 seconds, then 409 <see cref="BusyMessage"/>.
/// </summary>
internal static class PlanLock
{
    public const string BusyMessage = "Another change to KPI plans is in progress. Try again.";

    public static string NameFor(int periodId) => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"kpi-plan:{periodId}");

    public static ApplicationLock For(int periodId) => new(NameFor(periodId), BusyMessage);

    /// <summary>
    /// The lock of the period a plan write works in, or none when the record is gone (<paramref name="periodId"/> null): the write then
    /// answers its 400 or 404 without changing anything.
    /// </summary>
    public static ApplicationLock? ForPeriod(int? periodId) => periodId is { } id ? For(id) : null;
}

/// <summary>Loads what an assignment checks, in set-based queries: never one query per employee.</summary>
internal sealed class AssignmentLoader(
    IRepository<EvaluationPeriod> periods,
    IRepository<KpiTemplate> templates,
    IRepository<Scorecard> scorecards,
    IRepository<ScorecardItem> items,
    IReadModel read)
{
    public const int MaxEmployees = BulkAssignKpiRequestValidator.MaxEmployees;

    public async Task<Result<EvaluationPeriod>> FindPeriodAsync(int periodId, CancellationToken cancellationToken)
    {
        var period = await periods.FindAsync(periodId, cancellationToken);
        return period is null
            ? Result.Failure<EvaluationPeriod>(AppError.Validation(
                $"Evaluation period #{periodId} was not found.",
                "periodId",
                "Choose an existing evaluation period."))
            : Result.Success(period);
    }

    public async Task<Result<IReadOnlyList<PreparedItem>>> PrepareAsync(
        IReadOnlyList<AssignmentItemRequest> requested,
        EvaluationPeriod period,
        CancellationToken cancellationToken)
    {
        var ids = requested.Select(item => item.TemplateId).Distinct().ToArray();
        var found = await templates.ListAsync(template => ids.Contains(template.Id), cancellationToken);
        return AssignmentRules.Prepare(requested, period, found.ToDictionary(template => template.Id));
    }

    public Task<AssignmentEmployee?> EmployeeAsync(int employeeId, CancellationToken cancellationToken) =>
        read.FirstOrDefaultAsync(Project(read.Query<Employee>().Where(employee => employee.Id == employeeId)), cancellationToken);

    public Task<List<AssignmentEmployee>> EmployeesAsync(IReadOnlyCollection<int> employeeIds, CancellationToken cancellationToken)
    {
        var ids = employeeIds.ToArray();
        return read.ToListAsync(Project(read.Query<Employee>().Where(employee => ids.Contains(employee.Id))), cancellationToken);
    }

    /// <summary>
    /// The current employees of the period's company that match every filter given, at most <see cref="MaxEmployees"/> + 1 of them
    /// (one more than the cap, so the caller can tell that the filters match too many).
    /// </summary>
    public Task<List<AssignmentEmployee>> EmployeesMatchingAsync(BulkAssignKpiRequest request, int companyId, CancellationToken cancellationToken)
    {
        var resigned = EmploymentStatus.Resigned;
        var terminated = EmploymentStatus.Terminated;
        var query = read.Query<Employee>().Where(employee =>
            employee.CompanyId == companyId && employee.ExitDate == null && employee.Status != resigned && employee.Status != terminated);
        if (request.DepartmentId is { } departmentId)
        {
            query = query.Where(employee => employee.DepartmentId == departmentId);
        }

        if (request.BranchId is { } branchId)
        {
            query = query.Where(employee => employee.BranchId == branchId);
        }

        if (request.PositionId is { } positionId)
        {
            query = query.Where(employee => employee.PositionId == positionId);
        }

        if (request.JobGradeId is { } jobGradeId)
        {
            query = query.Where(employee => employee.JobGradeId == jobGradeId);
        }

        return read.ToListAsync(Project(query).Take(MaxEmployees + 1), cancellationToken);
    }

    /// <summary>The scorecards the employees already have in the period, with their items: two queries for any number of employees.</summary>
    public async Task<Dictionary<int, ExistingScorecard>> ExistingAsync(int periodId, IReadOnlyCollection<int> employeeIds, CancellationToken cancellationToken)
    {
        var ids = employeeIds.ToArray();
        var found = await scorecards.ListAsync(scorecard => scorecard.PeriodId == periodId && ids.Contains(scorecard.EmployeeId), cancellationToken);
        if (found.Count == 0)
        {
            return [];
        }

        var scorecardIds = found.Select(scorecard => scorecard.Id).ToArray();
        var existingItems = (await items.ListAsync(item => scorecardIds.Contains(item.ScorecardId), cancellationToken))
            .ToLookup(item => item.ScorecardId);
        return found.ToDictionary(
            scorecard => scorecard.EmployeeId,
            scorecard => new ExistingScorecard(scorecard.Id, scorecard.Status, [.. existingItems[scorecard.Id]]));
    }

    private static IQueryable<AssignmentEmployee> Project(IQueryable<Employee> employees) =>
        employees.OrderBy(employee => employee.Id).Select(employee => new AssignmentEmployee(
            employee.Id,
            employee.EmployeeCode,
            employee.CompanyId,
            employee.BranchId,
            employee.DepartmentId,
            employee.PositionId,
            employee.ManagerEmployeeId,
            employee.Status,
            employee.ExitDate));
}

internal sealed record WrittenPlan(AssignmentEmployee Employee, int ScorecardId, bool Created, int ItemsAdded);

/// <summary>
/// Writes the plans inside the transaction the caller opened: first the new scorecards (their ids are needed by the items), then
/// every item, each with the snapshot of its template. The caller commits.
/// </summary>
internal sealed class AssignmentWriter(
    IRepository<Scorecard> scorecards,
    IRepository<ScorecardItem> items,
    IUnitOfWork unitOfWork,
    IClock clock,
    ICurrentUser user)
{
    public async Task<Result<IReadOnlyList<WrittenPlan>>> WriteAsync(
        EvaluationPeriod period,
        IReadOnlyList<EmployeePlan> plans,
        IReadOnlyList<PreparedItem> prepared,
        CancellationToken cancellationToken)
    {
        var created = new Dictionary<int, Scorecard>();
        foreach (var plan in plans.Where(plan => plan.Existing is null))
        {
            var employee = plan.Employee;
            var scorecard = Scorecard.Create(employee.Id, period.Id, employee.CompanyId, employee.BranchId, employee.DepartmentId, employee.PositionId, reviewerEmployeeId: null);
            if (scorecard.IsFailure)
            {
                return Result.Failure<IReadOnlyList<WrittenPlan>>(scorecard.Error);
            }

            scorecards.Add(scorecard.Value);
            created[employee.Id] = scorecard.Value;
        }

        if (created.Count > 0)
        {
            var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result.Failure<IReadOnlyList<WrittenPlan>>(saved.Error);
            }
        }

        var written = new List<WrittenPlan>(plans.Count);
        foreach (var plan in plans)
        {
            var scorecardId = plan.Existing?.Id ?? created[plan.Employee.Id].Id;
            for (var position = 0; position < prepared.Count; position++)
            {
                var item = prepared[position];
                var details = ScorecardItemDetails.FromTemplate(
                    item.Template,
                    scorecardId,
                    item.Target,
                    item.Weight,
                    item.StartDate,
                    item.EndDate,
                    AssignmentRules.SortOrderFor(item, plan.Existing, position),
                    user.UserId,
                    clock.Today);
                var newItem = ScorecardItem.Create(details);
                if (newItem.IsFailure)
                {
                    return Result.Failure<IReadOnlyList<WrittenPlan>>(newItem.Error);
                }

                items.Add(newItem.Value);
            }

            written.Add(new WrittenPlan(plan.Employee, scorecardId, plan.Existing is null, prepared.Count));
        }

        var itemsSaved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return itemsSaved.IsFailure
            ? Result.Failure<IReadOnlyList<WrittenPlan>>(itemsSaved.Error)
            : Result.Success<IReadOnlyList<WrittenPlan>>(written);
    }
}

/// <summary>
/// Assigns KPIs to one employee: creates the employee's scorecard for the period (status Planning, with the employee's placement
/// copied now) or adds the KPIs to the one in Planning. The check order is: request shape, period, employee (and not the caller's own
/// employee: 409), templates, then the scorecard.
/// </summary>
internal sealed class AssignKpiHandler(
    IValidator<AssignKpiRequest> validator,
    AssignmentLoader loader,
    AssignmentWriter writer,
    ScorecardReader reader,
    ICurrentUser user,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork) : IAssignKpiHandler
{
    public async Task<Result<AssignmentResult>> HandleAsync(AssignKpiRequest request, CancellationToken cancellationToken)
    {
        var valid = await validator.CheckAsync(request, cancellationToken);
        if (valid.IsFailure)
        {
            return Result.Failure<AssignmentResult>(valid.Error);
        }

        var begun = await unitOfWork.BeginTransactionAsync(PlanLock.For(request.PeriodId), cancellationToken);
        if (begun.IsFailure)
        {
            return Result.Failure<AssignmentResult>(begun.Error);
        }

        int scorecardId;
        bool created;
        await using (var transaction = begun.Value)
        {
            var period = await loader.FindPeriodAsync(request.PeriodId, cancellationToken);
            if (period.IsFailure)
            {
                return Result.Failure<AssignmentResult>(period.Error);
            }

            var open = AssignmentRules.EnsureOpen(period.Value);
            if (open.IsFailure)
            {
                return Result.Failure<AssignmentResult>(open.Error);
            }

            var employee = await loader.EmployeeAsync(request.EmployeeId, cancellationToken);
            if (employee is null)
            {
                return Result.Failure<AssignmentResult>(AppError.Validation(
                    $"Employee #{request.EmployeeId} was not found.",
                    "employeeId",
                    "Choose an existing employee."));
            }

            var notOwn = AssignmentRules.EnsureNotOwnPlan(user.EmployeeId, employee.Id);
            if (notOwn.IsFailure)
            {
                return Result.Failure<AssignmentResult>(notOwn.Error);
            }

            var eligible = AssignmentRules.CheckEmployee(employee, period.Value);
            if (eligible.IsFailure)
            {
                return Result.Failure<AssignmentResult>(eligible.Error);
            }

            var prepared = await loader.PrepareAsync(request.Items!, period.Value, cancellationToken);
            if (prepared.IsFailure)
            {
                return Result.Failure<AssignmentResult>(prepared.Error);
            }

            var existing = (await loader.ExistingAsync(period.Value.Id, [employee.Id], cancellationToken)).GetValueOrDefault(employee.Id);
            var placed = AssignmentRules.CheckPlacement(employee, existing, prepared.Value);
            if (placed.IsFailure)
            {
                return Result.Failure<AssignmentResult>(placed.Error);
            }

            activity.Record(ScorecardActivity.Category, ScorecardActivity.Assigned(prepared.Value.Count, employee.EmployeeCode, period.Value.Name));
            var written = await writer.WriteAsync(period.Value, [new EmployeePlan(employee, existing)], prepared.Value, cancellationToken);
            if (written.IsFailure)
            {
                return Result.Failure<AssignmentResult>(written.Error);
            }

            await transaction.CommitAsync(cancellationToken);
            scorecardId = written.Value[0].ScorecardId;
            created = written.Value[0].Created;
        }

        var detail = await reader.DetailAsync(reader.Unscoped(), scorecardId, cancellationToken);
        return detail.IsFailure
            ? Result.Failure<AssignmentResult>(detail.Error)
            : Result.Success(new AssignmentResult(detail.Value, created));
    }
}

/// <summary>
/// Assigns the same KPIs to many employees. What is wrong with the request, the period or a template fails the whole call before
/// anything is written; what is wrong with one employee only skips that employee, with the reason (the caller's own employee is always
/// skipped: nobody plans their own scorecard). One transaction writes every scorecard and item, and one activity entry summarises the
/// run. Running the same request again assigns nothing twice.
/// </summary>
internal sealed class BulkAssignKpiHandler(
    IValidator<BulkAssignKpiRequest> validator,
    AssignmentLoader loader,
    AssignmentWriter writer,
    ICurrentUser user,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork) : IBulkAssignKpiHandler
{
    public async Task<Result<BulkAssignmentResult>> HandleAsync(BulkAssignKpiRequest request, CancellationToken cancellationToken)
    {
        var valid = await validator.CheckAsync(request, cancellationToken);
        if (valid.IsFailure)
        {
            return Result.Failure<BulkAssignmentResult>(valid.Error);
        }

        var begun = await unitOfWork.BeginTransactionAsync(PlanLock.For(request.PeriodId), cancellationToken);
        if (begun.IsFailure)
        {
            return Result.Failure<BulkAssignmentResult>(begun.Error);
        }

        await using var transaction = begun.Value;
        var period = await loader.FindPeriodAsync(request.PeriodId, cancellationToken);
        if (period.IsFailure)
        {
            return Result.Failure<BulkAssignmentResult>(period.Error);
        }

        var open = AssignmentRules.EnsureOpen(period.Value);
        if (open.IsFailure)
        {
            return Result.Failure<BulkAssignmentResult>(open.Error);
        }

        var prepared = await loader.PrepareAsync(request.Items!, period.Value, cancellationToken);
        if (prepared.IsFailure)
        {
            return Result.Failure<BulkAssignmentResult>(prepared.Error);
        }

        var skipped = new List<SkippedEmployeeDto>();
        var employees = await ResolveAsync(request, period.Value, skipped, cancellationToken);
        if (employees.IsFailure)
        {
            return Result.Failure<BulkAssignmentResult>(employees.Error);
        }

        var existing = await loader.ExistingAsync(period.Value.Id, [.. employees.Value.Select(employee => employee.Id)], cancellationToken);
        var plans = new List<EmployeePlan>();
        foreach (var employee in employees.Value)
        {
            var refusal = AssignmentRules.EnsureNotOwnPlan(user.EmployeeId, employee.Id);
            if (refusal.IsSuccess)
            {
                refusal = AssignmentRules.CheckEmployee(employee, period.Value);
            }

            if (refusal.IsSuccess)
            {
                refusal = AssignmentRules.CheckPlacement(employee, existing.GetValueOrDefault(employee.Id), prepared.Value);
            }

            if (refusal.IsFailure)
            {
                skipped.Add(new SkippedEmployeeDto(employee.Id, employee.EmployeeCode, refusal.Error.Message));
            }
            else
            {
                plans.Add(new EmployeePlan(employee, existing.GetValueOrDefault(employee.Id)));
            }
        }

        IReadOnlyList<WrittenPlan> written = [];
        if (plans.Count > 0)
        {
            activity.Record(ScorecardActivity.Category, ScorecardActivity.BulkAssigned(period.Value.Name, plans.Count, skipped.Count));
            var result = await writer.WriteAsync(period.Value, plans, prepared.Value, cancellationToken);
            if (result.IsFailure)
            {
                return Result.Failure<BulkAssignmentResult>(result.Error);
            }

            written = result.Value;
        }

        await transaction.CommitAsync(cancellationToken);
        return Result.Success(new BulkAssignmentResult(
            [.. written.Select(plan => new AssignedEmployeeDto(plan.Employee.Id, plan.Employee.EmployeeCode, plan.ScorecardId, plan.ItemsAdded)).OrderBy(plan => plan.EmployeeId)],
            [.. skipped.OrderBy(entry => entry.EmployeeId)]));
    }

    /// <summary>The employees of the call: the listed ones (ids that match nobody are skipped) or those matching the filters (at most 500).</summary>
    private async Task<Result<IReadOnlyList<AssignmentEmployee>>> ResolveAsync(
        BulkAssignKpiRequest request,
        EvaluationPeriod period,
        List<SkippedEmployeeDto> skipped,
        CancellationToken cancellationToken)
    {
        if (request.EmployeeIds is { } listed)
        {
            var ids = listed.Distinct().ToArray();
            var found = await loader.EmployeesAsync(ids, cancellationToken);
            var foundIds = found.Select(employee => employee.Id).ToHashSet();
            skipped.AddRange(ids.Where(id => !foundIds.Contains(id)).Select(id => new SkippedEmployeeDto(id, null, "Employee was not found.")));
            return Result.Success<IReadOnlyList<AssignmentEmployee>>(found);
        }

        var matching = await loader.EmployeesMatchingAsync(request, period.CompanyId, cancellationToken);
        return matching.Count > AssignmentLoader.MaxEmployees
            ? Result.Failure<IReadOnlyList<AssignmentEmployee>>(AppError.Validation(
                $"The filters match more than {AssignmentLoader.MaxEmployees} employees; at most {AssignmentLoader.MaxEmployees} can be assigned in one call. Narrow the filters.",
                "filters",
                "Narrow the filters."))
            : Result.Success<IReadOnlyList<AssignmentEmployee>>(matching);
    }
}
