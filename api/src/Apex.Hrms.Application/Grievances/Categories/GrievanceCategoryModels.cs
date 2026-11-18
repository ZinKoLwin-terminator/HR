using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Grievances;

namespace Apex.Hrms.Application.Grievances.Categories;

/// <summary>A kind of grievance (grv.Category). <c>description</c> is never null (empty when not set).</summary>
public sealed record GrievanceCategoryDto(
    int Id,
    string Code,
    string Name,
    string Description,
    bool AllowsAnonymous,
    int DefaultTargetDays,
    int SortOrder,
    string Status,
    byte[] RowVersion) : IIdentified;

/// <summary>The fields a grievance category has on create and update.</summary>
internal interface IGrievanceCategoryFields
{
    string Code { get; }

    string Name { get; }

    string? Description { get; }

    bool AllowsAnonymous { get; }

    int DefaultTargetDays { get; }

    int SortOrder { get; }
}

/// <summary>POST body. Status is the status the new category starts with.</summary>
public sealed record GrievanceCategoryCreateRequest(
    string Code,
    string Name,
    string? Description,
    bool AllowsAnonymous,
    int DefaultTargetDays,
    int SortOrder,
    string Status) : IGrievanceCategoryFields, ICreateRequest<GrievanceCategoryDetails>
{
    public GrievanceCategoryDetails ToDetails() => GrievanceCategoryDetailsFactory.From(this);

    public RecordStatus ParseStatus() => Enum.Parse<RecordStatus>(Status);
}

/// <summary>PUT body. It has no status (use activate and deactivate) and requires the row version.</summary>
public sealed record GrievanceCategoryUpdateRequest(
    string Code,
    string Name,
    string? Description,
    bool AllowsAnonymous,
    int DefaultTargetDays,
    int SortOrder,
    string? RowVersion) : IGrievanceCategoryFields, IDetailsRequest<GrievanceCategoryDetails>, IVersionedRequest
{
    public GrievanceCategoryDetails ToDetails() => GrievanceCategoryDetailsFactory.From(this);
}

internal static class GrievanceCategoryDetailsFactory
{
    public static GrievanceCategoryDetails From(IGrievanceCategoryFields fields) =>
        new GrievanceCategoryDetails(
            fields.Code,
            fields.Name,
            fields.Description,
            fields.AllowsAnonymous,
            (short)Math.Clamp(fields.DefaultTargetDays, short.MinValue, short.MaxValue),
            (short)Math.Clamp(fields.SortOrder, short.MinValue, short.MaxValue)).Normalized();
}

public sealed record GrievanceCategoryFilter(string? Status = null, bool? AllowsAnonymous = null);
