using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Activity;
using Apex.Hrms.Application.References;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.UseCases;

/// <summary>Deletes a record unless other records still reference it (there is no soft delete).</summary>
/// <typeparam name="TEntity">The entity to delete.</typeparam>
/// <typeparam name="TDto">Only the key the API layer resolves this handler by.</typeparam>
internal sealed class DeleteHandler<TEntity, TDto>(
    EntityLoader<TEntity> loader,
    IEntityDescriptor<TEntity> descriptor,
    IEnumerable<IReferenceCheck<TEntity>> references,
    IRepository<TEntity> repository,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork) : IDeleteHandler<TDto>
    where TEntity : AuditedEntity
{
    public async Task<Result> HandleAsync(int id, string? rowVersion, CancellationToken cancellationToken)
    {
        var loaded = await loader.LoadForChangeAsync(id, rowVersion, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure(loaded.Error);
        }

        var usage = await ReferenceUsage.SummarizeAsync(references, ReferenceUse.Delete, id, cancellationToken);
        if (usage is not null)
        {
            return Result.Failure(AppError.Conflict(
                $"\"{descriptor.Describe(loaded.Value)}\" cannot be deleted because it is used by {usage}. Deactivate it instead."));
        }

        activity.Record(
            descriptor.ActivityCategory,
            ActivityMessages.Deleted(descriptor.Label, descriptor.ActivitySubject(loaded.Value)));
        repository.Remove(loaded.Value);
        return await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
