using System.Globalization;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Performance;

namespace Apex.Hrms.Application.Performance.Scorecards;

/// <summary>The columns of an employee the assignment rules read (placement, manager and employment state), nothing personal.</summary>
internal sealed record AssignmentEmployee(
    int Id,
    string EmployeeCode,
    int CompanyId,
    int BranchId,
    int DepartmentId,
    int PositionId,
    int? ManagerEmployeeId,
    EmploymentStatus Status,
    DateOnly? ExitDate)
{
    public bool HasLeft => ExitDate is not null || Status.HasLeft();
}

/// <summary>A requested KPI after the checks that only need the request, the period and the template.</summary>
internal sealed record PreparedItem(int Index, KpiTemplate Template, decimal? Target, decimal? Weight, DateOnly StartDate, DateOnly EndDate, short? SortOrder)
{
    /// <summary>The weight the KPI will carry: the one requested or the template's default.</summary>
    public decimal EffectiveWeight => Weight ?? Template.DefaultWeight;
}

/// <summary>The scorecard an employee already has in the period, with its items.</summary>
internal sealed record ExistingScorecard(int Id, ScorecardStatus Status, IReadOnlyList<ScorecardItem> Items);

/// <summary>What will be written for one employee: a new scorecard or items for the existing one.</summary>
internal sealed record EmployeePlan(AssignmentEmployee Employee, ExistingScorecard? Existing);

/// <summary>
/// The rules of assigning KPIs, shared by the single and the bulk assignment. A single assignment fails with the first refusal; the
/// bulk assignment skips the employee with the same message as the reason. Rules that depend on the employee are separated from
/// those that depend only on the request, the period and the templates, because the bulk call applies the second kind to the whole
/// call and the first kind to each employee.
/// </summary>
internal static class AssignmentRules
{
    /// <summary>KPIs are assigned in a period that is Draft or Active (409 otherwise).</summary>
    public static Result EnsureOpen(EvaluationPeriod period) =>
        period.Status is EvaluationPeriodStatus.Draft or EvaluationPeriodStatus.Active
            ? Result.Success()
            : Result.Failure(AppError.Conflict(
                $"Period \"{period.Name}\" is {EvaluationPeriodStatusText.ToText(period.Status)}; KPIs can only be assigned in a Draft or Active period."));

    /// <summary>
    /// The checks that need only the request, the period and the templates, collected so every problem is reported at once: the
    /// template exists and is Active, appears once, the dates lie within the period, the snapshot rule stays consistent (an overridden
    /// target of 0 on Higher Is Better is refused) and the requested weights alone do not exceed 100%. Fields are
    /// <c>items[i].templateId</c>, <c>items[i].target</c>, <c>items[i].startDate</c> and so on.
    /// </summary>
    public static Result<IReadOnlyList<PreparedItem>> Prepare(
        IReadOnlyList<AssignmentItemRequest> items,
        EvaluationPeriod period,
        IReadOnlyDictionary<int, KpiTemplate> templates)
    {
        var violations = new Violations();
        var prepared = new List<PreparedItem>(items.Count);
        var seen = new HashSet<int>();
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var field = $"items[{index}]";
            if (!templates.TryGetValue(item.TemplateId, out var template))
            {
                violations.Add($"{field}.templateId", $"KPI template #{item.TemplateId} was not found.");
                continue;
            }

            if (!template.IsActive)
            {
                violations.Add($"{field}.templateId", $"KPI template \"{template.Code}\" is inactive and cannot be assigned.");
                continue;
            }

            if (!seen.Add(template.Id))
            {
                violations.Add($"{field}.templateId", $"KPI template \"{template.Code}\" appears more than once in the request.");
                continue;
            }

            var start = item.StartDate ?? period.StartDate;
            var end = item.EndDate ?? period.EndDate;
            var within = true;
            if (start < period.StartDate || start > period.EndDate)
            {
                violations.Add($"{field}.startDate", $"Must be within the period ({Format(period.StartDate)} to {Format(period.EndDate)}).");
                within = false;
            }

            if (end < period.StartDate || end > period.EndDate)
            {
                violations.Add($"{field}.endDate", $"Must be within the period ({Format(period.StartDate)} to {Format(period.EndDate)}).");
                within = false;
            }

            if (within && end < start)
            {
                violations.Add($"{field}.endDate", "Must be on or after the start date.");
                continue;
            }

            var details = ScorecardItemDetails.FromTemplate(template, 0, item.Target, item.Weight, start, end, 0, null, period.StartDate);
            var rule = details.Rule.Validate();
            if (rule.IsFailure)
            {
                foreach (var (name, messages) in rule.Error.FieldErrors ?? new Dictionary<string, string[]>())
                {
                    foreach (var message in messages)
                    {
                        violations.Add($"{field}.{name}", message);
                    }
                }

                continue;
            }

            prepared.Add(new PreparedItem(index, template, item.Target, item.Weight, start, end, item.SortOrder is { } order ? (short)order : null));
        }

