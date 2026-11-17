using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Performance;
using FluentValidation;

namespace Apex.Hrms.Application.Performance.Scorecards;

/// <summary>
/// Loads the scorecard or item an HR plan write works on. The write endpoints are guarded by the <c>kpi-setup</c> permission and
/// are not row-scoped (HR plans every employee's scorecard), so they do not use the scope of the read endpoints. A change still
/// needs the row version the client last saw (400 when missing, 404 when the record is gone, 409 when it is stale), and the
/// repository checks it again in the database when saving. Separation of duties: nobody plans their own scorecard, so a scorecard
/// of the caller's linked employee is refused (409, <see cref="AssignmentRules.OwnPlanMessage"/>); the built-in administrator, who
/// has no employee, is never refused for this.
/// </summary>
internal sealed class PlanLoader(
    IRepository<Scorecard> scorecards,
    IRepository<ScorecardItem> items,
    IRepository<EvaluationPeriod> periods,
    IRepository<KpiCategory> categories,
    IReadModel read,
    ICurrentUser user)
{
    /// <summary>The scorecard a plan write changes: the row version (400), the scorecard (404), not the caller's own (409), current (409).</summary>
    public async Task<Result<Scorecard>> ScorecardForChangeAsync(int id, string? rowVersion, CancellationToken cancellationToken)
    {
        var expected = RowVersionToken.Decode(rowVersion);
        if (expected.IsFailure)
        {
            return Result.Failure<Scorecard>(expected.Error);
        }

        var scorecard = await scorecards.FindAsync(id, cancellationToken);
        if (scorecard is null)
        {
            return Result.Failure<Scorecard>(NotFoundError.For("Scorecard", id));
        }

        var notOwn = AssignmentRules.EnsureNotOwnPlan(user.EmployeeId, scorecard.EmployeeId);
        if (notOwn.IsFailure)
        {
            return Result.Failure<Scorecard>(notOwn.Error);
        }

        if (!scorecard.RowVersion.AsSpan().SequenceEqual(expected.Value))
        {
            return Result.Failure<Scorecard>(RowVersionToken.Stale);
        }

        scorecards.ExpectRowVersion(scorecard, expected.Value);
        return Result.Success(scorecard);
    }

    public async Task<Result<ScorecardItem>> ItemForChangeAsync(int id, string? rowVersion, CancellationToken cancellationToken)
    {
        var expected = RowVersionToken.Decode(rowVersion);
        if (expected.IsFailure)
        {
            return Result.Failure<ScorecardItem>(expected.Error);
        }

        var item = await items.FindAsync(id, cancellationToken);
        if (item is null)
        {
            return Result.Failure<ScorecardItem>(NotFoundError.For("Scorecard item", id));
        }

        if (!item.RowVersion.AsSpan().SequenceEqual(expected.Value))
        {
            return Result.Failure<ScorecardItem>(RowVersionToken.Stale);
        }

        items.ExpectRowVersion(item, expected.Value);
        return Result.Success(item);
    }

    /// <summary>The scorecard of an item a plan write changes; the caller's own scorecard is refused (409).</summary>
    public async Task<Result<Scorecard>> ScorecardOfAsync(ScorecardItem item, CancellationToken cancellationToken)
    {
        var scorecard = await scorecards.FindAsync(item.ScorecardId, cancellationToken);
        if (scorecard is null)
        {
            return Result.Failure<Scorecard>(NotFoundError.For("Scorecard", item.ScorecardId));
        }

        var notOwn = AssignmentRules.EnsureNotOwnPlan(user.EmployeeId, scorecard.EmployeeId);
        return notOwn.IsFailure ? Result.Failure<Scorecard>(notOwn.Error) : Result.Success(scorecard);
    }

    /// <summary>What the caller may read of the review fields of <paramref name="scorecard"/> (<see cref="ReviewVisibility"/>).</summary>
    public ReviewVisibility VisibilityOf(Scorecard scorecard) => ReviewVisibility.For(scorecard.Status, scorecard.EmployeeId, user.EmployeeId);

    public async Task<EvaluationPeriod> PeriodOfAsync(Scorecard scorecard, CancellationToken cancellationToken) =>
        await periods.FindAsync(scorecard.PeriodId, cancellationToken)
            ?? throw new InvalidOperationException($"Scorecard #{scorecard.Id} points at a period that does not exist.");

    public async Task<string> CategoryNameAsync(int categoryId, CancellationToken cancellationToken) =>
        (await categories.FindAsync(categoryId, cancellationToken))?.Name ?? string.Empty;

    /// <summary>"EMP-0007" and the period name, as the activity entries name a scorecard.</summary>
    public async Task<(string EmployeeCode, string PeriodName)> LabelAsync(Scorecard scorecard, EvaluationPeriod period, CancellationToken cancellationToken)
    {
        var employeeId = scorecard.EmployeeId;
        var code = await read.FirstOrDefaultAsync(
            read.Query<Employee>().Where(employee => employee.Id == employeeId).Select(employee => employee.EmployeeCode),
            cancellationToken);
        return (code ?? $"#{employeeId}", period.Name);
    }

    /// <summary>
    /// The period must be Draft or Active for a change that adds or alters planned KPIs (409 otherwise). Removing a KPI or a
    /// whole plan that was never finalized is allowed in any period, so a forgotten plan can always be cleaned up.
    /// </summary>
    public static Result EnsurePeriodOpen(EvaluationPeriod period) => AssignmentRules.EnsureOpen(period);
}

