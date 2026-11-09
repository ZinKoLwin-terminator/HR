using Apex.Hrms.Application.UseCases;

namespace Apex.Hrms.Application.Organization.Departments;

/// <summary>Mirrors the Department interface in organization.models.ts.</summary>
public sealed record DepartmentDto(
    int Id,
    int CompanyId,
    int BranchId,
    string Code,
    string Name,
    string Description,
    int? HeadEmployeeId,
    string CostCenter,
    string Status,
    byte[] RowVersion) : IIdentified;

public sealed record DepartmentRequest(
    int CompanyId,
    int BranchId,
    string Code,
    string Name,
    string? Description,
    int? HeadEmployeeId,
    string? CostCenter,
    string Status,
    byte[]? RowVersion = null) : IVersionedRequest;

public sealed record DepartmentFilter(int? CompanyId = null, int? BranchId = null, string? Status = null);
