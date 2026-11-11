using System.Linq.Expressions;
using Apex.Hrms.Application.Organization;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Recruitment;
using FluentValidation;

namespace Apex.Hrms.Application.Recruitment.JobOpenings;

internal sealed class JobOpeningMapping :
    IEntityDescriptor<JobOpening>,
    IDtoMapper<JobOpening, JobOpeningDto>
{
    public string Label => "Job opening";

    public string ActivityCategory => Domain.Activity.ActivityCategories.Recruitment;

    public string Describe(JobOpening entity) => entity.Label;

    public JobOpeningDto ToDto(JobOpening entity) => new(
        entity.Id,
        entity.Code,
        entity.Title,
        entity.DepartmentId,
        entity.HiringManagerEmployeeId,
        RecruitmentEmploymentTypeText.ToText(entity.EmploymentType),
        entity.Vacancies,
        entity.PostedOn,
        entity.ClosingDate,
        entity.Description,
        JobOpeningStatusText.ToText(entity.Status),
        entity.RowVersion);
}

internal sealed class JobOpeningQuerySpecification : IQuerySpecification<JobOpening, JobOpeningFilter>
{
    public IReadOnlyDictionary<string, Expression<Func<JobOpening, object>>> SortKeys { get; } =
        new Dictionary<string, Expression<Func<JobOpening, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = j => j.Id,
            ["code"] = j => j.Code,
            ["title"] = j => j.Title,
            ["departmentId"] = j => j.DepartmentId,
            ["hiringManagerId"] = j => j.HiringManagerEmployeeId,
            ["employmentType"] = j => j.EmploymentType,
            ["vacancies"] = j => j.Vacancies,
            ["postedOn"] = j => j.PostedOn,
            ["closingDate"] = j => j.ClosingDate,
            ["status"] = j => j.Status,
        };

    public Expression<Func<JobOpening, bool>> Search(string term) =>
        Predicates.ContainsAny<JobOpening>(term, j => j.Code, j => j.Title);

    public Expression<Func<JobOpening, bool>> Filter(JobOpeningFilter filter) => Predicates.Always<JobOpening>()
        .And(StatusFilter(filter.Status))
        .And(filter.DepartmentId is { } departmentId ? j => j.DepartmentId == departmentId : Predicates.Always<JobOpening>())
        .And(filter.HiringManagerId is { } managerId ? j => j.HiringManagerEmployeeId == managerId : Predicates.Always<JobOpening>())
        .And(EmploymentTypeFilter(filter.EmploymentType));

    private static Expression<Func<JobOpening, bool>> StatusFilter(string? text)
    {
        if (!FilterPredicates.IsSet(text))
        {
            return Predicates.Always<JobOpening>();
        }

        return JobOpeningStatusText.TryParse(text, out var status)
            ? j => j.Status == status
            : Predicates.Never<JobOpening>();
    }

    private static Expression<Func<JobOpening, bool>> EmploymentTypeFilter(string? text)
    {
        if (!FilterPredicates.IsSet(text))
        {
            return Predicates.Always<JobOpening>();
        }

        return RecruitmentEmploymentTypeText.TryParse(text, out var type)
            ? j => j.EmploymentType == type
            : Predicates.Never<JobOpening>();
    }
}

/// <summary>Limits of a job opening that follow from the database columns.</summary>
public static class JobOpeningLimits
{
    public const int TitleMaxLength = 80;
    public const int DescriptionMaxLength = 500;
}

/// <summary>Shape rules shared by the create and update requests. The domain owns vacancies and the dates.</summary>
internal abstract class JobOpeningFieldsValidator<TRequest> : RequestValidator<TRequest>
    where TRequest : IJobOpeningFields
{
    protected JobOpeningFieldsValidator()
    {
        RuleForText(x => x.Title, "Title").RequiredText(JobOpeningLimits.TitleMaxLength);
        RuleForText(x => x.Description, "Description").OptionalText(JobOpeningLimits.DescriptionMaxLength);
        RuleForText(x => x.EmploymentType, "Employment type")
            .NotEmpty().WithMessage("Employment type is required.")
            .Must(type => RecruitmentEmploymentTypeText.TryParse(type?.Trim(), out _))
            .WithMessage($"Employment type must be {RecruitmentEmploymentTypeText.Map.OneOf}.");
    }
}

internal sealed class JobOpeningCreateRequestValidator : JobOpeningFieldsValidator<JobOpeningCreateRequest>;

internal sealed class JobOpeningUpdateRequestValidator : JobOpeningFieldsValidator<JobOpeningUpdateRequest>
{
    public JobOpeningUpdateRequestValidator() => RuleForText(x => x.RowVersion).RequiredRowVersion();
}