/// <summary>
/// Changes the target, weight, dates and position of an assigned KPI while its scorecard is in Planning. The scoring rule is
/// validated again with the new target, the dates must lie within the period, and the weights of the scorecard must still add up
/// to at most 100%.
/// </summary>
internal sealed class ScorecardItemUpdateHandler(
    IValidator<ScorecardItemUpdateRequest> validator,
    PlanLoader loader,
    IRepository<ScorecardItem> items,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork) : IUpdateHandler<ScorecardItemUpdateRequest, ScorecardItemDto>
{
    public async Task<Result<ScorecardItemDto>> HandleAsync(int id, ScorecardItemUpdateRequest request, CancellationToken cancellationToken)
    {
        var valid = await validator.CheckAsync(request, cancellationToken);
        if (valid.IsFailure)
        {
            return Result.Failure<ScorecardItemDto>(valid.Error);
        }

        var begun = await unitOfWork.BeginTransactionAsync(PlanLock.Name, cancellationToken);
        if (begun.IsFailure)
        {
            return Result.Failure<ScorecardItemDto>(begun.Error);
        }

        await using var transaction = begun.Value;
        var loaded = await loader.ItemForChangeAsync(id, request.RowVersion, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<ScorecardItemDto>(loaded.Error);
        }

        var item = loaded.Value;
        var scorecard = await loader.ScorecardOfAsync(item, cancellationToken);
        if (scorecard.IsFailure)
        {
            return Result.Failure<ScorecardItemDto>(scorecard.Error);
        }

        var planning = scorecard.Value.EnsurePlanning();
        if (planning.IsFailure)
        {
            return Result.Failure<ScorecardItemDto>(planning.Error);
        }

        var period = await loader.PeriodOfAsync(scorecard.Value, cancellationToken);
        var open = PlanLoader.EnsurePeriodOpen(period);
        if (open.IsFailure)
        {
            return Result.Failure<ScorecardItemDto>(open.Error);
        }

        var start = request.StartDate!.Value;
        var end = request.EndDate!.Value;
        var violations = new Violations()
            .AddIf(start < period.StartDate || start > period.EndDate, "startDate", $"Must be within the period ({Format(period.StartDate)} to {Format(period.EndDate)}).")
            .AddIf(end < period.StartDate || end > period.EndDate, "endDate", $"Must be within the period ({Format(period.StartDate)} to {Format(period.EndDate)}).");
        if (violations.Any)
        {
            return Result.Failure<ScorecardItemDto>(violations.ToError());
        }

        var siblings = await items.ListAsync(other => other.ScorecardId == item.ScorecardId && other.Id != item.Id, cancellationToken);
        var total = Scorecard.EnsureWeightTotal(siblings.Sum(other => other.Weight) + request.Weight!.Value, "weight");
        if (total.IsFailure)
        {
            return Result.Failure<ScorecardItemDto>(total.Error);
        }

        var changed = item.UpdatePlan(request.Target!.Value, request.Weight.Value, start, end, request.SortOrder is { } order ? (short)order : item.SortOrder);
        if (changed.IsFailure)
        {
            return Result.Failure<ScorecardItemDto>(changed.Error);
        }

        var (code, periodName) = await loader.LabelAsync(scorecard.Value, period, cancellationToken);
        activity.Record(ScorecardActivity.Category, ScorecardActivity.ItemUpdated(item.KpiCode, code, periodName));
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return Result.Failure<ScorecardItemDto>(saved.Error);
        }

        await transaction.CommitAsync(cancellationToken);
        return Result.Success(ScorecardMapping.ToItemDto(item, await loader.CategoryNameAsync(item.CategoryId, cancellationToken), loader.VisibilityOf(scorecard.Value)));
    }

    private static string Format(DateOnly date) => date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>Removes an assigned KPI while its scorecard is in Planning.</summary>
internal sealed class ScorecardItemDeleteHandler(
    PlanLoader loader,
    IRepository<ScorecardItem> items,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork) : IDeleteHandler<ScorecardItemDto>
{
    public async Task<Result> HandleAsync(int id, string? rowVersion, CancellationToken cancellationToken)
    {
        var begun = await unitOfWork.BeginTransactionAsync(PlanLock.Name, cancellationToken);
        if (begun.IsFailure)
        {
            return Result.Failure(begun.Error);
        }

        await using var transaction = begun.Value;
        var loaded = await loader.ItemForChangeAsync(id, rowVersion, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure(loaded.Error);
        }

        var item = loaded.Value;
        var scorecard = await loader.ScorecardOfAsync(item, cancellationToken);
        if (scorecard.IsFailure)
        {
            return Result.Failure(scorecard.Error);
        }

        var planning = scorecard.Value.EnsurePlanning();
        if (planning.IsFailure)
        {
            return planning;
        }

        var period = await loader.PeriodOfAsync(scorecard.Value, cancellationToken);
        var (code, periodName) = await loader.LabelAsync(scorecard.Value, period, cancellationToken);
        activity.Record(ScorecardActivity.Category, ScorecardActivity.ItemRemoved(item.KpiCode, code, periodName));
        items.Remove(item);
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return saved;
        }

        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}

