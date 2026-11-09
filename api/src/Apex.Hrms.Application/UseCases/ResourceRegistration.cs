using Apex.Hrms.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.Application.UseCases;

internal static class ResourceRegistration
{
    /// <summary>
    /// Registers the six CRUD handlers of one resource together with the small collaborators they are composed from.
    /// </summary>
    public static IServiceCollection AddCrudResource<TEntity, TRequest, TDto, TFilter, TMapping, TRules, TSpecification, TPolicy>(
        this IServiceCollection services)
        where TEntity : Entity
        where TRequest : IVersionedRequest
        where TDto : IIdentified
        where TMapping : class, IEntityDescriptor<TEntity>, IDtoMapper<TEntity, TDto>, IEntityFactory<TEntity, TRequest>
        where TRules : class, ISaveRules<TEntity, TRequest>
        where TSpecification : class, IQuerySpecification<TEntity, TFilter>
        where TPolicy : class, IDeletionPolicy<TEntity>
    {
        services.AddScoped<TMapping>();
        services.AddScoped<IEntityDescriptor<TEntity>>(provider => provider.GetRequiredService<TMapping>());
        services.AddScoped<IDtoMapper<TEntity, TDto>>(provider => provider.GetRequiredService<TMapping>());
        services.AddScoped<IEntityFactory<TEntity, TRequest>>(provider => provider.GetRequiredService<TMapping>());
        services.AddScoped<ISaveRules<TEntity, TRequest>, TRules>();
        services.AddScoped<IQuerySpecification<TEntity, TFilter>, TSpecification>();
        services.AddScoped<IDeletionPolicy<TEntity>, TPolicy>();

        services.AddScoped<IGetAllHandler<TDto>, GetAllHandler<TEntity, TDto>>();
        services.AddScoped<IGetByIdHandler<TDto>, GetByIdHandler<TEntity, TDto>>();
        services.AddScoped<IQueryHandler<TDto, TFilter>, QueryHandler<TEntity, TDto, TFilter>>();
        services.AddScoped<ICreateHandler<TRequest, TDto>, CreateHandler<TEntity, TRequest, TDto>>();
        services.AddScoped<IUpdateHandler<TRequest, TDto>, UpdateHandler<TEntity, TRequest, TDto>>();
        services.AddScoped<IDeleteHandler<TDto>, DeleteHandler<TEntity, TDto>>();
        return services;
    }
}
