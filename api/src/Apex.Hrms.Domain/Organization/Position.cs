using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Organization;

/// <summary>The editable fields of a position. Status changes through Activate and Deactivate.</summary>
public sealed record PositionDetails(
    int DepartmentId,
    int? SectionId,
    int JobGradeId,
    string Code,
    string Title,
    string? Description,
    short Headcount,
    int? ReportsToPositionId)
{
    /// <summary>Trims text and turns blank optional text into null. Idempotent.</summary>
    public PositionDetails Normalized() => this with
    {
        Code = TextNormalization.Clean(Code),
        Title = TextNormalization.Clean(Title),
        Description = TextNormalization.CleanOptional(Description),
    };
}

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

    public static Result<Position> Create(PositionDetails details, RecordStatus status)
    {
        var position = new Position();
        var result = position.Update(details);
        if (result.IsFailure)
        {
            return Result.Failure<Position>(result.Error);
        }

        position.SetInitialStatus(status);
        return Result.Success(position);
    }

    /// <summary>
    /// Checks the rules that need no other record, without changing anything. <paramref name="ownId"/> is the id of
    /// the position being changed (0 for a new one).
    /// </summary>
    public static Result Validate(PositionDetails details, int ownId = 0)
    {
        var normalized = details.Normalized();
        var violations = new Violations();
        CheckCode(violations, normalized.Code);
        violations
            .Require("title", "Position title", normalized.Title)
            .AddIf(normalized.Headcount < 1, "headcount", "Headcount must be at least 1.");
        if (ownId != 0 && normalized.ReportsToPositionId == ownId)
        {
            violations.Add(
                "reportsToPositionId",
                "Choose a different position.",
                "A position cannot report to itself.");
        }

        return violations.ToResult();
    }

    /// <summary>Applies the details, or changes nothing when any rule is violated. Status is left untouched.</summary>
    public Result Update(PositionDetails details)
    {
        var valid = Validate(details, Id);
        if (valid.IsFailure)
        {
            return valid;
        }

        var normalized = details.Normalized();
        AssignCode(normalized.Code);
        DepartmentId = normalized.DepartmentId;
        SectionId = normalized.SectionId;
        JobGradeId = normalized.JobGradeId;
        Title = normalized.Title;
        Description = normalized.Description;
        Headcount = normalized.Headcount;
        ReportsToPositionId = normalized.ReportsToPositionId;
        return Result.Success();
    }
}
