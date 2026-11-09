using FluentValidation;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.UseCases;

internal sealed class UpdateHandler<TEntity, TRequest, TDto>(
    IValidator<TRequest> validator,
    ISaveRules<TEntity, TRequest> rules,
    IEntityFactory<TEntity, TRequest> factory,
    IEntityDescriptor<TEntity> descriptor,
    IRepository<TEntity> repository,
    IUnitOfWork unitOfWork,
    IDtoMapper<TEntity, TDto> mapper) : IUpdateHandler<TRequest, TDto>
    where TEntity : Entity
    where TRequest : IVersionedRequest
{
    public async Task<Result<TDto>> HandleAsync(int id, TRequest request, CancellationToken cancellationToken)
    {
        var valid = await validator.CheckAsync(request, cancellationToken);
        if (valid.IsFailure)
        {
            return Result.Failure<TDto>(valid.Error);
        }

        var entity = await repository.FindAsync(id, cancellationToken);
        if (entity is null)
        {
            return Result.Failure<TDto>(NotFoundError.For(descriptor.Label, id));
        }

        var allowed = await rules.CheckUpdateAsync(entity, request, cancellationToken);
        if (allowed.IsFailure)
        {
            return Result.Failure<TDto>(allowed.Error);
        }

        if (request.RowVersion is { Length: > 0 } rowVersion)
        {
            repository.ExpectRowVersion(entity, rowVersion);
        }

        var updated = factory.Update(entity, request);
        if (updated.IsFailure)
        {
            return Result.Failure<TDto>(updated.Error);
        }

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure
            ? Result.Failure<TDto>(saved.Error)
            : Result.Success(mapper.ToDto(entity));
    }
}
