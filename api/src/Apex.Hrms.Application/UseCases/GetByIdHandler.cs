using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.UseCases;

internal sealed class GetByIdHandler<TEntity, TDto>(
    IRepository<TEntity> repository,
    IEntityDescriptor<TEntity> descriptor,
    IDtoMapper<TEntity, TDto> mapper) : IGetByIdHandler<TDto>
    where TEntity : Entity
{
    public async Task<Result<TDto>> HandleAsync(int id, CancellationToken cancellationToken)
    {
        var entity = await repository.FindAsync(id, cancellationToken);
        return entity is null
            ? Result.Failure<TDto>(NotFoundError.For(descriptor.Label, id))
            : Result.Success(mapper.ToDto(entity));
    }
}
