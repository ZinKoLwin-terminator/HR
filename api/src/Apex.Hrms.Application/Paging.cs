namespace Apex.Hrms.Application;

public static class Paging
{
    public const int MaxPageSize = 200;

    /// <summary>Longest accepted search term; longer ones are rejected instead of becoming expensive LIKE scans.</summary>
    public const int MaxSearchLength = 100;

    public static int ClampPageSize(int pageSize) => Math.Clamp(pageSize, 1, MaxPageSize);

    /// <summary>Keeps the page index inside the available pages, as the frontend's mock paginator does.</summary>
    public static int ClampPageIndex(int pageIndex, int total, int pageSize)
    {
        var lastPage = Math.Max(0, (int)Math.Ceiling(total / (double)pageSize) - 1);
        return Math.Min(Math.Max(0, pageIndex), lastPage);
    }
}
