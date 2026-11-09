namespace Apex.Hrms.Application;

/// <summary>Mirrors SortState in the frontend; Direction is "asc", "desc" or empty.</summary>
public sealed record SortState(string Active, string Direction);

/// <summary>Mirrors PageRequest of TFilter in the frontend.</summary>
public sealed record PageRequest<TFilter>(
    int PageIndex,
    int PageSize,
    SortState? Sort,
    string? Search,
    TFilter Filters);
