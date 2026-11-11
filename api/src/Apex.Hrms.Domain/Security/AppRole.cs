using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Security;

/// <summary>The values of a role a user can edit.</summary>
public sealed record RoleDetails(string Name, string? Description);

/// <summary>A role (sec.Role). Built-in roles (<see cref="IsSystem"/>) cannot be renamed, deactivated or deleted.</summary>
public sealed class AppRole : AuditedEntity
{
    public const int NameMaxLength = 60;
    public const int DescriptionMaxLength = 300;

    public const string BuiltInCannotBeRenamed = "A built-in role cannot be renamed.";
    public const string BuiltInCannotBeDeactivated = "A built-in role cannot be deactivated.";
    public const string BuiltInCannotBeDeleted = "A built-in role cannot be deleted.";

    private AppRole()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public bool IsSystem { get; private set; }

    public RecordStatus Status { get; private set; }

    public bool IsActive => Status == RecordStatus.Active;

    /// <summary>The rules that need no other record. <paramref name="existing"/> is the role being updated, or null on create.</summary>
    public static Result Validate(RoleDetails details, AppRole? existing)
    {
        var violations = new Violations()
            .Require("name", "Role name", details.Name)
            .AddIf(details.Name?.Trim().Length > NameMaxLength, "name", $"Role name must be at most {NameMaxLength} characters.")
            .AddIf(details.Description?.Trim().Length > DescriptionMaxLength, "description", $"Description must be at most {DescriptionMaxLength} characters.");
        if (existing is { IsSystem: true } && !string.IsNullOrWhiteSpace(details.Name) && existing.Name != details.Name.Trim())
        {
            violations.Add("name", "Built-in roles keep their name.", BuiltInCannotBeRenamed);
        }

        return violations.ToResult();
    }

    /// <summary>Creates a custom (never built-in) role.</summary>
    public static Result<AppRole> Create(RoleDetails details, RecordStatus status)
    {
        var valid = Validate(details, existing: null);
        if (valid.IsFailure)
        {
            return Result.Failure<AppRole>(valid.Error);
        }

        return Result.Success(new AppRole
        {
            Name = TextNormalization.Clean(details.Name),
            Description = TextNormalization.CleanOptional(details.Description),
            IsSystem = false,
            Status = status,
        });
    }

    public Result Update(RoleDetails details)
    {
        var valid = Validate(details, this);
        if (valid.IsFailure)
        {
            return valid;
        }

        Name = TextNormalization.Clean(details.Name);
        Description = TextNormalization.CleanOptional(details.Description);
        return Result.Success();
    }

    public void Activate() => Status = RecordStatus.Active;

    /// <summary>Built-in roles stay active. Whether users still hold the role is checked by the caller.</summary>
    public Result Deactivate()
    {
        if (IsSystem)
        {
            return Result.Failure(AppError.Conflict(BuiltInCannotBeDeactivated));
        }

        Status = RecordStatus.Inactive;
        return Result.Success();
    }
}
