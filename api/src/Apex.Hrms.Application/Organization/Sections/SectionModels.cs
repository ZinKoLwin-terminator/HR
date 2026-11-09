using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;

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

/// <summary>The fields a section has on create and update.</summary>
internal interface ISectionFields
{
    int DepartmentId { get; }

    string Code { get; }

    string Name { get; }

    string? Description { get; }

    int? SupervisorEmployeeId { get; }
}

/// <summary>POST body. Status is the status the new section starts with.</summary>
public sealed record SectionCreateRequest(
    int DepartmentId,
    string Code,
    string Name,
    string? Description,
    int? SupervisorEmployeeId,
    string Status) : ISectionFields, ICreateRequest<SectionDetails>
{
    public SectionDetails ToDetails() => SectionDetailsFactory.From(this);

    public RecordStatus ParseStatus() => Enum.Parse<RecordStatus>(Status);
}

/// <summary>PUT body. It has no status (use activate and deactivate) and requires the row version.</summary>
public sealed record SectionUpdateRequest(
    int DepartmentId,
    string Code,
    string Name,
    string? Description,
    int? SupervisorEmployeeId,
    string? RowVersion) : ISectionFields, IDetailsRequest<SectionDetails>, IVersionedRequest
{
    public SectionDetails ToDetails() => SectionDetailsFactory.From(this);
}

internal static class SectionDetailsFactory
{
    public static SectionDetails From(ISectionFields fields) => new SectionDetails(
        fields.DepartmentId,
        fields.Code,
        fields.Name,
        fields.Description,
        fields.SupervisorEmployeeId).Normalized();
}

public sealed record SectionFilter(int? DepartmentId = null, string? Status = null);
