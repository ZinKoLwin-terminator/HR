using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.UseCases;

internal sealed class GetByIdHandler<TEntity, TDto>(
    EntityLoader<TEntity> loader,
    IDtoMapper<TEntity, TDto> mapper) : IGetByIdHandler<TDto>
    where TEntity : AuditedEntity
{
    public async Task<Result<TDto>> HandleAsync(int id, CancellationToken cancellationToken)
    {
        var loaded = await loader.LoadAsync(id, cancellationToken);
        return loaded.IsFailure
            ? Result.Failure<TDto>(loaded.Error)
            : Result.Success(mapper.ToDto(loaded.Value));
    }
}
