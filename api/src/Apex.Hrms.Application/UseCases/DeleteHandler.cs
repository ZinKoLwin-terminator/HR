using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.UseCases;

/// <summary>Deletes a record unless other records still reference it (there is no soft delete).</summary>
/// <typeparam name="TDto">Only the key the API layer resolves this handler by.</typeparam>
internal sealed class DeleteHandler<TEntity, TDto>(
    IRepository<TEntity> repository,
    IEntityDescriptor<TEntity> descriptor,
    IDeletionPolicy<TEntity> policy,
    IReferenceUsageReader usageReader,
    IUnitOfWork unitOfWork) : IDeleteHandler<TDto>
    where TEntity : Entity
{
    public async Task<Result> HandleAsync(int id, CancellationToken cancellationToken)
    {
        var entity = await repository.FindAsync(id, cancellationToken);
        if (entity is null)
        {
            return Result.Failure(NotFoundError.For(descriptor.Label, id));
        }

        var usages = await CountUsagesAsync(id, cancellationToken);
        if (usages.Count > 0)
        {
            var summary = string.Join(", ", usages.Select(usage => $"{usage.Count} {usage.Label}"));
            return Result.Failure(AppError.Conflict(
                $"\"{descriptor.Describe(entity)}\" cannot be deleted because it is used by {summary}. Deactivate it instead."));
        }

        repository.Remove(entity);
        return await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<(string Label, int Count)>> CountUsagesAsync(int id, CancellationToken cancellationToken)
    {
        var usages = new List<(string Label, int Count)>();
        foreach (var reference in policy.References)
        {
            var count = await usageReader.CountAsync(reference.Query, id, cancellationToken);
            if (count > 0)
            {
                usages.Add((reference.Label, count));
            }
        }

        return usages;
    }
}
