using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Grievances;

namespace Apex.Hrms.Application.Grievances.Categories;

/// <summary>
/// Grievance categories through the shared CRUD flow. Categories are HR master data, not case data: the shared flow records its usual
/// created/updated/deleted line in the activity feed (category "system", subject the category code), never a case, a submitter or a
/// count. The cases themselves never reach app.ActivityLog.
/// </summary>
internal sealed class GrievanceCategoryMapping :
    IEntityDescriptor<GrievanceCategory>,
    IDtoMapper<GrievanceCategory, GrievanceCategoryDto>,
    IEntityFactory<GrievanceCategory, GrievanceCategoryDetails>
{
    public string Label => "Grievance category";

    public string Describe(GrievanceCategory entity) => entity.Name;

    public string ActivitySubject(GrievanceCategory entity) => entity.Code;

    public GrievanceCategoryDto ToDto(GrievanceCategory entity) => new(
        entity.Id,
        entity.Code,
        entity.Name,
        entity.Description ?? string.Empty,
        entity.AllowsAnonymous,
        entity.DefaultTargetDays,
        entity.SortOrder,
        entity.Status.ToString(),
        entity.RowVersion);

    public Result Validate(GrievanceCategoryDetails details, GrievanceCategory? existing) => GrievanceCategory.Validate(details);

    public Result<GrievanceCategory> Create(GrievanceCategoryDetails details, RecordStatus status) => GrievanceCategory.Create(details, status);

    public Result Update(GrievanceCategory entity, GrievanceCategoryDetails details) => entity.Update(details);
}
