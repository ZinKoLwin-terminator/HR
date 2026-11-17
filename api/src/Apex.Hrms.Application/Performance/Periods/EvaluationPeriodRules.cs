using System.Globalization;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Organization;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;
using Apex.Hrms.Domain.Performance;

namespace Apex.Hrms.Application.Performance.Periods;

/// <summary>
/// Rules of an evaluation period that need other records: an existing company (an active one when new or changed), a name
/// that is unique within the company, and no overlap with another period of the same type in the same company (dates are
/// inclusive, so two periods that touch on one day overlap). What may be edited in which status is the entity's rule
/// (<see cref="EvaluationPeriod.CheckEdit"/>), checked first on update.
/// </summary>
internal sealed class EvaluationPeriodRules(
    IRepository<EvaluationPeriod> periods,
    IRepository<Company> companies) : ISaveRules<EvaluationPeriod, EvaluationPeriodDetails>
{
    public Task<Result> CheckCreateAsync(EvaluationPeriodDetails details, CancellationToken cancellationToken) =>
        CheckAsync(details, existing: null, cancellationToken);

    public async Task<Result> CheckUpdateAsync(EvaluationPeriod existing, EvaluationPeriodDetails details, CancellationToken cancellationToken)
    {
        var allowed = existing.CheckEdit(details);
        return allowed.IsFailure ? allowed : await CheckAsync(details, existing, cancellationToken);
    }

    private async Task<Result> CheckAsync(EvaluationPeriodDetails details, EvaluationPeriod? existing, CancellationToken cancellationToken)
    {
        var company = await HierarchyChecks.FindParentAsync(companies, details.CompanyId, "Company", "companyId", cancellationToken);
        if (company.IsFailure)
        {
            return company;
        }

        if (existing?.CompanyId != details.CompanyId)
        {
            var active = HierarchyChecks.EnsureActive(company.Value, "Company", company.Value.Name, "evaluation periods", "companyId");
            if (active.IsFailure)
            {
                return active;
            }
        }

        var companyId = details.CompanyId;
        var name = details.Name;
        var excludedId = existing?.Id ?? 0;
        if (await periods.AnyAsync(p => p.CompanyId == companyId && p.Name == name && p.Id != excludedId, cancellationToken))
        {
            return Result.Failure(AppError.Validation(
                $"Period name \"{name}\" already exists in this company.",
                "name",
                "Period name must be unique within the company."));
        }

        var datesChanged = existing is null
            || existing.CompanyId != details.CompanyId
            || existing.PeriodType != details.PeriodType
            || existing.StartDate != details.StartDate
            || existing.EndDate != details.EndDate;
        return datesChanged ? await EnsureNoOverlapAsync(details, excludedId, cancellationToken) : Result.Success();
    }

    private async Task<Result> EnsureNoOverlapAsync(EvaluationPeriodDetails details, int excludedId, CancellationToken cancellationToken)
    {
        var companyId = details.CompanyId;
        var type = details.PeriodType;
        var start = details.StartDate;
        var end = details.EndDate;
        var clashes = await periods.ListAsync(
            p => p.Id != excludedId && p.CompanyId == companyId && p.PeriodType == type && p.StartDate <= end && p.EndDate >= start,
            cancellationToken);
        if (clashes.OrderBy(p => p.StartDate).ThenBy(p => p.Id).FirstOrDefault() is not { } clash)
        {
            return Result.Success();
        }

        return Result.Failure(AppError.Conflict(
            $"The dates overlap period \"{clash.Name}\" ({Format(clash.StartDate)} to {Format(clash.EndDate)}), which is also {KpiFrequencyText.ToText(type)}. Periods of the same type cannot overlap in a company."));
    }

    private static string Format(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
