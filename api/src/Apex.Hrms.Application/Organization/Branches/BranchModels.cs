using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;

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

/// <summary>The fields a branch has on create and update.</summary>
internal interface IBranchFields
{
    int CompanyId { get; }

    string Code { get; }

    string Name { get; }

    string Type { get; }

    string? Email { get; }

    string? Phone { get; }

    string? Address { get; }

    string City { get; }

    string Country { get; }

    int? ManagerEmployeeId { get; }

    DateOnly OpenedDate { get; }
}

/// <summary>POST body. Status is the status the new branch starts with.</summary>
public sealed record BranchCreateRequest(
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
    string Status) : IBranchFields, ICreateRequest<BranchDetails>
{
    public BranchDetails ToDetails() => BranchDetailsFactory.From(this);

    public RecordStatus ParseStatus() => Enum.Parse<RecordStatus>(Status);
}

/// <summary>PUT body. It has no status (use activate and deactivate) and requires the row version.</summary>
public sealed record BranchUpdateRequest(
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
    string? RowVersion) : IBranchFields, IDetailsRequest<BranchDetails>, IVersionedRequest
{
    public BranchDetails ToDetails() => BranchDetailsFactory.From(this);
}

internal static class BranchDetailsFactory
{
    public static BranchDetails From(IBranchFields fields) => new BranchDetails(
        fields.CompanyId,
        fields.Code,
        fields.Name,
        BranchTypeText.Parse(fields.Type.Trim()),
        fields.Email,
        fields.Phone,
        fields.Address,
        fields.City,
        fields.Country,
        fields.ManagerEmployeeId,
        fields.OpenedDate).Normalized();
}

public sealed record BranchFilter(int? CompanyId = null, string? Type = null, string? Status = null);
