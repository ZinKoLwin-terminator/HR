using Apex.Hrms.Domain.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Apex.Hrms.Application.UseCases;

/// <summary>Which operations a resource exposes. Workflow resources (e.g. payroll runs) leave out Update and Delete.</summary>
[Flags]
public enum CrudOperations
{
    None = 0,
    Read = 1,
    Create = 2,
    Update = 4,
    Delete = 8,
    All = Read | Create | Update | Delete,
}

/// <summary>
/// Registers the handlers of one resource piece by piece, so a resource only gets the operations it needs. A module
/// calls <see cref="AddResource{TEntity, TDetails, TDto, TFilter, TMapping, TSpecification}"/> first and then the
/// operations it exposes; <see cref="AddCrudResource{TEntity, TDetails, TCreate, TUpdate, TDto, TFilter, TMapping, TRules, TSpecification}"/>
/// is the shorthand for the full set.
/// </summary>
internal static class ResourceRegistration
{
    /// <summary>The shared collaborators and the three read handlers (get all, get by id, query).</summary>
    public static IServiceCollection AddResource<TEntity, TDetails, TDto, TFilter, TMapping, TSpecification>(
        this IServiceCollection services)
        where TEntity : AuditedEntity
        where TDto : IIdentified
        where TMapping : class, IEntityDescriptor<TEntity>, IDtoMapper<TEntity, TDto>, IEntityFactory<TEntity, TDetails>
        where TSpecification : class, IQuerySpecification<TEntity, TFilter>
    {
        services.AddScoped<TMapping>();
        services.AddScoped<IEntityDescriptor<TEntity>>(provider => provider.GetRequiredService<TMapping>());
        services.AddScoped<IDtoMapper<TEntity, TDto>>(provider => provider.GetRequiredService<TMapping>());
        services.AddScoped<IEntityFactory<TEntity, TDetails>>(provider => provider.GetRequiredService<TMapping>());
        services.AddScoped<IQuerySpecification<TEntity, TFilter>, TSpecification>();
        services.TryAddScoped<EntityLoader<TEntity>>();

        services.AddScoped<IGetAllHandler<TDto>, GetAllHandler<TEntity, TDto>>();
        services.AddScoped<IGetByIdHandler<TDto>, GetByIdHandler<TEntity, TDto>>();
        services.AddScoped<IQueryHandler<TDto, TFilter>, QueryHandler<TEntity, TDto, TFilter>>();
        return services;
    }

    public static IServiceCollection AddCreate<TEntity, TDetails, TRequest, TDto, TRules>(this IServiceCollection services)
        where TEntity : AuditedEntity
        where TRequest : ICreateRequest<TDetails>
        where TRules : class, ISaveRules<TEntity, TDetails>
    {
        services.TryAddScoped<ISaveRules<TEntity, TDetails>, TRules>();
        services.AddScoped<ICreateHandler<TRequest, TDto>, CreateHandler<TEntity, TRequest, TDetails, TDto>>();
        return services;
    }

    public static IServiceCollection AddUpdate<TEntity, TDetails, TRequest, TDto, TRules>(this IServiceCollection services)
        where TEntity : AuditedEntity
        where TRequest : IDetailsRequest<TDetails>, IVersionedRequest
        where TRules : class, ISaveRules<TEntity, TDetails>
    {
        services.TryAddScoped<ISaveRules<TEntity, TDetails>, TRules>();
        services.AddScoped<IUpdateHandler<TRequest, TDto>, UpdateHandler<TEntity, TRequest, TDetails, TDto>>();
        return services;
    }

    public static IServiceCollection AddDelete<TEntity, TDto>(this IServiceCollection services)
        where TEntity : AuditedEntity =>
        services.AddScoped<IDeleteHandler<TDto>, DeleteHandler<TEntity, TDto>>();

    /// <summary>Registers one workflow command: the handler plus the domain action it runs.</summary>
    public static IServiceCollection AddEntityCommand<TEntity, TCommand, TDto, TAction>(this IServiceCollection services)
        where TEntity : AuditedEntity
        where TCommand : IEntityCommand
        where TAction : class, ICommandAction<TEntity, TCommand>
    {
        services.AddScoped<ICommandAction<TEntity, TCommand>, TAction>();
        services.AddScoped<ICommandHandler<TCommand, TDto>, CommandHandler<TEntity, TCommand, TDto>>();
        return services;
    }

    /// <summary>The activate and deactivate commands every coded record has.</summary>
    public static IServiceCollection AddStatusCommands<TEntity, TDto>(this IServiceCollection services)
        where TEntity : CodedRecord => services
        .AddEntityCommand<TEntity, ActivateCommand, TDto, ActivateAction<TEntity>>()
        .AddEntityCommand<TEntity, DeactivateCommand, TDto, DeactivateAction<TEntity>>();

    /// <summary>Full CRUD plus the status commands: read, create, update, delete, activate and deactivate.</summary>
    public static IServiceCollection AddCrudResource<TEntity, TDetails, TCreate, TUpdate, TDto, TFilter, TMapping, TRules, TSpecification>(
        this IServiceCollection services)
        where TEntity : CodedRecord
        where TCreate : ICreateRequest<TDetails>
        where TUpdate : IDetailsRequest<TDetails>, IVersionedRequest
        where TDto : IIdentified
        where TMapping : class, IEntityDescriptor<TEntity>, IDtoMapper<TEntity, TDto>, IEntityFactory<TEntity, TDetails>
        where TRules : class, ISaveRules<TEntity, TDetails>
        where TSpecification : class, IQuerySpecification<TEntity, TFilter> => services
        .AddResource<TEntity, TDetails, TDto, TFilter, TMapping, TSpecification>()
        .AddCreate<TEntity, TDetails, TCreate, TDto, TRules>()
        .AddUpdate<TEntity, TDetails, TUpdate, TDto, TRules>()
        .AddDelete<TEntity, TDto>()
        .AddStatusCommands<TEntity, TDto>();
}