        if (violations.Any)
        {
            return Result.Failure<IReadOnlyList<PreparedItem>>(violations.ToError());
        }

        var total = Scorecard.EnsureWeightTotal(prepared.Sum(item => item.EffectiveWeight), "items");
        return total.IsFailure
            ? Result.Failure<IReadOnlyList<PreparedItem>>(total.Error)
            : Result.Success<IReadOnlyList<PreparedItem>>(prepared);
    }

    /// <summary>The employee is a current employee of the period's company (400 on <c>employeeId</c> otherwise).</summary>
    public static Result CheckEmployee(AssignmentEmployee employee, EvaluationPeriod period)
    {
        if (employee.HasLeft)
        {
            return Result.Failure(AppError.Validation(
                $"Employee {employee.EmployeeCode} has left the company and cannot be assigned KPIs.",
                "employeeId",
                "Choose an employee who is still employed."));
        }

        return employee.CompanyId == period.CompanyId
            ? Result.Success()
            : Result.Failure(AppError.Validation(
                $"Employee {employee.EmployeeCode} does not belong to the company of period \"{period.Name}\".",
                "employeeId",
                "Choose an employee of the period's company."));
    }

    /// <summary>
    /// The checks that depend on the employee's scorecard: a department-specific template must be the employee's department's (400
    /// on <c>items[i].templateId</c>), the scorecard must still be in Planning and not already hold a requested KPI (409, naming
    /// the codes), and the weights together must stay within 100% (400 on <c>items</c>).
    /// </summary>
    public static Result CheckPlacement(AssignmentEmployee employee, ExistingScorecard? existing, IReadOnlyList<PreparedItem> prepared)
    {
        foreach (var item in prepared)
        {
            if (item.Template.DepartmentId is { } department && department != employee.DepartmentId)
            {
                return Result.Failure(AppError.Validation(
                    $"KPI template \"{item.Template.Code}\" is for another department than the one of employee {employee.EmployeeCode}.",
                    $"items[{item.Index}].templateId",
                    "This template is for another department."));
            }
        }

        if (existing is null)
        {
            return Result.Success();
        }

        if (existing.Status != ScorecardStatus.Planning)
        {
            return Result.Failure(AppError.Conflict(
                $"The scorecard of {employee.EmployeeCode} is {ScorecardStatusText.ToText(existing.Status)}; KPIs can only be assigned while it is in Planning."));
        }

        var assigned = existing.Items.Select(item => item.TemplateId).ToHashSet();
        var duplicates = prepared.Where(item => assigned.Contains(item.Template.Id)).Select(item => item.Template.Code).ToList();
        if (duplicates.Count > 0)
        {
            return Result.Failure(AppError.Conflict(duplicates.Count == 1
                ? $"KPI {duplicates[0]} is already assigned to the scorecard of {employee.EmployeeCode}."
                : $"KPIs {string.Join(", ", duplicates)} are already assigned to the scorecard of {employee.EmployeeCode}."));
        }

        var combined = existing.Items.Sum(item => item.Weight) + prepared.Sum(item => item.EffectiveWeight);
        return combined <= Scorecard.MaxTotalWeight
            ? Result.Success()
            : Result.Failure(AppError.Validation(
                $"The KPI weights on the scorecard of {employee.EmployeeCode} would add up to {combined.ToString("0.00", CultureInfo.InvariantCulture)}%; they cannot exceed {Scorecard.MaxTotalWeight:0}%.",
                "items",
                $"The weights cannot add up to more than {Scorecard.MaxTotalWeight:0}%."));
    }

    /// <summary>The sort order of the items of a plan: the one requested, or the next free position after the existing items.</summary>
    public static short SortOrderFor(PreparedItem item, ExistingScorecard? existing, int position)
    {
        if (item.SortOrder is { } requested)
        {
            return requested;
        }

        var next = existing is { Items.Count: > 0 } ? existing.Items.Max(other => other.SortOrder) + 1 : 0;
        return (short)Math.Min(next + position, short.MaxValue);
    }

    private static string Format(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
