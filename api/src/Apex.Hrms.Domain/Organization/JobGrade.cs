using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Organization;

/// <summary>
/// The editable fields of a job grade. Salary figures are sample monthly bands; real pay rules need
/// jurisdiction sign-off. Status changes through Activate and Deactivate.
/// </summary>
public sealed record JobGradeDetails(
    string Code,
    string Name,
    short Level,
    decimal MinSalary,
    decimal MaxSalary,
    string? Description)
{
    /// <summary>Trims text and turns blank optional text into null. Idempotent.</summary>
    public JobGradeDetails Normalized() => this with
    {
        Code = TextNormalization.Clean(Code),
        Name = TextNormalization.Clean(Name),
        Description = TextNormalization.CleanOptional(Description),
    };
}

public sealed class JobGrade : CodedRecord
{
    private JobGrade()
    {
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>1 is the most junior level.</summary>
    public short Level { get; private set; }

    public decimal MinSalary { get; private set; }

    public decimal MaxSalary { get; private set; }

    public string? Description { get; private set; }

    public static Result<JobGrade> Create(JobGradeDetails details, RecordStatus status)
    {
        var grade = new JobGrade();
        var result = grade.Update(details);
        if (result.IsFailure)
        {
            return Result.Failure<JobGrade>(result.Error);
        }

        grade.SetInitialStatus(status);
        return Result.Success(grade);
    }

    /// <summary>Checks the rules that need no other record, without changing anything.</summary>
    public static Result Validate(JobGradeDetails details)
    {
        var normalized = details.Normalized();
        var violations = new Violations();
        CheckCode(violations, normalized.Code);
        violations
            .Require("name", "Grade name", normalized.Name)
            .AddIf(normalized.Level < 1, "level", "Level must be at least 1.")
            .AddIf(normalized.MinSalary < 0, "minSalary", "Must not be negative.");
        if (normalized.MaxSalary < normalized.MinSalary)
        {
            violations.Add(
                "maxSalary",
                "Must be ≥ minimum salary.",
                "Maximum salary must be greater than or equal to minimum salary.");
        }

        return violations.ToResult();
    }

    /// <summary>Applies the details, or changes nothing when any rule is violated. Status is left untouched.</summary>
    public Result Update(JobGradeDetails details)
    {
        var valid = Validate(details);
        if (valid.IsFailure)
        {
            return valid;
        }

        var normalized = details.Normalized();
        AssignCode(normalized.Code);
        Name = normalized.Name;
        Level = normalized.Level;
        MinSalary = normalized.MinSalary;
        MaxSalary = normalized.MaxSalary;
        Description = normalized.Description;
        return Result.Success();
    }
}
