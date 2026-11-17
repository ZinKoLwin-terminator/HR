using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Performance;

/// <summary>The editable fields of a KPI category. Status changes through Activate and Deactivate.</summary>
public sealed record KpiCategoryDetails(string Code, string Name, string? Description)
{
    /// <summary>Trims text and turns blank optional text into null. Idempotent.</summary>
    public KpiCategoryDetails Normalized() => this with
    {
        Code = TextNormalization.Clean(Code),
        Name = TextNormalization.Clean(Name),
        Description = TextNormalization.CleanOptional(Description),
    };
}

/// <summary>A grouping of KPI templates (kpi.Category), for example Productivity or Quality.</summary>
public sealed class KpiCategory : CodedRecord
{
    private KpiCategory()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public static Result<KpiCategory> Create(KpiCategoryDetails details, RecordStatus status)
    {
        var category = new KpiCategory();
        var result = category.Update(details);
        if (result.IsFailure)
        {
            return Result.Failure<KpiCategory>(result.Error);
        }

        category.SetInitialStatus(status);
        return Result.Success(category);
    }

    /// <summary>Checks the rules that need no other record, without changing anything.</summary>
    public static Result Validate(KpiCategoryDetails details)
    {
        var normalized = details.Normalized();
        var violations = new Violations();
        CheckCode(violations, normalized.Code);
        return violations
            .Require("name", "Category name", normalized.Name)
            .ToResult();
    }

    /// <summary>Applies the details, or changes nothing when any rule is violated. Status is left untouched.</summary>
    public Result Update(KpiCategoryDetails details)
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
        return Result.Success();
    }
}
