using FluentValidation;

namespace Apex.Hrms.Application.Performance.Scorecards;

/// <summary>
/// Shape rules of the KPIs in an assignment: one to 50 items, each with a template and, when given, a target (at most 4 decimals,
/// not negative), a weight (above 0, at most 100, at most 2 decimals), dates in order and a sort order that fits the column.
/// Whether the template exists, the dates lie within the period and the rule stays consistent are rules that need other records.
/// </summary>
internal abstract class AssignmentItemsValidator<TRequest> : RequestValidator<TRequest>
    where TRequest : IAssignmentItems
{
    public const int MaxItems = 50;

    protected AssignmentItemsValidator()
    {
        RuleFor(x => x.Items)
            .NotNull().WithMessage("Choose at least one KPI to assign.")
            .Must(items => items!.Count > 0).WithMessage("Choose at least one KPI to assign.")
            .Must(items => items!.Count <= MaxItems).WithMessage($"At most {MaxItems} KPIs can be assigned in one request.");
        RuleForEach(x => x.Items)
            .NotNull().WithMessage("Each KPI needs a template.")
            .SetValidator(new AssignmentItemValidator());
    }
}

internal sealed class AssignmentItemValidator : RequestValidator<AssignmentItemRequest>
{
    public AssignmentItemValidator()
    {
        RuleFor(x => x.TemplateId).GreaterThan(0).WithMessage("Choose a KPI template.");
        RuleFor(x => x.Target)
            .Must(target => target is null or >= 0m).WithMessage("The target cannot be negative.")
            .PrecisionScale(18, 4, ignoreTrailingZeros: true).WithMessage("Use at most 4 decimal places.");
        RuleFor(x => x.Weight)
            .Must(weight => weight is null or (> 0m and <= 100m)).WithMessage("The weight must be greater than 0 and at most 100.")
            .PrecisionScale(5, 2, ignoreTrailingZeros: true).WithMessage("Use at most 2 decimal places.");
        RuleFor(x => x.SortOrder)
            .Must(order => order is null or (>= 0 and <= short.MaxValue)).WithMessage($"The sort order must be between 0 and {short.MaxValue}.");
        RuleFor(x => x.EndDate)
            .Must((item, end) => end is null || item.StartDate is null || end >= item.StartDate)
            .WithMessage("Must be on or after the start date.");
    }
}

internal sealed class AssignKpiRequestValidator : AssignmentItemsValidator<AssignKpiRequest>
{
    public AssignKpiRequestValidator()
    {
        RuleFor(x => x.EmployeeId).GreaterThan(0).WithMessage("Choose an employee.");
        RuleFor(x => x.PeriodId).GreaterThan(0).WithMessage("Choose an evaluation period.");
    }
}

internal sealed class BulkAssignKpiRequestValidator : AssignmentItemsValidator<BulkAssignKpiRequest>
{
    public const int MaxEmployees = 500;

    public BulkAssignKpiRequestValidator()
    {
        RuleFor(x => x.PeriodId).GreaterThan(0).WithMessage("Choose an evaluation period.");
        RuleFor(x => x.EmployeeIds)
            .Must((request, ids) => (ids is not null) != HasFilter(request))
            .WithMessage("Choose the employees either with employeeIds or with filters (departmentId, branchId, positionId, jobGradeId), not both and not neither.")
            .Must(ids => ids is null || ids.Count > 0).WithMessage("Choose at least one employee.")
            .Must(ids => ids is null || ids.Count <= MaxEmployees).WithMessage($"At most {MaxEmployees} employees can be assigned in one call.")
            .Must(ids => ids is null || ids.All(id => id > 0)).WithMessage("Every employee id must be a positive number.");
    }

    internal static bool HasFilter(BulkAssignKpiRequest request) =>
        request.DepartmentId is not null || request.BranchId is not null || request.PositionId is not null || request.JobGradeId is not null;
}

internal sealed class ScorecardItemUpdateRequestValidator : RequestValidator<ScorecardItemUpdateRequest>
{
    public ScorecardItemUpdateRequestValidator()
    {
        RuleFor(x => x.Target)
            .NotNull().WithMessage("Target is required.")
            .Must(target => target >= 0m).WithMessage("The target cannot be negative.")
            .PrecisionScale(18, 4, ignoreTrailingZeros: true).WithMessage("Use at most 4 decimal places.");
        RuleFor(x => x.Weight)
            .NotNull().WithMessage("Weight is required.")
            .Must(weight => weight is > 0m and <= 100m).WithMessage("The weight must be greater than 0 and at most 100.")
            .PrecisionScale(5, 2, ignoreTrailingZeros: true).WithMessage("Use at most 2 decimal places.");
        RuleFor(x => x.StartDate).NotNull().WithMessage("Start date is required.");
        RuleFor(x => x.EndDate)
            .NotNull().WithMessage("End date is required.")
            .Must((item, end) => item.StartDate is null || end >= item.StartDate).WithMessage("Must be on or after the start date.");
        RuleFor(x => x.SortOrder)
            .Must(order => order is null or (>= 0 and <= short.MaxValue)).WithMessage($"The sort order must be between 0 and {short.MaxValue}.");
        RuleForText(x => x.RowVersion).RequiredRowVersion();
    }
}
