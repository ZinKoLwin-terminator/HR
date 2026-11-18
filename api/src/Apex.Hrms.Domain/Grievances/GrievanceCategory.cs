using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Grievances;

/// <summary>The editable fields of a grievance category. Status changes through Activate and Deactivate.</summary>
public sealed record GrievanceCategoryDetails(
    string Code,
    string Name,
    string? Description,
    bool AllowsAnonymous,
    short DefaultTargetDays,
    short SortOrder)
{
    /// <summary>Trims text and turns blank optional text into null. Idempotent.</summary>
    public GrievanceCategoryDetails Normalized() => this with
    {
        Code = TextNormalization.Clean(Code),
        Name = TextNormalization.Clean(Name),
        Description = TextNormalization.CleanOptional(Description),
    };
}

/// <summary>
/// A kind of grievance (grv.Category), for example Workplace Harassment. <see cref="AllowsAnonymous"/> decides whether a case of
/// this kind may be raised anonymously; <see cref="DefaultTargetDays"/> proposes the target resolution date at acknowledgement.
/// </summary>
public sealed class GrievanceCategory : CodedRecord
{
    public const int CodeMaxLength = 20;
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 300;
    public const short MinTargetDays = 1;
    public const short MaxTargetDays = 365;

    private GrievanceCategory()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public bool AllowsAnonymous { get; private set; }

    public short DefaultTargetDays { get; private set; }

    public short SortOrder { get; private set; }

    public static Result<GrievanceCategory> Create(GrievanceCategoryDetails details, RecordStatus status)
    {
        var category = new GrievanceCategory();
        var result = category.Update(details);
        if (result.IsFailure)
        {
            return Result.Failure<GrievanceCategory>(result.Error);
        }

        category.SetInitialStatus(status);
        return Result.Success(category);
    }

    /// <summary>Checks the rules that need no other record (as the CHECK constraints of grv.Category), without changing anything.</summary>
    public static Result Validate(GrievanceCategoryDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        var normalized = details.Normalized();
        var violations = new Violations();
        CheckCode(violations, normalized.Code);
        return violations
            .AddIf(normalized.Code.Length > CodeMaxLength, "code", $"Code must be at most {CodeMaxLength} characters.")
            .Require("name", "Category name", normalized.Name)
            .AddIf(normalized.Name.Length > NameMaxLength, "name", $"Category name must be at most {NameMaxLength} characters.")
            .AddIf(normalized.Description is { Length: > DescriptionMaxLength }, "description", $"Description must be at most {DescriptionMaxLength} characters.")
            .AddIf(
                normalized.DefaultTargetDays is < MinTargetDays or > MaxTargetDays,
                "defaultTargetDays",
                $"The target must be between {MinTargetDays} and {MaxTargetDays} days.")
            .AddIf(normalized.SortOrder < 0, "sortOrder", "The sort order cannot be negative.")
            .ToResult();
    }

    /// <summary>Applies the details, or changes nothing when any rule is violated. Status is left untouched.</summary>
    public Result Update(GrievanceCategoryDetails details)
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
        AllowsAnonymous = normalized.AllowsAnonymous;
        DefaultTargetDays = normalized.DefaultTargetDays;
        SortOrder = normalized.SortOrder;
        return Result.Success();
    }
}
