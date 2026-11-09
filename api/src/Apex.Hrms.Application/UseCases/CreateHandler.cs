using FluentValidation;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.UseCases;

/// <summary>
/// Create flow: request shape (validator), normalized details, domain rules, cross-record rules, then save.
/// </summary>
internal sealed class CreateHandler<TEntity, TRequest, TDetails, TDto>(
    IValidator<TRequest> validator,
    IEntityFactory<TEntity, TDetails> factory,
    ISaveRules<TEntity, TDetails> rules,
    IRepository<TEntity> repository,
    IUnitOfWork unitOfWork,
    IDtoMapper<TEntity, TDto> mapper) : ICreateHandler<TRequest, TDto>
    where TEntity : Entity
    where TRequest : ICreateRequest<TDetails>
{
    public async Task<Result<TDto>> HandleAsync(TRequest request, CancellationToken cancellationToken)
    {
        var valid = await validator.CheckAsync(request, cancellationToken);
        if (valid.IsFailure)
        {
            return Result.Failure<TDto>(valid.Error);
        }

        var details = request.ToDetails();
        var consistent = factory.Validate(details, existing: null);
        if (consistent.IsFailure)
        {
            return Result.Failure<TDto>(consistent.Error);
        }

        var allowed = await rules.CheckCreateAsync(details, cancellationToken);
        if (allowed.IsFailure)
        {
            return Result.Failure<TDto>(allowed.Error);
        }

        var created = factory.Create(details, request.ParseStatus());
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
