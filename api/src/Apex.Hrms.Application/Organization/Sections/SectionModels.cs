using Apex.Hrms.Application.UseCases;

namespace Apex.Hrms.Application.Organization.Sections;

/// <summary>Mirrors the Section interface in organization.models.ts.</summary>
public sealed record SectionDto(
    int Id,
    int DepartmentId,
    string Code,
    string Name,
    string Description,
    int? SupervisorEmployeeId,
    string Status,
    byte[] RowVersion) : IIdentified;

public sealed record SectionRequest(
    int DepartmentId,
    string Code,
    string Name,
    string? Description,
    int? SupervisorEmployeeId,
    string Status,
    byte[]? RowVersion = null) : IVersionedRequest;

public sealed record SectionFilter(int? DepartmentId = null, string? Status = null);
