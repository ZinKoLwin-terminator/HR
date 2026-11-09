using System.Linq.Expressions;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.UseCases;

/// <summary>A response that exposes the record id (used for the Location header after create).</summary>
public interface IIdentified
{
    int Id { get; }
}

/// <summary>
/// A request that carries the row version (base64) the client last saw. It is required: a missing value is a
/// Validation error on <c>rowVersion</c>, a stale one a Conflict.
/// </summary>
public interface IVersionedRequest
{
    string? RowVersion { get; }
}

/// <summary>A write request that can produce the domain details it describes.</summary>
public interface IDetailsRequest<out TDetails>
{
    /// <summary>The normalized (trimmed) details. Call only after the request validator passed.</summary>
    TDetails ToDetails();
}

/// <summary>A create request also states the status the new record starts with.</summary>
public interface ICreateRequest<out TDetails> : IDetailsRequest<TDetails>
{
    /// <summary>Call only after the request validator passed.</summary>
    RecordStatus ParseStatus();
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

/// <summary>Builds and changes domain entities from normalized details.</summary>
public interface IEntityFactory<TEntity, in TDetails>
{
    /// <summary>
    /// Runs the domain rules that need no other record, without changing anything. <paramref name="existing"/> is the
    /// record being updated, or null on create.
    /// </summary>
    Result Validate(TDetails details, TEntity? existing);

    Result<TEntity> Create(TDetails details, RecordStatus status);

    Result Update(TEntity entity, TDetails details);
}

/// <summary>Business rules that need other records (uniqueness, hierarchy, active parents), checked against normalized details.</summary>
public interface ISaveRules<in TEntity, in TDetails>
{
    Task<Result> CheckCreateAsync(TDetails details, CancellationToken cancellationToken);

    Task<Result> CheckUpdateAsync(TEntity existing, TDetails details, CancellationToken cancellationToken);
}

public interface IQuerySpecification<TEntity, in TFilter>
{
    /// <summary>Rows whose searchable fields contain the (non-blank) term.</summary>
    Expression<Func<TEntity, bool>> Search(string term);

    Expression<Func<TEntity, bool>> Filter(TFilter filter);

    /// <summary>Sortable fields by their API name (case-insensitive).</summary>
    IReadOnlyDictionary<string, Expression<Func<TEntity, object>>> SortKeys { get; }
}
