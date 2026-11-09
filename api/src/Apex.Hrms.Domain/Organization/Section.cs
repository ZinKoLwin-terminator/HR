using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Organization;

public sealed record SectionDetails(
    int DepartmentId,
    string Code,
    string Name,
    string? Description,
    int? SupervisorEmployeeId,
    RecordStatus Status);

public sealed class Section : CodedRecord
{
    private Section()
    {
    }

    public int DepartmentId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public int? SupervisorEmployeeId { get; private set; }

    public static Result<Section> Create(SectionDetails details)
    {
        var section = new Section();
        var result = section.Update(details);
        return result.IsFailure ? Result.Failure<Section>(result.Error) : Result.Success(section);
    }

    /// <summary>Applies the details, or changes nothing when any rule is violated.</summary>
    public Result Update(SectionDetails details)
    {
        var violations = new Violations();
        CheckCode(violations, details.Code);
        violations.Require("name", "Section name", details.Name);
        if (violations.Any)
        {
            return violations.ToResult();
        }

        AssignCodeAndStatus(details.Code, details.Status);
        DepartmentId = details.DepartmentId;
        Name = TextNormalization.Clean(details.Name);
        Description = TextNormalization.CleanOptional(details.Description);
        SupervisorEmployeeId = details.SupervisorEmployeeId;
        return Result.Success();
    }
}
