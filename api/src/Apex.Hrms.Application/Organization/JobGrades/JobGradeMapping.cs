using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Organization.JobGrades;

internal sealed class JobGradeMapping :
    IEntityDescriptor<JobGrade>,
    IDtoMapper<JobGrade, JobGradeDto>,
    IEntityFactory<JobGrade, JobGradeDetails>
{
    public string Label => "Job grade";

    public string ActivityCategory => Domain.Activity.ActivityCategories.Organization;

    public string Describe(JobGrade entity) => $"{entity.Code} {entity.Name}";

    public JobGradeDto ToDto(JobGrade entity) => new(
        entity.Id,
        entity.Code,
        entity.Name,
        entity.Level,
        entity.MinSalary,
        entity.MaxSalary,
        entity.Description ?? string.Empty,
        entity.Status.ToString(),
        entity.RowVersion);

    public Result Validate(JobGradeDetails details, JobGrade? existing) => JobGrade.Validate(details);

    public Result<JobGrade> Create(JobGradeDetails details, RecordStatus status) => JobGrade.Create(details, status);

    public Result Update(JobGrade entity, JobGradeDetails details) => entity.Update(details);
}
