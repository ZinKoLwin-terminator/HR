using Apex.Hrms.Application.UseCases;

namespace Apex.Hrms.Application.Organization.Positions;

/// <summary>Mirrors the Position interface in organization.models.ts.</summary>
public sealed record PositionDto(
    int Id,
    int DepartmentId,
    int? SectionId,
    int JobGradeId,
    string Code,
    string Title,
    string Description,
    short Headcount,
    int? ReportsToPositionId,
    string Status,
    byte[] RowVersion) : IIdentified;

public sealed record PositionRequest(
    int DepartmentId,
    int? SectionId,
    int JobGradeId,
    string Code,
    string Title,
    string? Description,
    short Headcount,
    int? ReportsToPositionId,
    string Status,
    byte[]? RowVersion = null) : IVersionedRequest;

public sealed record PositionFilter(int? DepartmentId = null, int? JobGradeId = null, string? Status = null);
