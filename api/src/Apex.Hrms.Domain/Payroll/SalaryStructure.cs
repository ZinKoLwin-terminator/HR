using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Payroll;

/// <summary>The editable fields of a salary structure. <see cref="ComponentIds"/> is the display order. Status changes through Activate and Deactivate.</summary>
public sealed record SalaryStructureDetails(string Code, string Name, string? Description, IReadOnlyList<int> ComponentIds)
{
    /// <summary>Trims text and turns a blank description into null. Idempotent.</summary>
    public SalaryStructureDetails Normalized() => this with
    {
        Code = TextNormalization.Clean(Code),
        Name = TextNormalization.Clean(Name),
        Description = TextNormalization.CleanOptional(Description),
    };
}

/// <summary>One component of a structure and its place in the display order (pay.SalaryStructureComponent).</summary>
public sealed class SalaryStructureComponent
{
    private SalaryStructureComponent()
    {
    }

    internal SalaryStructureComponent(int salaryComponentId, short sortOrder)
    {
        SalaryComponentId = salaryComponentId;
        SortOrder = sortOrder;
    }

    public int SalaryStructureId { get; private set; }

    public int SalaryComponentId { get; private set; }

    public short SortOrder { get; private set; }

    internal void MoveTo(short sortOrder) => SortOrder = sortOrder;
}

/// <summary>
/// A named set of components applied to employees (pay.SalaryStructure). The cross-record rules (the components exist and
/// one is an earning) are checked in the Application layer; the rules that need no other record are here.
/// </summary>
public sealed class SalaryStructure : CodedRecord
{
    public const int MaxComponents = 50;

    // The table keeps the order in SortOrder under a unique key, so a reorder that moves rows onto each other's values
    // could collide while it is being saved. Every change therefore writes the new order into the other half of the number
    // range (1..Half or Half+1..), which no row of the old order uses.
    private const short Half = 1000;

    private readonly List<SalaryStructureComponent> components = [];

    private SalaryStructure()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public IReadOnlyCollection<SalaryStructureComponent> Components => components;

    /// <summary>The component ids in display order.</summary>
    public IReadOnlyList<int> ComponentIds =>
        [.. components.OrderBy(component => component.SortOrder).Select(component => component.SalaryComponentId)];

    public static Result<SalaryStructure> Create(SalaryStructureDetails details, RecordStatus status)
    {
        var structure = new SalaryStructure();
        var result = structure.Update(details);
        if (result.IsFailure)
        {
            return Result.Failure<SalaryStructure>(result.Error);
        }

        structure.SetInitialStatus(status);
        return Result.Success(structure);
    }

    /// <summary>Checks the rules that need no other record, without changing anything.</summary>
    public static Result Validate(SalaryStructureDetails details)
    {
        var normalized = details.Normalized();
        var violations = new Violations();
        CheckCode(violations, normalized.Code);
        violations.Require("name", "Structure name", normalized.Name);
        if (normalized.ComponentIds is not { Count: > 0 })
        {
            violations.Add("componentIds", "Choose at least one component.");
        }
        else if (normalized.ComponentIds.Count > MaxComponents)
        {
            violations.Add("componentIds", $"Choose at most {MaxComponents} components.");
        }
        else if (normalized.ComponentIds.Distinct().Count() != normalized.ComponentIds.Count)
        {
            violations.Add("componentIds", "Choose each component once.");
        }

        return violations.ToResult();
    }

    /// <summary>Applies the details, or changes nothing when any rule is violated. Status is left untouched.</summary>
    public Result Update(SalaryStructureDetails details)
    {
        var valid = Validate(details);
        if (valid.IsFailure)
        {
            return valid;
        }

        var normalized = details.Normalized();
        AssignCode(normalized.Code);
        Name = normalized.Name;
        Description = normalized.Description;
        SetComponents(normalized.ComponentIds);
        return Result.Success();
    }

    private void SetComponents(IReadOnlyList<int> ids)
    {
        var first = components.Any(component => component.SortOrder <= Half) ? (short)(Half + 1) : (short)1;
        components.RemoveAll(component => !ids.Contains(component.SalaryComponentId));
        for (var index = 0; index < ids.Count; index++)
        {
            var sortOrder = (short)(first + index);
            var existing = components.Find(component => component.SalaryComponentId == ids[index]);
            if (existing is null)
            {
                components.Add(new SalaryStructureComponent(ids[index], sortOrder));
            }
            else
            {
                existing.MoveTo(sortOrder);
            }
        }
    }
}
