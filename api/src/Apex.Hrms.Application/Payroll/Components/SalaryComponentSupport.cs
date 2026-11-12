using System.Linq.Expressions;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Organization;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Payroll;
using FluentValidation;

namespace Apex.Hrms.Application.Payroll.Components;

internal sealed class SalaryComponentMapping :
    IEntityDescriptor<SalaryComponent>,
    IDtoMapper<SalaryComponent, SalaryComponentDto>,
    IEntityFactory<SalaryComponent, SalaryComponentDetails>
{
    public string Label => "Salary component";

    public string ActivityCategory => Domain.Activity.ActivityCategories.Payroll;

    public string Describe(SalaryComponent entity) => entity.Name;

    public SalaryComponentDto ToDto(SalaryComponent entity) => new(
        entity.Id,
        entity.Code,
        entity.Name,
        ComponentTypeText.ToText(entity.Type),
        CalculationMethodText.ToText(entity.Calculation),
        entity.Value,
        entity.Description ?? string.Empty,
        entity.Status.ToString(),
        entity.RowVersion);

    public Result Validate(SalaryComponentDetails details, SalaryComponent? existing) => SalaryComponent.Validate(details);

    public Result<SalaryComponent> Create(SalaryComponentDetails details, RecordStatus status) =>
        SalaryComponent.Create(details, status);

    public Result Update(SalaryComponent entity, SalaryComponentDetails details) => entity.Update(details);
}

/// <summary>The code of a component is unique.</summary>
internal sealed class SalaryComponentRules(IRepository<SalaryComponent> components) : ISaveRules<SalaryComponent, SalaryComponentDetails>
{
    public Task<Result> CheckCreateAsync(SalaryComponentDetails details, CancellationToken cancellationToken) =>
        EnsureUniqueCodeAsync(details, excludedId: 0, cancellationToken);

    public Task<Result> CheckUpdateAsync(SalaryComponent existing, SalaryComponentDetails details, CancellationToken cancellationToken) =>
        EnsureUniqueCodeAsync(details, existing.Id, cancellationToken);

    private Task<Result> EnsureUniqueCodeAsync(SalaryComponentDetails details, int excludedId, CancellationToken cancellationToken)
    {
        var code = details.Code;
        return HierarchyChecks.EnsureUniqueAsync(
            components, c => c.Code == code && c.Id != excludedId, "Component code", "code", code, cancellationToken);
    }
}

internal sealed class SalaryComponentQuerySpecification : IQuerySpecification<SalaryComponent, SalaryComponentFilter>
{
    public IReadOnlyDictionary<string, Expression<Func<SalaryComponent, object>>> SortKeys { get; } =
        new Dictionary<string, Expression<Func<SalaryComponent, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = c => c.Id,
            ["code"] = c => c.Code,
            ["name"] = c => c.Name,
            ["type"] = c => c.Type,
            ["calculation"] = c => c.Calculation,
            ["value"] = c => c.Value,
            ["status"] = c => c.Status,
        };

    public Expression<Func<SalaryComponent, bool>> Search(string term) =>
        Predicates.ContainsAny<SalaryComponent>(term, c => c.Code, c => c.Name);

    public Expression<Func<SalaryComponent, bool>> Filter(SalaryComponentFilter filter) =>
        FilterPredicates.Status<SalaryComponent>(filter.Status)
            .And(TypeFilter(filter.Type))
            .And(CalculationFilter(filter.Calculation));

    private static Expression<Func<SalaryComponent, bool>> TypeFilter(string? text)
    {
        if (!FilterPredicates.IsSet(text))
        {
            return Predicates.Always<SalaryComponent>();
        }

        return ComponentTypeText.TryParse(text, out var type) ? c => c.Type == type : Predicates.Never<SalaryComponent>();
    }

    private static Expression<Func<SalaryComponent, bool>> CalculationFilter(string? text)
    {
        if (!FilterPredicates.IsSet(text))
        {
            return Predicates.Always<SalaryComponent>();
        }

        return CalculationMethodText.TryParse(text, out var calculation)
            ? c => c.Calculation == calculation
            : Predicates.Never<SalaryComponent>();
    }
}

/// <summary>Shape rules shared by the create and update requests. Labels match the domain wording.</summary>
internal abstract class SalaryComponentFieldsValidator<TRequest> : RequestValidator<TRequest>
    where TRequest : ISalaryComponentFields
{
    protected SalaryComponentFieldsValidator()
    {
        RuleForText(x => x.Code, "Code").RequiredCode(20);
        RuleForText(x => x.Name, "Component name").RequiredText(60);
        RuleForText(x => x.Type, "Type")
            .Must(type => ComponentTypeText.TryParse(type?.Trim(), out _))
            .WithMessage($"Type must be {ComponentTypeText.Map.OneOf}.");
        RuleForText(x => x.Calculation, "Calculation")
            .Must(calculation => CalculationMethodText.TryParse(calculation?.Trim(), out _))
            .WithMessage($"Calculation must be {CalculationMethodText.Map.OneOf}.");
        RuleFor(x => x.Value)
            .PrecisionScale(18, 2, ignoreTrailingZeros: true).WithMessage("Use at most 2 decimal places.");
        RuleForText(x => x.Description, "Description").OptionalText(250);
    }
}

internal sealed class SalaryComponentCreateRequestValidator : SalaryComponentFieldsValidator<SalaryComponentCreateRequest>
{
    public SalaryComponentCreateRequestValidator() => RuleFor(x => x.Status).RequiredStatus();
}

internal sealed class SalaryComponentUpdateRequestValidator : SalaryComponentFieldsValidator<SalaryComponentUpdateRequest>
{
    public SalaryComponentUpdateRequestValidator() => RuleForText(x => x.RowVersion).RequiredRowVersion();
}