/// <summary>Deletes a scorecard that is still in Planning and has no review history, with its items.</summary>
internal sealed class ScorecardDeleteHandler(
    PlanLoader loader,
    IRepository<ScorecardItem> items,
    IRepository<Scorecard> scorecards,
    IRepository<ReviewAction> history,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork) : IDeleteHandler<ScorecardDto>
{
    public async Task<Result> HandleAsync(int id, string? rowVersion, CancellationToken cancellationToken)
    {
        var begun = await unitOfWork.BeginTransactionAsync(PlanLock.Name, cancellationToken);
        if (begun.IsFailure)
        {
            return Result.Failure(begun.Error);
        }

        await using var transaction = begun.Value;
        var loaded = await loader.ScorecardForChangeAsync(id, rowVersion, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure(loaded.Error);
        }

        var scorecard = loaded.Value;
        var planning = scorecard.EnsurePlanning();
        if (planning.IsFailure)
        {
            return planning;
        }

        if (await history.AnyAsync(action => action.ScorecardId == id, cancellationToken))
        {
            return Result.Failure(AppError.Conflict("The scorecard has a review history and cannot be deleted."));
        }

        var period = await loader.PeriodOfAsync(scorecard, cancellationToken);
        var (code, periodName) = await loader.LabelAsync(scorecard, period, cancellationToken);
        activity.Record(ScorecardActivity.Category, ScorecardActivity.ScorecardDeleted(code, periodName));
        foreach (var item in await items.ListAsync(item => item.ScorecardId == id, cancellationToken))
        {
            items.Remove(item);
        }

        scorecards.Remove(scorecard);
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return saved;
        }

        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}

/// <summary>
/// Finalizes the plan of a scorecard: Planning to Draft. It needs an open period, at least one KPI and weights that add up to
/// exactly 100%. The reviewer is the employee's manager now (none when the employee has no manager: then only HR-level
/// reviewers can review it). The step is appended to the review history with the actor and the time.
/// </summary>
internal sealed class FinalizePlanHandler(
    PlanLoader loader,
    ScorecardReader reader,
    IRepository<ScorecardItem> items,
    IRepository<ReviewAction> history,
    AssignmentLoader assignments,
    IClock clock,
    ICurrentUser user,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork) : ICommandHandler<FinalizePlanCommand, ScorecardDetailDto>
{
    public async Task<Result<ScorecardDetailDto>> HandleAsync(FinalizePlanCommand command, CancellationToken cancellationToken)
    {
        var begun = await unitOfWork.BeginTransactionAsync(PlanLock.Name, cancellationToken);
        if (begun.IsFailure)
        {
            return Result.Failure<ScorecardDetailDto>(begun.Error);
        }

        await using (var transaction = begun.Value)
        {
            var loaded = await loader.ScorecardForChangeAsync(command.Id, command.RowVersion, cancellationToken);
            if (loaded.IsFailure)
            {
                return Result.Failure<ScorecardDetailDto>(loaded.Error);
            }

            var scorecard = loaded.Value;
            var planning = scorecard.EnsurePlanning();
            if (planning.IsFailure)
            {
                return Result.Failure<ScorecardDetailDto>(planning.Error);
            }

            var period = await loader.PeriodOfAsync(scorecard, cancellationToken);
            var open = PlanLoader.EnsurePeriodOpen(period);
            if (open.IsFailure)
            {
                return Result.Failure<ScorecardDetailDto>(open.Error);
            }

            var weights = (await items.ListAsync(item => item.ScorecardId == command.Id, cancellationToken)).Select(item => item.Weight).ToList();
            var employee = await assignments.EmployeeAsync(scorecard.EmployeeId, cancellationToken);
            var actor = new ReviewActor(user.UserId, user.EmployeeId, user.DisplayName);
            var finalized = scorecard.FinalizePlan(clock.Today, employee?.ManagerEmployeeId, weights, actor, clock.UtcNow.UtcDateTime);
            if (finalized.IsFailure)
            {
                return Result.Failure<ScorecardDetailDto>(finalized.Error);
            }

            history.Add(finalized.Value);
            var (code, periodName) = await loader.LabelAsync(scorecard, period, cancellationToken);
            activity.Record(ScorecardActivity.Category, ScorecardActivity.PlanFinalized(code, periodName));
            var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result.Failure<ScorecardDetailDto>(saved.Error);
            }

            await transaction.CommitAsync(cancellationToken);
        }

        return await reader.DetailAsync(reader.Unscoped(), command.Id, cancellationToken);
    }
}
