using System.Linq.Expressions;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Organization;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Payroll;
using FluentValidation;

namespace Apex.Hrms.Application.Payroll.Structures;

internal sealed class SalaryStructureMapping :
    IEntityDescriptor<SalaryStructure>,
    IDtoMapper<SalaryStructure, SalaryStructureDto>,
    IEntityFactory<SalaryStructure, SalaryStructureDetails>
{
    public string Label => "Salary structure";

    public string ActivityCategory => Domain.Activity.ActivityCategories.Payroll;

    public string Describe(SalaryStructure entity) => entity.Name;

    public SalaryStructureDto ToDto(SalaryStructure entity) => new(
        entity.Id,
        entity.Code,
        entity.Name,
        entity.Description ?? string.Empty,
        entity.ComponentIds,
        entity.Status.ToString(),
        entity.RowVersion);

    public Result Validate(SalaryStructureDetails details, SalaryStructure? existing) => SalaryStructure.Validate(details);

    public Result<SalaryStructure> Create(SalaryStructureDetails details, RecordStatus status) =>
        SalaryStructure.Create(details, status);

    public Result Update(SalaryStructure entity, SalaryStructureDetails details) => entity.Update(details);
}

/// <summary>The code is unique, every chosen component exists and at least one of them is an earning (an allowance).</summary>
internal sealed class SalaryStructureRules(
    IRepository<SalaryStructure> structures,
    IRepository<SalaryComponent> components) : ISaveRules<SalaryStructure, SalaryStructureDetails>
{
    public Task<Result> CheckCreateAsync(SalaryStructureDetails details, CancellationToken cancellationToken) =>
        CheckAsync(details, excludedId: 0, cancellationToken);

    public Task<Result> CheckUpdateAsync(SalaryStructure existing, SalaryStructureDetails details, CancellationToken cancellationToken) =>
        CheckAsync(details, existing.Id, cancellationToken);

    private async Task<Result> CheckAsync(SalaryStructureDetails details, int excludedId, CancellationToken cancellationToken)
    {
        var code = details.Code;
        var unique = await HierarchyChecks.EnsureUniqueAsync(
            structures, s => s.Code == code && s.Id != excludedId, "Structure code", "code", code, cancellationToken);
        if (unique.IsFailure)
        {
            return unique;
        }

        var ids = details.ComponentIds;
        var chosen = await components.ListAsync(component => ids.Contains(component.Id), cancellationToken);
        if (chosen.Count != ids.Count)
        {
            return Result.Failure(AppError.Validation(
                "Some chosen components no longer exist.", "componentIds", "Choose existing components."));
        }

        return chosen.Any(component => component.Type == ComponentType.Earning)
            ? Result.Success()
            : Result.Failure(AppError.Validation(
                "A structure needs at least one earning (an allowance).", "componentIds", "Include at least one earning."));
    }
}

internal sealed class SalaryStructureQuerySpecification : IQuerySpecification<SalaryStructure, SalaryStructureFilter>
{
    public IReadOnlyDictionary<string, Expression<Func<SalaryStructure, object>>> SortKeys { get; } =
        new Dictionary<string, Expression<Func<SalaryStructure, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = s => s.Id,
            ["code"] = s => s.Code,
            ["name"] = s => s.Name,
            ["status"] = s => s.Status,
        };

    public Expression<Func<SalaryStructure, bool>> Search(string term) =>
        Predicates.ContainsAny<SalaryStructure>(term, s => s.Code, s => s.Name);

    public Expression<Func<SalaryStructure, bool>> Filter(SalaryStructureFilter filter) =>
        FilterPredicates.Status<SalaryStructure>(filter.Status);
}

/// <summary>Shape rules shared by the create and update requests. The component list itself is checked by the domain.</summary>
internal abstract class SalaryStructureFieldsValidator<TRequest> : RequestValidator<TRequest>
    where TRequest : ISalaryStructureFields
{
    protected SalaryStructureFieldsValidator()
    {
        RuleForText(x => x.Code, "Code").RequiredCode(20);
        RuleForText(x => x.Name, "Structure name").RequiredText(60);
        RuleForText(x => x.Description, "Description").OptionalText(250);
    }
}

internal sealed class SalaryStructureCreateRequestValidator : SalaryStructureFieldsValidator<SalaryStructureCreateRequest>
{
    public SalaryStructureCreateRequestValidator() => RuleFor(x => x.Status).RequiredStatus();
}

internal sealed class SalaryStructureUpdateRequestValidator : SalaryStructureFieldsValidator<SalaryStructureUpdateRequest>
{
    public SalaryStructureUpdateRequestValidator() => RuleForText(x => x.RowVersion).RequiredRowVersion();
}
