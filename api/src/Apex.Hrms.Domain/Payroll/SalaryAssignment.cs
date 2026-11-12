using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Payroll;

/// <summary>
/// The editable fields of a salary assignment. <see cref="BasicSalary"/> is a monthly demonstration figure and is
/// personal data: it never goes into logs or activity text.
/// </summary>
public sealed record SalaryAssignmentDetails(
    int EmployeeId,
    int SalaryStructureId,
    decimal BasicSalary,
    DateOnly EffectiveFrom,
    string? Remarks)
{
    /// <summary>Trims the remarks and turns blank ones into null. Idempotent.</summary>
    public SalaryAssignmentDetails Normalized() => this with { Remarks = TextNormalization.CleanOptional(Remarks) };
}

/// <summary>
/// An employee's basic salary and structure from a date onwards (pay.SalaryAssignment); a later row revises the salary.
/// The rules that need other records (the band of the job grade, the joining date, the last assignment) are in the
/// Application layer.
/// </summary>
public sealed class SalaryAssignment : AuditedEntity
{
    public const int RemarksMaxLength = 120;

    /// <summary>The largest monthly basic the screens accept.</summary>
    public const decimal MaxBasicSalary = 1_000_000_000m;

    private SalaryAssignment()
    {
    }

    public int EmployeeId { get; private set; }

    public int SalaryStructureId { get; private set; }

    public decimal BasicSalary { get; private set; }

    public DateOnly EffectiveFrom { get; private set; }

    public string? Remarks { get; private set; }

    public static Result<SalaryAssignment> Create(SalaryAssignmentDetails details)
    {
        var valid = Validate(details);
        if (valid.IsFailure)
        {
            return Result.Failure<SalaryAssignment>(valid.Error);
        }

        var assignment = new SalaryAssignment { EmployeeId = details.EmployeeId };
        assignment.Apply(details.Normalized());
        return Result.Success(assignment);
    }

    /// <summary>Checks the rules that need no other record, without changing anything.</summary>
    public static Result Validate(SalaryAssignmentDetails details)
    {
        var normalized = details.Normalized();
        return new Violations()
            .AddIf(!(normalized.BasicSalary > 0), "basicSalary", "Must be greater than 0.")
            .AddIf(normalized.BasicSalary > MaxBasicSalary, "basicSalary", $"Must be at most {MaxBasicSalary:0}.")
            .AddIf(
                normalized.Remarks is { Length: > RemarksMaxLength },
                "remarks",
                $"Remarks must be at most {RemarksMaxLength} characters.")
            .ToResult();
    }

    /// <summary>The employee of an assignment never changes: moving a row away would let someone lose their last assignment unnoticed.</summary>
    public Result Update(SalaryAssignmentDetails details)
    {
        var same = EnsureSameEmployee(details.EmployeeId);
        if (same.IsFailure)
        {
            return same;
        }

        var valid = Validate(details);
        if (valid.IsFailure)
        {
            return valid;
        }

        Apply(details.Normalized());
        return Result.Success();
    }

    public Result EnsureSameEmployee(int employeeId) =>
        employeeId != EmployeeId
            ? Result.Failure(AppError.Validation(
                "The employee of a salary assignment cannot be changed.",
                "employeeId",
                "The employee cannot be changed."))
            : Result.Success();

    private void Apply(SalaryAssignmentDetails details)
    {
        SalaryStructureId = details.SalaryStructureId;
        BasicSalary = details.BasicSalary;
        EffectiveFrom = details.EffectiveFrom;
        Remarks = details.Remarks;
    }
}
