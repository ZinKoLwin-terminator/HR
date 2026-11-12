using System.Globalization;
using System.Linq.Expressions;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Organization;
using Apex.Hrms.Domain.Payroll;
using FluentValidation;

namespace Apex.Hrms.Application.Payroll.Assignments;

internal sealed class SalaryAssignmentMapping :
    IEntityDescriptor<SalaryAssignment>,
    IDtoMapper<SalaryAssignment, SalaryAssignmentDto>
{
    public string Label => "Salary assignment";

    public string ActivityCategory => Domain.Activity.ActivityCategories.Payroll;

    public string Describe(SalaryAssignment entity) => $"employee #{entity.EmployeeId} from {Iso(entity.EffectiveFrom)}";

    /// <summary>
    /// The activity subject: the employee code and the date, never the name or the amount (the mock names the employee;
    /// the API uses the code like the other modules).
    /// </summary>
    public static string Subject(string employeeCode, DateOnly effectiveFrom) => $"{employeeCode} from {Iso(effectiveFrom)}";

    public SalaryAssignmentDto ToDto(SalaryAssignment entity) => new(
        entity.Id,
        entity.EmployeeId,
        entity.SalaryStructureId,
        entity.BasicSalary,
        entity.EffectiveFrom,
        entity.Remarks ?? string.Empty,
        entity.RowVersion);

    internal static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

/// <summary>
/// The rules of a salary assignment that need other records, in the order the frontend's mock checks them and stopping
/// at the first: the employee exists, the structure exists and is active, the basic salary is inside the band of the
/// employee's job grade, the date is not before the joining date, and no other assignment of the employee starts that day.
/// </summary>
internal sealed class SalaryAssignmentRules(
    IRepository<Employee> employees,
    IRepository<SalaryStructure> structures,
    IRepository<JobGrade> grades,
    IRepository<SalaryAssignment> assignments)
{
    /// <returns>The employee, so the caller can name the change by the employee code.</returns>
    public async Task<Result<Employee>> CheckAsync(SalaryAssignmentDetails details, int excludedId, CancellationToken cancellationToken)
    {
        var employee = await employees.FindAsync(details.EmployeeId, cancellationToken);
        if (employee is null)
        {
            return Result.Failure<Employee>(AppError.Validation(
                "The selected employee does not exist.", "employeeId", "Choose an employee."));
        }

        var structure = await structures.FindAsync(details.SalaryStructureId, cancellationToken);
        if (structure is null || !structure.IsActive)
        {
            return Result.Failure<Employee>(AppError.Validation(
                "Choose an active salary structure.", "structureId", "Choose an active structure."));
        }

        var grade = await grades.FindAsync(employee.JobGradeId, cancellationToken);
        if (grade is not null && (details.BasicSalary < grade.MinSalary || details.BasicSalary > grade.MaxSalary))
        {
            var band = $"{Plain(grade.MinSalary)} – {Plain(grade.MaxSalary)}";
            return Result.Failure<Employee>(AppError.Validation(
                $"Basic salary must be within the {grade.Code} band ({band}).",
                "basicSalary",
                $"Outside the {grade.Code} band ({band})."));
        }

        if (details.EffectiveFrom < employee.JoiningDate)
        {
            return Result.Failure<Employee>(AppError.Validation(
                "The salary cannot start before the joining date.",
                "effectiveFrom",
                $"Must be on or after {SalaryAssignmentMapping.Iso(employee.JoiningDate)}."));
        }

        var employeeId = details.EmployeeId;
        var effectiveFrom = details.EffectiveFrom;
        if (await assignments.AnyAsync(
                a => a.Id != excludedId && a.EmployeeId == employeeId && a.EffectiveFrom == effectiveFrom,
                cancellationToken))
        {
            return Result.Failure<Employee>(AppError.Validation(
                "This employee already has a salary starting on that date.",
                "effectiveFrom",
                "Another assignment starts on this date."));
        }

        return Result.Success(employee);
    }

    private static string Plain(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}

internal sealed class SalaryAssignmentQuerySpecification : IQuerySpecification<SalaryAssignment, SalaryAssignmentFilter>
{
    public IReadOnlyDictionary<string, Expression<Func<SalaryAssignment, object>>> SortKeys { get; } =
        new Dictionary<string, Expression<Func<SalaryAssignment, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = a => a.Id,
            ["employeeId"] = a => a.EmployeeId,
            ["structureId"] = a => a.SalaryStructureId,
            ["basicSalary"] = a => a.BasicSalary,
            ["effectiveFrom"] = a => a.EffectiveFrom,
        };

    public Expression<Func<SalaryAssignment, bool>> Search(string term) =>
        Predicates.ContainsAny<SalaryAssignment>(term, a => a.Remarks);

    public Expression<Func<SalaryAssignment, bool>> Filter(SalaryAssignmentFilter filter) => Predicates.Always<SalaryAssignment>()
        .And(filter.EmployeeId is { } employeeId ? a => a.EmployeeId == employeeId : Predicates.Always<SalaryAssignment>())
        .And(filter.StructureId is { } structureId ? a => a.SalaryStructureId == structureId : Predicates.Always<SalaryAssignment>());
}

/// <summary>Shape rules shared by the create and update requests.</summary>
internal abstract class SalaryAssignmentFieldsValidator<TRequest> : RequestValidator<TRequest>
    where TRequest : ISalaryAssignmentFields
{
    protected SalaryAssignmentFieldsValidator()
    {
        RuleFor(x => x.EmployeeId).GreaterThan(0).WithMessage("Choose an employee.");
        RuleFor(x => x.StructureId).GreaterThan(0).WithMessage("Choose a salary structure.");
        RuleFor(x => x.BasicSalary)
            .PrecisionScale(18, 2, ignoreTrailingZeros: true).WithMessage("Use at most 2 decimal places.");
        RuleFor(x => x.EffectiveFrom).NotEqual(default(DateOnly)).WithMessage("Effective date is required.");
        RuleForText(x => x.Remarks, "Remarks").OptionalText(SalaryAssignment.RemarksMaxLength);
    }
}

internal sealed class SalaryAssignmentCreateRequestValidator : SalaryAssignmentFieldsValidator<SalaryAssignmentCreateRequest>;

internal sealed class SalaryAssignmentUpdateRequestValidator : SalaryAssignmentFieldsValidator<SalaryAssignmentUpdateRequest>
{
    public SalaryAssignmentUpdateRequestValidator() => RuleForText(x => x.RowVersion).RequiredRowVersion();
}
