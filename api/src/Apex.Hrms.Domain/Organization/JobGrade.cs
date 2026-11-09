using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Organization;

/// <summary>Salary figures are sample monthly bands; real pay rules need jurisdiction sign-off.</summary>
public sealed record JobGradeDetails(
    string Code,
    string Name,
    short Level,
    decimal MinSalary,
    decimal MaxSalary,
    string? Description,
    RecordStatus Status);

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

    public static Result<JobGrade> Create(JobGradeDetails details)
    {
        var grade = new JobGrade();
        var result = grade.Update(details);
        return result.IsFailure ? Result.Failure<JobGrade>(result.Error) : Result.Success(grade);
    }

    /// <summary>Applies the details, or changes nothing when any rule is violated.</summary>
    public Result Update(JobGradeDetails details)
    {
        var violations = new Violations();
        CheckCode(violations, details.Code);
        violations
            .Require("name", "Grade name", details.Name)
            .AddIf(details.Level < 1, "level", "Level must be at least 1.")
            .AddIf(details.MinSalary < 0, "minSalary", "Must not be negative.");
        if (details.MaxSalary < details.MinSalary)
        {
            violations.Add(
                "maxSalary",
                "Must be ≥ minimum salary.",
                "Maximum salary must be greater than or equal to minimum salary.");
        }

        if (violations.Any)
        {
            return violations.ToResult();
        }

        AssignCodeAndStatus(details.Code, details.Status);
        Name = TextNormalization.Clean(details.Name);
        Level = details.Level;
        MinSalary = details.MinSalary;
        MaxSalary = details.MaxSalary;
        Description = TextNormalization.CleanOptional(details.Description);
        return Result.Success();
    }
}
