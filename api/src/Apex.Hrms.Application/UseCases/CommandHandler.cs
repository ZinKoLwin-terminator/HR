using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Activity;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.UseCases;

/// <summary>
/// Command flow: load inside the access scope and against the row version, run the domain method, save, and return
/// the record with its new row version.
/// </summary>
internal sealed class CommandHandler<TEntity, TCommand, TDto>(
    EntityLoader<TEntity> loader,
    ICommandAction<TEntity, TCommand> action,
    IEntityDescriptor<TEntity> descriptor,
    IEnumerable<ICommandActivity<TEntity, TCommand>> wording,
    IActivityRecorder activity,
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

        var statusBefore = (loaded.Value as CodedRecord)?.Status;
        var executed = action.Execute(loaded.Value, command);
        if (executed.IsFailure)
        {
            return Result.Failure<TDto>(executed.Error);
        }

        // Activating an active record (or deactivating an inactive one) changes nothing, so it leaves no entry.
        var noChange = command is ActivateCommand or DeactivateCommand
            && loaded.Value is CodedRecord coded
            && coded.Status == statusBefore;
        if (!noChange)
        {
            var text = wording.FirstOrDefault()?.Describe(loaded.Value, command)
                ?? ActivityMessages.Updated(descriptor.Label, descriptor.ActivitySubject(loaded.Value));
            activity.Record(descriptor.ActivityCategory, text);
        }

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure
            ? Result.Failure<TDto>(saved.Error)
            : Result.Success(mapper.ToDto(loaded.Value));
    }
}
