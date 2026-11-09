using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Organization.JobGrades;

/// <summary>Mirrors the JobGrade interface in organization.models.ts. Salary bands are sample monthly amounts.</summary>
public sealed record JobGradeDto(
    int Id,
    string Code,
    string Name,
    short Level,
    decimal MinSalary,
    decimal MaxSalary,
    string Description,
    string Status,
    byte[] RowVersion) : IIdentified;

/// <summary>The fields a job grade has on create and update.</summary>
internal interface IJobGradeFields
{
    string Code { get; }

    string Name { get; }

    short Level { get; }

    decimal MinSalary { get; }

    decimal MaxSalary { get; }

    string? Description { get; }
}

/// <summary>POST body. Status is the status the new job grade starts with.</summary>
public sealed record JobGradeCreateRequest(
    string Code,
    string Name,
    short Level,
    decimal MinSalary,
    decimal MaxSalary,
    string? Description,
    string Status) : IJobGradeFields, ICreateRequest<JobGradeDetails>
{
    public JobGradeDetails ToDetails() => JobGradeDetailsFactory.From(this);

    public RecordStatus ParseStatus() => Enum.Parse<RecordStatus>(Status);
}

/// <summary>PUT body. It has no status (use activate and deactivate) and requires the row version.</summary>
public sealed record JobGradeUpdateRequest(
    string Code,
    string Name,
    short Level,
    decimal MinSalary,
    decimal MaxSalary,
    string? Description,
    string? RowVersion) : IJobGradeFields, IDetailsRequest<JobGradeDetails>, IVersionedRequest
{
    public JobGradeDetails ToDetails() => JobGradeDetailsFactory.From(this);
}

internal static class JobGradeDetailsFactory
{
    public static JobGradeDetails From(IJobGradeFields fields) => new JobGradeDetails(
        fields.Code,
        fields.Name,
        fields.Level,
        fields.MinSalary,
        fields.MaxSalary,
        fields.Description).Normalized();
}

public sealed record JobGradeFilter(string? Status = null);
