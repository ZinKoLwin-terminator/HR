using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;

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

/// <summary>The fields a position has on create and update.</summary>
internal interface IPositionFields
{
    int DepartmentId { get; }

    int? SectionId { get; }

    int JobGradeId { get; }

    string Code { get; }

    string Title { get; }

    string? Description { get; }

    short Headcount { get; }

    int? ReportsToPositionId { get; }
}

/// <summary>POST body. Status is the status the new position starts with.</summary>
public sealed record PositionCreateRequest(
    int DepartmentId,
    int? SectionId,
    int JobGradeId,
    string Code,
    string Title,
    string? Description,
    short Headcount,
    int? ReportsToPositionId,
    string Status) : IPositionFields, ICreateRequest<PositionDetails>
{
    public PositionDetails ToDetails() => PositionDetailsFactory.From(this);

    public RecordStatus ParseStatus() => Enum.Parse<RecordStatus>(Status);
}

/// <summary>PUT body. It has no status (use activate and deactivate) and requires the row version.</summary>
public sealed record PositionUpdateRequest(
    int DepartmentId,
    int? SectionId,
    int JobGradeId,
    string Code,
    string Title,
    string? Description,
    short Headcount,
    int? ReportsToPositionId,
    string? RowVersion) : IPositionFields, IDetailsRequest<PositionDetails>, IVersionedRequest
{
    public PositionDetails ToDetails() => PositionDetailsFactory.From(this);
}

internal static class PositionDetailsFactory
{
    public static PositionDetails From(IPositionFields fields) => new PositionDetails(
        fields.DepartmentId,
        fields.SectionId,
        fields.JobGradeId,
        fields.Code,
        fields.Title,
        fields.Description,
        fields.Headcount,
        fields.ReportsToPositionId).Normalized();
}

public sealed record PositionFilter(int? DepartmentId = null, int? JobGradeId = null, string? Status = null);
