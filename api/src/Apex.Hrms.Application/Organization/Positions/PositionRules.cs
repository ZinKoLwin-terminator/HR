using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.References;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Organization.Positions;

/// <summary>Limits of the position reporting structure.</summary>
public static class ReportingLimits
{
    /// <summary>Longest reporting chain that is walked when looking for a loop; anything deeper is rejected.</summary>
    public const int MaxDepth = 50;
}

internal sealed class PositionRules(
    IRepository<Position> positions,
    IRepository<Department> departments,
    IRepository<Section> sections,
    IRepository<JobGrade> jobGrades,
    IEnumerable<IReferenceCheck<Position>> references) : ISaveRules<Position, PositionDetails>
{
    public Task<Result> CheckCreateAsync(PositionDetails details, CancellationToken cancellationToken) =>
        CheckAsync(details, existing: null, cancellationToken);

    public Task<Result> CheckUpdateAsync(
        Position existing,
        PositionDetails details,
        CancellationToken cancellationToken) =>
        CheckAsync(details, existing, cancellationToken);

    private async Task<Result> CheckAsync(
        PositionDetails details,
        Position? existing,
        CancellationToken cancellationToken)
    {
        var code = details.Code;
        var excludedId = existing?.Id ?? 0;
        var unique = await HierarchyChecks.EnsureUniqueAsync(
            positions, p => p.Code == code && p.Id != excludedId, "Position code", "code", code, cancellationToken);
        if (unique.IsFailure)
        {
            return unique;
        }

        var department = await CheckDepartmentAsync(details, existing, cancellationToken);
        if (department.IsFailure)
        {
            return department;
        }

        var grade = await CheckJobGradeAsync(details, existing, cancellationToken);
        if (grade.IsFailure)
        {
            return grade;
        }

        var section = await CheckSectionAsync(details, existing, cancellationToken);
        if (section.IsFailure)
        {
            return section;
        }

        var reportsTo = await CheckReportsToAsync(details, existing, cancellationToken);
        if (reportsTo.IsFailure || existing is null || existing.DepartmentId == details.DepartmentId)
        {
            return reportsTo;
        }

        return await ReferenceUsage.EnsureCanMoveAsync(
            references, existing.Id, "Position", existing.Title, "department", "departmentId", cancellationToken);
    }

    private async Task<Result> CheckDepartmentAsync(
        PositionDetails details,
        Position? existing,
        CancellationToken cancellationToken)
    {
        if (existing?.DepartmentId == details.DepartmentId)
        {
            return Result.Success();
        }

        var department = await HierarchyChecks.FindParentAsync(
            departments, details.DepartmentId, "Department", "departmentId", cancellationToken);
        return department.IsFailure
            ? department
            : HierarchyChecks.EnsureActive(
                department.Value, "Department", department.Value.Name, "positions", "departmentId");
    }

    private async Task<Result> CheckJobGradeAsync(
        PositionDetails details,
        Position? existing,
        CancellationToken cancellationToken)
    {
        if (existing?.JobGradeId == details.JobGradeId)
        {
            return Result.Success();
        }

        var grade = await HierarchyChecks.FindParentAsync(
            jobGrades, details.JobGradeId, "Job grade", "jobGradeId", cancellationToken);
        return grade.IsFailure
            ? grade
            : HierarchyChecks.EnsureActive(
                grade.Value, "Job grade", $"{grade.Value.Code} {grade.Value.Name}", "positions", "jobGradeId");
    }

    private async Task<Result> CheckSectionAsync(
        PositionDetails details,
        Position? existing,
        CancellationToken cancellationToken)
    {
        if (details.SectionId is not { } sectionId)
        {
            return Result.Success();
        }

        var section = await HierarchyChecks.FindParentAsync(
            sections, sectionId, "Section", "sectionId", cancellationToken);
        if (section.IsFailure)
        {
            return section;
        }

        if (section.Value.DepartmentId != details.DepartmentId)
        {
            return Result.Failure(AppError.Validation(
                "The selected section does not belong to the selected department.",
                "sectionId",
                "Choose a section of the selected department."));
        }

        return existing?.SectionId == sectionId
            ? Result.Success()
            : HierarchyChecks.EnsureActive(section.Value, "Section", section.Value.Name, "positions", "sectionId");
    }

    private async Task<Result> CheckReportsToAsync(
        PositionDetails details,
        Position? existing,
        CancellationToken cancellationToken)
    {
        if (details.ReportsToPositionId is not { } reportsToId || existing?.ReportsToPositionId == reportsToId)
        {
            return Result.Success();
        }

        var manager = await HierarchyChecks.FindParentAsync(
            positions, reportsToId, "Position", "reportsToPositionId", cancellationToken);
        return manager.IsFailure || existing is null
            ? manager
            : await EnsureNoReportingLoopAsync(existing, manager.Value, cancellationToken);
    }

    /// <summary>
    /// Walks up the chain of managers starting at the new one. Reaching the position being changed means a loop;
    /// the walk is bounded so corrupt data cannot make it run forever.
    /// </summary>
    private async Task<Result> EnsureNoReportingLoopAsync(
        Position existing,
        Position newManager,
        CancellationToken cancellationToken)
    {
        var current = newManager;
        for (var depth = 0; depth < ReportingLimits.MaxDepth; depth++)
        {
            if (current.Id == existing.Id)
            {
                return Failure(
                    $"Position \"{existing.Title}\" cannot report to \"{newManager.Title}\" because that would create a reporting loop.",
                    "Choose a position that does not report to this one, directly or indirectly.");
            }

            if (current.ReportsToPositionId is not { } nextId)
            {
                return Result.Success();
            }

            var next = await positions.FindAsync(nextId, cancellationToken);
            if (next is null)
            {
                return Result.Success();
            }

            current = next;
        }

        return Failure(
            $"The reporting chain above \"{newManager.Title}\" is longer than {ReportingLimits.MaxDepth} positions.",
            "Choose a position higher up the structure.");
    }

    private static Result Failure(string message, string fieldMessage) =>
        Result.Failure(AppError.Validation(message, "reportsToPositionId", fieldMessage));
}
