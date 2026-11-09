namespace Apex.Hrms.Application;

/// <summary>Mirrors PagedResult of T in the frontend.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int PageIndex, int PageSize)
{
    public PagedResult<TOut> Map<TOut>(Func<T, TOut> selector) =>
        new([.. Items.Select(selector)], Total, PageIndex, PageSize);
}
