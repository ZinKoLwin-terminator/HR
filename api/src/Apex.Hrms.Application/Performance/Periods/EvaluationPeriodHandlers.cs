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

internal sealed class ClosePeriodAction : ICommandAction<EvaluationPeriod, ClosePeriodCommand>
{
    public Result Execute(EvaluationPeriod entity, ClosePeriodCommand command) => entity.Close();
}

/// <summary>The wording of a period move in the activity feed: <c>Evaluation period "2026 Q4" activated</c>.</summary>
internal sealed class PeriodMoveActivity<TCommand> : ICommandActivity<EvaluationPeriod, TCommand>
    where TCommand : IPeriodMove
{
    public string Describe(EvaluationPeriod entity, TCommand command) =>
        $"Evaluation period \"{entity.Name}\" {command.Verb}";
}
