using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.UseCases;

internal sealed class QueryHandler<TEntity, TDto, TFilter>(
    IRepository<TEntity> repository,
    IAccessScope<TEntity> scope,
    ICurrentUser user,
    IQuerySpecification<TEntity, TFilter> specification,
    IDtoMapper<TEntity, TDto> mapper) : IQueryHandler<TDto, TFilter>
    where TEntity : Entity
{
    public async Task<Result<PagedResult<TDto>>> HandleAsync(
        PageRequest<TFilter> request,
        CancellationToken cancellationToken)
    {
        var sort = ResolveSort(request.Sort);
        if (sort.IsFailure)
        {
            return Result.Failure<PagedResult<TDto>>(sort.Error);
        }

        if (request.Search is { Length: > Paging.MaxSearchLength })
        {
            return Result.Failure<PagedResult<TDto>>(AppError.Validation(
                $"Search must be at most {Paging.MaxSearchLength} characters.",
                "search",
                $"Search must be at most {Paging.MaxSearchLength} characters."));
        }

        var predicate = scope.PredicateFor(user).And(specification.Filter(request.Filters));
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            predicate = predicate.And(specification.Search(request.Search.Trim()));
        }

        var query = new EntityQuery<TEntity>(
            predicate,
            sort.Value,
            Math.Max(0, request.PageIndex),
            Paging.ClampPageSize(request.PageSize));

        var page = await repository.QueryAsync(query, cancellationToken);
        return Result.Success(page.Map(mapper.ToDto));
    }

    private Result<SortCriterion<TEntity>?> ResolveSort(SortState? sort)
    {
        var direction = sort?.Direction;
        if (sort is null || string.IsNullOrWhiteSpace(sort.Active) || string.IsNullOrEmpty(direction))
        {
            return Result.Success<SortCriterion<TEntity>?>(null);
        }

        if (!specification.SortKeys.TryGetValue(sort.Active, out var key))
        {
            return Result.Failure<SortCriterion<TEntity>?>(
                AppError.Validation($"Cannot sort by \"{sort.Active}\".", "sort", "Choose a sortable field."));
        }

        return Result.Success<SortCriterion<TEntity>?>(
            new SortCriterion<TEntity>(key, string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase)));
    }
}
