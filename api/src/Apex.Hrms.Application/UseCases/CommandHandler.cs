using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.UseCases;

/// <summary>
/// Command flow: load inside the access scope and against the row version, run the domain method, save, and return
/// the record with its new row version.
/// </summary>
internal sealed class CommandHandler<TEntity, TCommand, TDto>(
    EntityLoader<TEntity> loader,
    ICommandAction<TEntity, TCommand> action,
    IUnitOfWork unitOfWork,
    IDtoMapper<TEntity, TDto> mapper) : ICommandHandler<TCommand, TDto>
    where TEntity : AuditedEntity
    where TCommand : IEntityCommand
{
    public async Task<Result<TDto>> HandleAsync(TCommand command, CancellationToken cancellationToken)
    {
        var loaded = await loader.LoadForChangeAsync(command.Id, command.RowVersion, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<TDto>(loaded.Error);
        }

        var executed = action.Execute(loaded.Value, command);
        if (executed.IsFailure)
        {
            return Result.Failure<TDto>(executed.Error);
        }

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure
            ? Result.Failure<TDto>(saved.Error)
            : Result.Success(mapper.ToDto(loaded.Value));
    }
}
