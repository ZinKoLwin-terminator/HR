using System.Linq.Expressions;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Organization;

/// <summary>Checks shared by the organization rule classes: uniqueness and parent lookups.</summary>
internal static class HierarchyChecks
{
    public static async Task<Result> EnsureUniqueAsync<TEntity>(
        IRepository<TEntity> repository,
        Expression<Func<TEntity, bool>> duplicate,
        string label,
        string field,
        string value,
        CancellationToken cancellationToken)
        where TEntity : Entity =>
        await repository.AnyAsync(duplicate, cancellationToken)
            ? Result.Failure(AppError.Validation($"{label} \"{value}\" already exists.", field, $"{label} must be unique."))
            : Result.Success();

    /// <summary>Finds the referenced parent, failing with a field error when it does not exist.</summary>
    public static async Task<Result<TParent>> FindParentAsync<TParent>(
        IRepository<TParent> repository,
        int id,
        string label,
        string field,
        CancellationToken cancellationToken)
        where TParent : Entity
    {
        var parent = await repository.FindAsync(id, cancellationToken);
        return parent is null
            ? Result.Failure<TParent>(AppError.Validation(
                $"{label} #{id} was not found.",
                field,
                $"Choose an existing {label.ToLowerInvariant()}."))
            : Result.Success(parent);
    }

    /// <summary>Inactive records cannot receive new children.</summary>
    public static Result EnsureActive(CodedRecord parent, string label, string name, string childPlural, string field) =>
        parent.IsActive
            ? Result.Success()
            : Result.Failure(AppError.Validation(
                $"{label} \"{name}\" is inactive and cannot receive new {childPlural}.",
                field,
                $"Choose an active {label.ToLowerInvariant()}."));
}
