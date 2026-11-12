using System.Linq.Expressions;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Activity;
using Apex.Hrms.Application.Organization;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Payroll;
using FluentValidation;

namespace Apex.Hrms.Application.Payroll.Runs;

internal sealed class PayrollRunMapping(IClock clock) :
    IEntityDescriptor<PayrollRun>,
    IDtoMapper<PayrollRun, PayrollRunDto>,
    IEntityFactory<PayrollRun, PayrollRunDetails>
{
    public string Label => "Payroll period";

    public string ActivityCategory => Domain.Activity.ActivityCategories.Payroll;

    public string Describe(PayrollRun entity) => entity.PeriodName;

    public PayrollRunDto ToDto(PayrollRun entity) => new(
        entity.Id,
        entity.PeriodName,
        entity.Year,
        entity.Month,
        entity.StartDate,
        entity.EndDate,
        entity.PayDate,
        PayrollRunStatusText.ToText(entity.Status),
        entity.EmployeeCount,
        entity.TotalBasic,
        entity.TotalAllowances,
        entity.TotalGross,
        entity.TotalDeductions,
        entity.TotalNet,
        WarningsOf(entity),
        entity.ProcessedOn,
        entity.ProcessedByName,
        entity.ApprovedByName,
        entity.ApprovedOn,
        entity.PaidOn,
        entity.RowVersion);

    public Result Validate(PayrollRunDetails details, PayrollRun? existing) => PayrollRun.Validate(details, clock.Today);

    public Result<PayrollRun> Create(PayrollRunDetails details, RecordStatus status) => PayrollRun.Create(details, clock.Today);

    public Result Update(PayrollRun entity, PayrollRunDetails details) => entity.Update(details, clock.Today);

    // A run in Draft has no notes: returning it to draft removes them in a statement of its own, so the collection
    // the change tracker still holds is not the truth any more.
    private static string[] WarningsOf(PayrollRun run) => run.Status == PayrollRunStatus.Draft
        ? []
        : [.. run.Warnings.OrderBy(warning => warning.Id).Select(warning => warning.Message)];
}

/// <summary>One run per calendar month; a run can only be changed while it is a draft.</summary>
internal sealed class PayrollRunRules(IRepository<PayrollRun> runs) : ISaveRules<PayrollRun, PayrollRunDetails>
{
    public Task<Result> CheckCreateAsync(PayrollRunDetails details, CancellationToken cancellationToken) =>
        EnsureUniqueAsync(details, excludedId: 0, cancellationToken);

    public async Task<Result> CheckUpdateAsync(PayrollRun existing, PayrollRunDetails details, CancellationToken cancellationToken)
    {
        var draft = existing.EnsureDraftForChange();
        return draft.IsFailure ? draft : await EnsureUniqueAsync(details, existing.Id, cancellationToken);
    }

    private async Task<Result> EnsureUniqueAsync(PayrollRunDetails details, int excludedId, CancellationToken cancellationToken)
    {
        var year = details.Year;
        var month = details.Month;
        if (!await runs.AnyAsync(run => run.Id != excludedId && run.Year == year && run.Month == month, cancellationToken))
        {
            return Result.Success();
        }

        var name = PayrollRun.NameOf(PayrollRun.StartOf(year, month));
        return Result.Failure(AppError.Validation(
            $"A payroll period for {name} already exists.", "month", "This period already exists."));
    }
}

internal sealed class PayrollRunQuerySpecification : IQuerySpecification<PayrollRun, PayrollRunFilter>
{
    public IReadOnlyDictionary<string, Expression<Func<PayrollRun, object>>> SortKeys { get; } =
        new Dictionary<string, Expression<Func<PayrollRun, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = r => r.Id,
            ["periodName"] = r => r.PeriodName,
            ["year"] = r => r.Year,
            ["month"] = r => r.Month,
            ["startDate"] = r => r.StartDate,
            ["payDate"] = r => r.PayDate,
            ["status"] = r => r.Status,
            ["employeeCount"] = r => r.EmployeeCount,
            ["totalGross"] = r => r.TotalGross,
            ["totalNet"] = r => r.TotalNet,
        };

    public Expression<Func<PayrollRun, bool>> Search(string term) =>
        Predicates.ContainsAny<PayrollRun>(term, r => r.PeriodName);

    public Expression<Func<PayrollRun, bool>> Filter(PayrollRunFilter filter) =>
        StatusFilter(filter.Status)
            .And(filter.Year is { } year ? r => r.Year == year : Predicates.Always<PayrollRun>());

    private static Expression<Func<PayrollRun, bool>> StatusFilter(string? text)
    {
        if (!FilterPredicates.IsSet(text))
        {
            return Predicates.Always<PayrollRun>();
        }

        return PayrollRunStatusText.TryParse(text, out var status)
            ? r => r.Status == status
            : Predicates.Never<PayrollRun>();
    }
}

internal sealed class PayrollRunCreateRequestValidator : RequestValidator<PayrollRunCreateRequest>;

internal sealed class PayrollRunUpdateRequestValidator : RequestValidator<PayrollRunUpdateRequest>
{
    public PayrollRunUpdateRequestValidator() => RuleForText(x => x.RowVersion).RequiredRowVersion();
}

/// <summary>Deletes a draft period. Anything that was processed is returned to draft first; nothing points at a draft run.</summary>
internal sealed class PayrollRunDeleteHandler(
    EntityLoader<PayrollRun> loader,
    IRepository<PayrollRun> repository,
    PayrollRunMapping mapper,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork) : IDeleteHandler<PayrollRunDto>
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

        activity.Record(mapper.ActivityCategory, ActivityMessages.Deleted(mapper.Label, mapper.Describe(loaded.Value)));
        repository.Remove(loaded.Value);
        return await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
