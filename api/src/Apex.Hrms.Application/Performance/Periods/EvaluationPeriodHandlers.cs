using System.Globalization;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Activity;
using Apex.Hrms.Application.References;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;

namespace Apex.Hrms.Application.Performance.Periods;

/// <summary>The period picker for callers without the setup permission: periods that are not Draft.</summary>
public interface IEvaluationPeriodOptionsHandler
{
    Task<Result<IReadOnlyList<EvaluationPeriodOptionDto>>> HandleAsync(int? companyId, CancellationToken cancellationToken);
}

internal sealed class EvaluationPeriodOptionsHandler(IRepository<EvaluationPeriod> periods) : IEvaluationPeriodOptionsHandler
{
    public async Task<Result<IReadOnlyList<EvaluationPeriodOptionDto>>> HandleAsync(int? companyId, CancellationToken cancellationToken)
    {
        var draft = EvaluationPeriodStatus.Draft;
        var rows = await periods.ListAsync(
            p => p.Status != draft && (companyId == null || p.CompanyId == companyId),
            cancellationToken);
        IReadOnlyList<EvaluationPeriodOptionDto> options =
        [
            .. rows.OrderByDescending(p => p.StartDate).ThenBy(p => p.Id).Select(EvaluationPeriodMapping.ToOption),
        ];
        return Result.Success(options);
    }
}

