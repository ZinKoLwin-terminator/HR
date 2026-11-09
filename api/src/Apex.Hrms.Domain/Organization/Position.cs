using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Organization;

public sealed record PositionDetails(
    int DepartmentId,
    int? SectionId,
    int JobGradeId,
    string Code,
    string Title,
    string? Description,
    short Headcount,
    int? ReportsToPositionId,
    RecordStatus Status);

public sealed class Position : CodedRecord
{
    private Position()
    {
    }

    public int DepartmentId { get; private set; }

    public int? SectionId { get; private set; }

    public int JobGradeId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>Approved headcount for this position.</summary>
    public short Headcount { get; private set; }

    public int? ReportsToPositionId { get; private set; }

    public static Result<Position> Create(PositionDetails details)
    {
        var position = new Position();
        var result = position.Update(details);
        return result.IsFailure ? Result.Failure<Position>(result.Error) : Result.Success(position);
    }

    /// <summary>Applies the details, or changes nothing when any rule is violated.</summary>
    public Result Update(PositionDetails details)
    {
        var violations = new Violations();
        CheckCode(violations, details.Code);
        violations
            .Require("title", "Position title", details.Title)
            .AddIf(details.Headcount < 1, "headcount", "Headcount must be at least 1.");
        if (Id != 0 && details.ReportsToPositionId == Id)
        {
            violations.Add(
                "reportsToPositionId",
                "Choose a different position.",
                "A position cannot report to itself.");
        }

        if (violations.Any)
        {
            return violations.ToResult();
        }

        AssignCodeAndStatus(details.Code, details.Status);
        DepartmentId = details.DepartmentId;
        SectionId = details.SectionId;
        JobGradeId = details.JobGradeId;
        Title = TextNormalization.Clean(details.Title);
        Description = TextNormalization.CleanOptional(details.Description);
        Headcount = details.Headcount;
        ReportsToPositionId = details.ReportsToPositionId;
        return Result.Success();
    }
}
