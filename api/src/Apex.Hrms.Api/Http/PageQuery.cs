using Apex.Hrms.Application;

namespace Apex.Hrms.Api.Http;

/// <summary>
/// The paging part of a list query: <c>?pageIndex=0&amp;pageSize=10&amp;sort=name:desc&amp;search=term</c>.
/// <c>sort</c> is <c>field</c> or <c>field:asc|desc</c> (ascending by default).
/// </summary>
internal sealed record PageQuery(int PageIndex = 0, int PageSize = 10, string? Sort = null, string? Search = null)
{
    public PageRequest<TFilter> ToRequest<TFilter>(TFilter filter) => new(PageIndex, PageSize, ParseSort(), Search, filter);

    private SortState? ParseSort()
    {
        if (string.IsNullOrWhiteSpace(Sort))
        {
            return null;
        }

        var parts = Sort.Split(':', 2, StringSplitOptions.TrimEntries);
        var direction = parts.Length == 2 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc";
        return new SortState(parts[0], direction);
    }
}
