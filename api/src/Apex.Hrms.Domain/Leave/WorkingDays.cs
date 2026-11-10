namespace Apex.Hrms.Domain.Leave;

/// <summary>Counts the days of a leave request that are actually worked.</summary>
public static class WorkingDays
{
    /// <summary>
    /// Days in the inclusive range that are neither a weekend day nor a holiday. A range that ends before it starts
    /// has none.
    /// </summary>
    public static int Count(
        DateOnly from,
        DateOnly to,
        IReadOnlySet<DayOfWeek> weekendDays,
        IReadOnlySet<DateOnly> holidays)
    {
        var count = 0;
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            if (!weekendDays.Contains(day.DayOfWeek) && !holidays.Contains(day))
            {
                count++;
            }
        }

        return count;
    }
}
