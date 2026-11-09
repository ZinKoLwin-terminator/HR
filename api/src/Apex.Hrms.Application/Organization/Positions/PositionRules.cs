using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Organization.Positions;

internal sealed class PositionRules(
    IRepository<Position> positions,
    IRepository<Department> departments,
    IRepository<Section> sections,
    IRepository<JobGrade> jobGrades) : ISaveRules<Position, PositionRequest>
{
    public Task<Result> CheckCreateAsync(PositionRequest request, CancellationToken cancellationToken) =>
        CheckAsync(request, existing: null, cancellationToken);

    public Task<Result> CheckUpdateAsync(
        Position existing,
        PositionRequest request,
        CancellationToken cancellationToken) =>
        CheckAsync(request, existing, cancellationToken);

    private async Task<Result> CheckAsync(
        PositionRequest request,
        Position? existing,
        CancellationToken cancellationToken)
    {
        var code = request.Code.Trim();
        var excludedId = existing?.Id ?? 0;
        var unique = await HierarchyChecks.EnsureUniqueAsync(
            positions, p => p.Code == code && p.Id != excludedId, "Position code", "code", code, cancellationToken);
        if (unique.IsFailure)
        {
            return unique;
        }

        var department = await CheckDepartmentAsync(request, existing, cancellationToken);
        if (department.IsFailure)
        {
            return department;
        }

        var grade = await CheckJobGradeAsync(request, existing, cancellationToken);
        if (grade.IsFailure)
        {
            return grade;
        }

        var section = await CheckSectionAsync(request, existing, cancellationToken);
        return section.IsFailure ? section : await CheckReportsToAsync(request, existing, cancellationToken);
    }

    private async Task<Result> CheckDepartmentAsync(
        PositionRequest request,
        Position? existing,
        CancellationToken cancellationToken)
    {
        if (existing?.DepartmentId == request.DepartmentId)
        {
            return Result.Success();
        }

        var department = await HierarchyChecks.FindParentAsync(
            departments, request.DepartmentId, "Department", "departmentId", cancellationToken);
        return department.IsFailure
            ? department
            : HierarchyChecks.EnsureActive(
                department.Value, "Department", department.Value.Name, "positions", "departmentId");
    }

    private async Task<Result> CheckJobGradeAsync(
        PositionRequest request,
        Position? existing,
        CancellationToken cancellationToken)
    {
        if (existing?.JobGradeId == request.JobGradeId)
        {
            return Result.Success();
        }

        var grade = await HierarchyChecks.FindParentAsync(
            jobGrades, request.JobGradeId, "Job grade", "jobGradeId", cancellationToken);
        return grade.IsFailure
            ? grade
            : HierarchyChecks.EnsureActive(
                grade.Value, "Job grade", $"{grade.Value.Code} {grade.Value.Name}", "positions", "jobGradeId");
    }

    private async Task<Result> CheckSectionAsync(
        PositionRequest request,
        Position? existing,
        CancellationToken cancellationToken)
    {
        if (request.SectionId is not { } sectionId)
        {
            return Result.Success();
        }

        var section = await HierarchyChecks.FindParentAsync(
            sections, sectionId, "Section", "sectionId", cancellationToken);
        if (section.IsFailure)
        {
            return section;
        }

        if (section.Value.DepartmentId != request.DepartmentId)
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
        PositionRequest request,
        Position? existing,
        CancellationToken cancellationToken)
    {
        if (request.ReportsToPositionId is not { } reportsToId || existing?.ReportsToPositionId == reportsToId)
        {
            return Result.Success();
        }

        var manager = await HierarchyChecks.FindParentAsync(
            positions, reportsToId, "Position", "reportsToPositionId", cancellationToken);
        return manager.IsFailure ? manager : Result.Success();
    }
}
