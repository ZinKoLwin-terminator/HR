using Apex.Hrms.Application.UseCases;

namespace Apex.Hrms.Application.Organization.JobGrades;

/// <summary>Mirrors the JobGrade interface in organization.models.ts. Salary bands are sample monthly amounts.</summary>
public sealed record JobGradeDto(
    int Id,
    string Code,
    string Name,
    short Level,
    decimal MinSalary,
    decimal MaxSalary,
    string Description,
    string Status,
    byte[] RowVersion) : IIdentified;

public sealed record JobGradeRequest(
    string Code,
    string Name,
    short Level,
    decimal MinSalary,
    decimal MaxSalary,
    string? Description,
    string Status,
    byte[]? RowVersion = null) : IVersionedRequest;

public sealed record JobGradeFilter(string? Status = null);
