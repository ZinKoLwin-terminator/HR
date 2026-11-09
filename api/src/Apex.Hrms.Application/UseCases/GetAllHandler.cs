using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.UseCases;

internal sealed class GetAllHandler<TEntity, TDto>(
    IRepository<TEntity> repository,
    IDtoMapper<TEntity, TDto> mapper) : IGetAllHandler<TDto>
    where TEntity : Entity
{
    public async Task<Result<IReadOnlyList<TDto>>> HandleAsync(CancellationToken cancellationToken)
    {
        var entities = await repository.ListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<TDto>>([.. entities.Select(mapper.ToDto)]);
    }
}
