using System.Linq.Expressions;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.UseCases;

/// <summary>A response that exposes the record id (used for the Location header after create).</summary>
public interface IIdentified
{
    int Id { get; }
}

/// <summary>A write request that may carry the row version the client last saw.</summary>
public interface IVersionedRequest
{
    byte[]? RowVersion { get; }
}

/// <summary>Names a resource in messages, e.g. "Company #4 was not found."</summary>
public interface IEntityDescriptor<in TEntity>
{
    string Label { get; }

    /// <summary>How the record is called in messages, e.g. its name.</summary>
    string Describe(TEntity entity);
}

public interface IDtoMapper<in TEntity, out TDto>
{
    TDto ToDto(TEntity entity);
}

/// <summary>Builds and changes domain entities from write requests.</summary>
public interface IEntityFactory<TEntity, in TRequest>
{
    Result<TEntity> Create(TRequest request);

    Result Update(TEntity entity, TRequest request);
}

/// <summary>Business rules that need other records (uniqueness, hierarchy, active parents).</summary>
public interface ISaveRules<in TEntity, in TRequest>
{
    Task<Result> CheckCreateAsync(TRequest request, CancellationToken cancellationToken);

    Task<Result> CheckUpdateAsync(TEntity existing, TRequest request, CancellationToken cancellationToken);
}

public sealed record ReferenceCheck(string Label, ReferenceQuery Query);

/// <summary>Which records block deleting an entity, with the wording used in the conflict message.</summary>
public interface IDeletionPolicy<TEntity>
{
    IReadOnlyList<ReferenceCheck> References { get; }
}

public interface IQuerySpecification<TEntity, in TFilter>
{
    /// <summary>Rows whose searchable fields contain the (non-blank) term.</summary>
    Expression<Func<TEntity, bool>> Search(string term);

    Expression<Func<TEntity, bool>> Filter(TFilter filter);

    /// <summary>Sortable fields by their API name (case-insensitive).</summary>
    IReadOnlyDictionary<string, Expression<Func<TEntity, object>>> SortKeys { get; }
}
