using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Payroll;

/// <summary>
/// The editable fields of a salary component. <see cref="Value"/> is an amount for <see cref="CalculationMethod.Fixed"/>
/// and a percentage (above 0, at most 100) otherwise. DEMONSTRATION values only: not statutory tax or social security
/// rates. Status changes through Activate and Deactivate.
/// </summary>
public sealed record SalaryComponentDetails(
    string Code,
    string Name,
    ComponentType Type,
    CalculationMethod Calculation,
    decimal Value,
    string? Description)
{
    /// <summary>Trims text and turns a blank description into null. Idempotent.</summary>
    public SalaryComponentDetails Normalized() => this with
    {
        Code = TextNormalization.Clean(Code),
        Name = TextNormalization.Clean(Name),
        Description = TextNormalization.CleanOptional(Description),
    };
}

/// <summary>A reusable pay element, an allowance or a deduction (pay.SalaryComponent).</summary>
public sealed class SalaryComponent : CodedRecord
{
    public const decimal MaxPercentage = 100m;

    private SalaryComponent()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public ComponentType Type { get; private set; }

    public CalculationMethod Calculation { get; private set; }

    /// <summary>The amount of a fixed component, otherwise a percentage from above 0 to 100.</summary>
    public decimal Value { get; private set; }

    public string? Description { get; private set; }

    public static Result<SalaryComponent> Create(SalaryComponentDetails details, RecordStatus status)
    {
        var component = new SalaryComponent();
        var result = component.Update(details);
        if (result.IsFailure)
        {
            return Result.Failure<SalaryComponent>(result.Error);
        }

        component.SetInitialStatus(status);
        return Result.Success(component);
    }

    /// <summary>Checks the rules that need no other record, without changing anything.</summary>
    public static Result Validate(SalaryComponentDetails details)
    {
        var normalized = details.Normalized();
        var violations = new Violations();
        CheckCode(violations, normalized.Code);
        violations.Require("name", "Component name", normalized.Name);
        if (!(normalized.Value > 0))
        {
            violations.Add("value", "Must be greater than 0.", "The amount or percentage must be greater than zero.");
        }
        else if (normalized.Calculation != CalculationMethod.Fixed && normalized.Value > MaxPercentage)
        {
            violations.Add("value", "Enter a percentage up to 100.", "A percentage cannot exceed 100.");
        }

        return violations.ToResult();
    }

    /// <summary>Applies the details, or changes nothing when any rule is violated. Status is left untouched.</summary>
    public Result Update(SalaryComponentDetails details)
    {
        var valid = Validate(details);
        if (valid.IsFailure)
        {
            return valid;
        }

        var normalized = details.Normalized();
        AssignCode(normalized.Code);
        Name = normalized.Name;
        Type = normalized.Type;
        Calculation = normalized.Calculation;
        Value = normalized.Value;
        Description = normalized.Description;
        return Result.Success();
    }
}
