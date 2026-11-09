using FluentValidation;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.UseCases;

internal sealed class CreateHandler<TEntity, TRequest, TDto>(
    IValidator<TRequest> validator,
    ISaveRules<TEntity, TRequest> rules,
    IEntityFactory<TEntity, TRequest> factory,
    IRepository<TEntity> repository,
    IUnitOfWork unitOfWork,
    IDtoMapper<TEntity, TDto> mapper) : ICreateHandler<TRequest, TDto>
    where TEntity : Entity
{
    public async Task<Result<TDto>> HandleAsync(TRequest request, CancellationToken cancellationToken)
    {
        var valid = await validator.CheckAsync(request, cancellationToken);
        if (valid.IsFailure)
        {
            return Result.Failure<TDto>(valid.Error);
        }

        var allowed = await rules.CheckCreateAsync(request, cancellationToken);
        if (allowed.IsFailure)
        {
            return Result.Failure<TDto>(allowed.Error);
        }

        var created = factory.Create(request);
        if (created.IsFailure)
        {
            return Result.Failure<TDto>(created.Error);
        }

        repository.Add(created.Value);
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure
            ? Result.Failure<TDto>(saved.Error)
            : Result.Success(mapper.ToDto(created.Value));
    }
}
