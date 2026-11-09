using Apex.Hrms.Application.UseCases;

namespace Apex.Hrms.Application.Organization.Branches;

/// <summary>Mirrors the Branch interface in organization.models.ts. Type is "Head Office", "Branch", "Regional Office" or "Warehouse".</summary>
public sealed record BranchDto(
    int Id,
    int CompanyId,
    string Code,
    string Name,
    string Type,
    string Email,
    string Phone,
    string Address,
    string City,
    string Country,
    int? ManagerEmployeeId,
    DateOnly OpenedDate,
    string Status,
    byte[] RowVersion) : IIdentified;

public sealed record BranchRequest(
    int CompanyId,
    string Code,
    string Name,
    string Type,
    string? Email,
    string? Phone,
    string? Address,
    string City,
    string Country,
    int? ManagerEmployeeId,
    DateOnly OpenedDate,
    string Status,
    byte[]? RowVersion = null) : IVersionedRequest;

public sealed record BranchFilter(int? CompanyId = null, string? Type = null, string? Status = null);
