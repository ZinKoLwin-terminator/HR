using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Organization;

/// <summary>The editable fields of a section. Status changes through Activate and Deactivate.</summary>
public sealed record SectionDetails(
    int DepartmentId,
    string Code,
    string Name,
    string? Description,
    int? SupervisorEmployeeId)
{
    /// <summary>Trims text and turns blank optional text into null. Idempotent.</summary>
    public SectionDetails Normalized() => this with
    {
        Code = TextNormalization.Clean(Code),
        Name = TextNormalization.Clean(Name),
        Description = TextNormalization.CleanOptional(Description),
    };
}

public sealed class Section : CodedRecord
{
    private Section()
    {
    }

    public int DepartmentId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public int? SupervisorEmployeeId { get; private set; }

    public static Result<Section> Create(SectionDetails details, RecordStatus status)
    {
        var section = new Section();
        var result = section.Update(details);
        if (result.IsFailure)
        {
            return Result.Failure<Section>(result.Error);
        }

        section.SetInitialStatus(status);
        return Result.Success(section);
    }

    /// <summary>Checks the rules that need no other record, without changing anything.</summary>
    public static Result Validate(SectionDetails details)
    {
        var normalized = details.Normalized();
        var violations = new Violations();
        CheckCode(violations, normalized.Code);
        violations.Require("name", "Section name", normalized.Name);
        return violations.ToResult();
    }

    /// <summary>Applies the details, or changes nothing when any rule is violated. Status is left untouched.</summary>
    public Result Update(SectionDetails details)
    {
        var valid = Validate(details);
        if (valid.IsFailure)
        {
            return valid;
        }

        var normalized = details.Normalized();
        AssignCode(normalized.Code);
        DepartmentId = normalized.DepartmentId;
        Name = normalized.Name;
        Description = normalized.Description;
        SupervisorEmployeeId = normalized.SupervisorEmployeeId;
        return Result.Success();
    }
}
