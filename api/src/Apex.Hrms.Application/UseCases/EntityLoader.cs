using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.UseCases;

/// <summary>
/// Loads the record a use case works on: inside the access scope of the caller (outside it the record does not
/// exist) and, for changes, only while the row version the client sent is still current.
/// </summary>
internal sealed class EntityLoader<TEntity>(
    IRepository<TEntity> repository,
    IAccessScope<TEntity> scope,
    ICurrentUser user,
    IEntityDescriptor<TEntity> descriptor)
    where TEntity : AuditedEntity
{
    public async Task<Result<TEntity>> LoadAsync(int id, CancellationToken cancellationToken)
    {
        var entity = await repository.FindAsync(id, scope.PredicateFor(user), cancellationToken);
        return entity is null
            ? Result.Failure<TEntity>(NotFoundError.For(descriptor.Label, id))
            : Result.Success(entity);
    }

    /// <summary>
    /// Loads the record for an update, delete or command. The row version must be present and valid (400), the record
    /// must exist (404) and match it (409); the repository then checks it again in the database when saving.
    /// </summary>
    public async Task<Result<TEntity>> LoadForChangeAsync(int id, string? rowVersion, CancellationToken cancellationToken)
    {
        var expected = RowVersionToken.Decode(rowVersion);
        if (expected.IsFailure)
        {
            return Result.Failure<TEntity>(expected.Error);
        }

        var loaded = await LoadAsync(id, cancellationToken);
        if (loaded.IsFailure)
        {
            return loaded;
        }

        if (!loaded.Value.RowVersion.AsSpan().SequenceEqual(expected.Value))
        {
            return Result.Failure<TEntity>(RowVersionToken.Stale);
        }

        repository.ExpectRowVersion(loaded.Value, expected.Value);
        return loaded;
    }
}
