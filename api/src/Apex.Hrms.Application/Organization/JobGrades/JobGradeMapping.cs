using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Organization.JobGrades;

internal sealed class JobGradeMapping :
    IEntityDescriptor<JobGrade>,
    IDtoMapper<JobGrade, JobGradeDto>,
    IEntityFactory<JobGrade, JobGradeRequest>
{
    public string Label => "Job grade";

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

    public Result<JobGrade> Create(JobGradeRequest request) => JobGrade.Create(ToDetails(request));

    public Result Update(JobGrade entity, JobGradeRequest request) => entity.Update(ToDetails(request));

    private static JobGradeDetails ToDetails(JobGradeRequest request) => new(
        request.Code,
        request.Name,
        request.Level,
        request.MinSalary,
        request.MaxSalary,
        request.Description,
        Enum.Parse<RecordStatus>(request.Status));
}
