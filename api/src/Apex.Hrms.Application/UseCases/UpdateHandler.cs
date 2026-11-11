using FluentValidation;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Activity;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.UseCases;

/// <summary>
/// Update flow: request shape (validator, including the required row version), load inside the access scope and
/// against the row version, normalized details, domain rules, cross-record rules, then save. Status never changes here.
/// </summary>
internal sealed class UpdateHandler<TEntity, TRequest, TDetails, TDto>(
    IValidator<TRequest> validator,
    IEntityFactory<TEntity, TDetails> factory,
    ISaveRules<TEntity, TDetails> rules,
    EntityLoader<TEntity> loader,
    IEntityDescriptor<TEntity> descriptor,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork,
    IDtoMapper<TEntity, TDto> mapper) : IUpdateHandler<TRequest, TDto>
    where TEntity : AuditedEntity
    where TRequest : IDetailsRequest<TDetails>, IVersionedRequest
{
    public async Task<Result<TDto>> HandleAsync(int id, TRequest request, CancellationToken cancellationToken)
    {
        var valid = await validator.CheckAsync(request, cancellationToken);
        if (valid.IsFailure)
        {
            return Result.Failure<TDto>(valid.Error);
        }

        var loaded = await loader.LoadForChangeAsync(id, request.RowVersion, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<TDto>(loaded.Error);
        }

        var entity = loaded.Value;
        var details = request.ToDetails();
        var consistent = factory.Validate(details, entity);
        if (consistent.IsFailure)
        {
            return Result.Failure<TDto>(consistent.Error);
        }

        var allowed = await rules.CheckUpdateAsync(entity, details, cancellationToken);
        if (allowed.IsFailure)
        {
            return Result.Failure<TDto>(allowed.Error);
        }

        var updated = factory.Update(entity, details);
        if (updated.IsFailure)
        {
            return Result.Failure<TDto>(updated.Error);
        }

        activity.Record(
            descriptor.ActivityCategory,
            ActivityMessages.Updated(descriptor.Label, descriptor.ActivitySubject(entity)));

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure
            ? Result.Failure<TDto>(saved.Error)
            : Result.Success(mapper.ToDto(entity));
    }
}
