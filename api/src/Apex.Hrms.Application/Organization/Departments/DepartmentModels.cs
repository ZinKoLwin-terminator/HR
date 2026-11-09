using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;

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

/// <summary>The fields a department has on create and update.</summary>
internal interface IDepartmentFields
{
    int CompanyId { get; }

    int BranchId { get; }

    string Code { get; }

    string Name { get; }

    string? Description { get; }

    int? HeadEmployeeId { get; }

    string? CostCenter { get; }
}

/// <summary>POST body. Status is the status the new department starts with.</summary>
public sealed record DepartmentCreateRequest(
    int CompanyId,
    int BranchId,
    string Code,
    string Name,
    string? Description,
    int? HeadEmployeeId,
    string? CostCenter,
    string Status) : IDepartmentFields, ICreateRequest<DepartmentDetails>
{
    public DepartmentDetails ToDetails() => DepartmentDetailsFactory.From(this);

    public RecordStatus ParseStatus() => Enum.Parse<RecordStatus>(Status);
}

/// <summary>PUT body. It has no status (use activate and deactivate) and requires the row version.</summary>
public sealed record DepartmentUpdateRequest(
    int CompanyId,
    int BranchId,
    string Code,
    string Name,
    string? Description,
    int? HeadEmployeeId,
    string? CostCenter,
    string? RowVersion) : IDepartmentFields, IDetailsRequest<DepartmentDetails>, IVersionedRequest
{
    public DepartmentDetails ToDetails() => DepartmentDetailsFactory.From(this);
}

internal static class DepartmentDetailsFactory
{
    public static DepartmentDetails From(IDepartmentFields fields) => new DepartmentDetails(
        fields.CompanyId,
        fields.BranchId,
        fields.Code,
        fields.Name,
        fields.Description,
        fields.HeadEmployeeId,
        fields.CostCenter).Normalized();
}

public sealed record DepartmentFilter(int? CompanyId = null, int? BranchId = null, string? Status = null);