/// <summary>
/// Deletes a Draft period that no scorecard uses. A period cannot be deactivated, so the refusal does not suggest it; a
/// period with scorecards is Conflict with the count.
/// </summary>
internal sealed class EvaluationPeriodDeleteHandler(
    EntityLoader<EvaluationPeriod> loader,
    IEnumerable<IReferenceCheck<EvaluationPeriod>> references,
    IRepository<EvaluationPeriod> repository,
    EvaluationPeriodMapping mapper,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork) : IDeleteHandler<EvaluationPeriodDto>
{
    public async Task<Result> HandleAsync(int id, string? rowVersion, CancellationToken cancellationToken)
    {
        var loaded = await loader.LoadForChangeAsync(id, rowVersion, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure(loaded.Error);
        }

        var allowed = loaded.Value.EnsureCanDelete();
        if (allowed.IsFailure)
        {
            return allowed;
        }

        var usage = await ReferenceUsage.SummarizeAsync(references, ReferenceUse.Delete, id, cancellationToken);
        if (usage is not null)
        {
            return Result.Failure(AppError.Conflict(
                $"\"{mapper.Describe(loaded.Value)}\" cannot be deleted because it is used by {usage}."));
        }

        activity.Record(mapper.ActivityCategory, ActivityMessages.Deleted(mapper.Label, mapper.Describe(loaded.Value)));
        repository.Remove(loaded.Value);
        return await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

internal sealed class ActivatePeriodAction : ICommandAction<EvaluationPeriod, ActivatePeriodCommand>
{
    public Result Execute(EvaluationPeriod entity, ActivatePeriodCommand command) => entity.Activate();
}

internal sealed class CompletePeriodAction : ICommandAction<EvaluationPeriod, CompletePeriodCommand>
{
    public Result Execute(EvaluationPeriod entity, CompletePeriodCommand command) => entity.Complete();
}

/// <summary>The wording of a period move in the activity feed: <c>Evaluation period "2026 Q4" activated</c>.</summary>
internal sealed class PeriodMoveActivity<TCommand> : ICommandActivity<EvaluationPeriod, TCommand>
    where TCommand : IPeriodMove
{
    public string Describe(EvaluationPeriod entity, TCommand command) =>
        $"Evaluation period \"{entity.Name}\" {command.Verb}";
}

/// <summary>
/// Closes a period (Completed to Closed, final). A Closed period refuses every scorecard write, so closing is refused (409 with the count)
/// while any of its scorecards is still in review or returned to the employee (<see cref="EvaluationPeriod.StatusesBlockingClose"/>):
/// they would be frozen half way. Draft and Planning scorecards do not block; they stay recorded as never submitted. The count needs no
/// lock: while the period is Completed no scorecard can enter a blocking status from outside them (a Draft one cannot be submitted, a
/// plan cannot be finalized), so the count can only go down between the check and the save.
/// </summary>
internal sealed class ClosePeriodHandler(
    EntityLoader<EvaluationPeriod> loader,
    IReadModel read,
    EvaluationPeriodMapping mapper,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork) : ICommandHandler<ClosePeriodCommand, EvaluationPeriodDto>
{
    public async Task<Result<EvaluationPeriodDto>> HandleAsync(ClosePeriodCommand command, CancellationToken cancellationToken)
    {
        var loaded = await loader.LoadForChangeAsync(command.Id, command.RowVersion, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<EvaluationPeriodDto>(loaded.Error);
        }

        var period = loaded.Value;
        var inReview = period.Status == EvaluationPeriodStatus.Completed
            ? await read.CountAsync(InReview(read.Query<Scorecard>(), period.Id), cancellationToken)
            : 0;
        var closed = period.Close(inReview);
        if (closed.IsFailure)
        {
            return Result.Failure<EvaluationPeriodDto>(closed.Error);
        }

        activity.Record(mapper.ActivityCategory, $"Evaluation period \"{period.Name}\" {command.Verb}");
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure ? Result.Failure<EvaluationPeriodDto>(saved.Error) : Result.Success(mapper.ToDto(period));
    }

    /// <summary>The scorecards of the period whose status blocks closing (<see cref="EvaluationPeriod.BlocksClosing"/>).</summary>
    private static IQueryable<Scorecard> InReview(IQueryable<Scorecard> scorecards, int periodId)
    {
        var submitted = ScorecardStatus.Submitted;
        var resubmitted = ScorecardStatus.Resubmitted;
        var underReview = ScorecardStatus.UnderReview;
        var approved = ScorecardStatus.Approved;
        var returned = ScorecardStatus.Returned;
        return scorecards.Where(scorecard => scorecard.PeriodId == periodId
            && (scorecard.Status == submitted
                || scorecard.Status == resubmitted
                || scorecard.Status == underReview
                || scorecard.Status == approved
                || scorecard.Status == returned));
    }
}

/// <summary>
/// Creating and updating a period checks that it overlaps no other period of the same type in the company, then saves. Two such
/// requests at the same moment could both pass the check, so each runs in a transaction holding the application lock
/// <c>kpi-period:{companyId}</c> (the company of the request: the one the overlap check reads). The check then sees every earlier change of
/// that company. The wait is at most 15 seconds, then 409 <see cref="BusyMessage"/>.
/// </summary>
internal static class PeriodLock
{
    public const string BusyMessage = "Another change to the evaluation periods of this company is in progress. Try again.";

    public static string NameFor(int companyId) => string.Create(CultureInfo.InvariantCulture, $"kpi-period:{companyId}");

    public static ApplicationLock For(int companyId) => new(NameFor(companyId), BusyMessage);

    /// <summary>Runs <paramref name="work"/> in a transaction under the lock of <paramref name="companyId"/>; commits only when it succeeds.</summary>
    public static async Task<Result<T>> RunAsync<T>(
        IUnitOfWork unitOfWork,
        int companyId,
        Func<CancellationToken, Task<Result<T>>> work,
        CancellationToken cancellationToken)
    {
        var begun = await unitOfWork.BeginTransactionAsync(For(companyId), cancellationToken);
        if (begun.IsFailure)
        {
            return Result.Failure<T>(begun.Error);
        }

        await using var transaction = begun.Value;
        var result = await work(cancellationToken);
        if (result.IsSuccess)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return result;
    }
}

/// <summary>The shared create flow of a period, under <see cref="PeriodLock"/>.</summary>
internal sealed class LockedPeriodCreateHandler(
    CreateHandler<EvaluationPeriod, EvaluationPeriodCreateRequest, EvaluationPeriodDetails, EvaluationPeriodDto> inner,
    IUnitOfWork unitOfWork) : ICreateHandler<EvaluationPeriodCreateRequest, EvaluationPeriodDto>
{
    public Task<Result<EvaluationPeriodDto>> HandleAsync(EvaluationPeriodCreateRequest request, CancellationToken cancellationToken) =>
        PeriodLock.RunAsync(unitOfWork, request.CompanyId, token => inner.HandleAsync(request, token), cancellationToken);
}

/// <summary>The shared update flow of a period, under <see cref="PeriodLock"/> of the company the request names.</summary>
internal sealed class LockedPeriodUpdateHandler(
    UpdateHandler<EvaluationPeriod, EvaluationPeriodUpdateRequest, EvaluationPeriodDetails, EvaluationPeriodDto> inner,
    IUnitOfWork unitOfWork) : IUpdateHandler<EvaluationPeriodUpdateRequest, EvaluationPeriodDto>
{
    public Task<Result<EvaluationPeriodDto>> HandleAsync(int id, EvaluationPeriodUpdateRequest request, CancellationToken cancellationToken) =>
        PeriodLock.RunAsync(unitOfWork, request.CompanyId, token => inner.HandleAsync(id, request, token), cancellationToken);
}
