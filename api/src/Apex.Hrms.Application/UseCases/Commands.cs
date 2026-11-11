using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.UseCases;

/// <summary>A command aimed at one record; the row version is required like for update and delete.</summary>
public interface IEntityCommand : IVersionedRequest
{
    int Id { get; }
}

/// <summary>Lets the API layer build any command from the route id and the request body.</summary>
public interface IEntityCommand<TSelf> : IEntityCommand
    where TSelf : IEntityCommand<TSelf>
{
    static abstract TSelf From(int id, string? rowVersion);
}

/// <summary>The domain operation of one command: it changes the entity through a domain method that returns a Result.</summary>
public interface ICommandAction<in TEntity, in TCommand>
{
    Result Execute(TEntity entity, TCommand command);
}

/// <summary>
/// Optional wording of a command in the activity feed. A command without one is logged as the generic
/// <c>Label "subject" updated</c> (a status change is an update in the mock services).
/// </summary>
public interface ICommandActivity<in TEntity, in TCommand>
{
    string Describe(TEntity entity, TCommand command);
}

public sealed record ActivateCommand(int Id, string? RowVersion) : IEntityCommand<ActivateCommand>
{
    public static ActivateCommand From(int id, string? rowVersion) => new(id, rowVersion);
}

public sealed record DeactivateCommand(int Id, string? RowVersion) : IEntityCommand<DeactivateCommand>
{
    public static DeactivateCommand From(int id, string? rowVersion) => new(id, rowVersion);
}

internal sealed class ActivateAction<TEntity> : ICommandAction<TEntity, ActivateCommand>
    where TEntity : CodedRecord
{
    public Result Execute(TEntity entity, ActivateCommand command) => entity.Activate();
}

internal sealed class DeactivateAction<TEntity> : ICommandAction<TEntity, DeactivateCommand>
    where TEntity : CodedRecord
{
    public Result Execute(TEntity entity, DeactivateCommand command) => entity.Deactivate();
}
