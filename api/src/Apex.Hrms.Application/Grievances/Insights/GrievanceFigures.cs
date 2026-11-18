using System.Globalization;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Grievances;

namespace Apex.Hrms.Application.Grievances.Insights;

/// <summary>
/// One counted case as the insights see it: dates already in the business time zone, the department only of a named case, no subject,
/// description or person. <see cref="ResolvedOn"/> is set only while the case is Resolved or Closed (a reopened case is unresolved).
/// </summary>
internal sealed record GrievanceCaseFact(
    int Id,
    string CaseNo,
    int CategoryId,
    string CategoryName,
    GrievanceStatus Status,
    GrievancePriority? Priority,
    bool IsAnonymous,
    int? DepartmentId,
    DateOnly SubmittedOn,
    DateOnly? TargetResolutionDate,
    DateOnly? ResolvedOn,
    bool IsEscalated,
    int? LeadEmployeeId)
{
    public bool IsOpen => GrievanceCase.IsActive(Status);

    public bool IsFinished => Status is GrievanceStatus.Resolved or GrievanceStatus.Closed;

    public bool IsOverdue(DateOnly today) => IsOpen && TargetResolutionDate is { } target && target < today;

    /// <summary>Calendar days from submission to resolution, or null until resolved.</summary>
    public int? ResolutionDays => IsFinished && ResolvedOn is { } resolved ? resolved.DayNumber - SubmittedOn.DayNumber : null;

    /// <summary>Submission to resolution, or to <paramref name="today"/> while unresolved (never negative).</summary>
    public int AgeDays(DateOnly today) => ResolutionDays ?? Math.Max(0, today.DayNumber - SubmittedOn.DayNumber);
}

/// <summary>The grievance metrics, pure functions over counted cases (see <c>GrievanceInsightModels.cs</c> for the definitions).</summary>
internal static class GrievanceFigures
{
    public const int DepartmentMinGroup = 3;
    public const string SuppressedLabel = "Other / suppressed";
    public const string NotDisclosedLabel = "Not disclosed";
    public const int TrendMonths = 12;
    public const int RecentSteps = 10;

    private static readonly GrievancePriority?[] PriorityOrder =
        [GrievancePriority.Low, GrievancePriority.Medium, GrievancePriority.High, GrievancePriority.Critical, null];

    /// <summary>The 11 non-Draft statuses in workflow order, zeros included.</summary>
    public static IReadOnlyList<GrievanceStatusCountDto> ByStatus(IReadOnlyCollection<GrievanceCaseFact> facts) =>
    [
        .. Enum.GetValues<GrievanceStatus>()
            .Where(status => status != GrievanceStatus.Draft)
            .Select(status => new GrievanceStatusCountDto(GrievanceText.Status.ToText(status), facts.Count(fact => fact.Status == status))),
    ];

    /// <summary>Low, Medium, High, Critical, then null (not yet acknowledged), zeros included.</summary>
    public static IReadOnlyList<GrievancePriorityCountDto> ByPriority(IReadOnlyCollection<GrievanceCaseFact> facts) =>
    [
        .. PriorityOrder.Select(priority => new GrievancePriorityCountDto(
            priority is { } value ? GrievanceText.Priority.ToText(value) : null,
            facts.Count(fact => fact.Priority == priority))),
    ];

    /// <summary>Categories with at least one case, most cases first, then by name.</summary>
    public static IReadOnlyList<GrievanceCategoryCountDto> ByCategory(IReadOnlyCollection<GrievanceCaseFact> facts) =>
    [
        .. facts.GroupBy(fact => (fact.CategoryId, fact.CategoryName))
            .Select(group => new GrievanceCategoryCountDto(group.Key.CategoryId, group.Key.CategoryName, group.Count()))
            .OrderByDescending(count => count.Count)
            .ThenBy(count => count.CategoryName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(count => count.CategoryId),
    ];

    /// <summary>
    /// The department of each named case at submission; an anonymous case (or one with no department) is "Not disclosed"; every department
    /// with fewer than <see cref="DepartmentMinGroup"/> cases is merged into "Other / suppressed". Departments by name, then Not disclosed,
    /// then Other / suppressed (each merged group only when it has cases).
    /// </summary>
    public static IReadOnlyList<GrievanceDepartmentCountDto> ByDepartment(IReadOnlyCollection<GrievanceCaseFact> facts, IReadOnlyDictionary<int, string> departmentNames)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(departmentNames);
        var notDisclosed = facts.Count(fact => fact.IsAnonymous || fact.DepartmentId is null);
        var groups = facts.Where(fact => !fact.IsAnonymous && fact.DepartmentId is not null)
            .GroupBy(fact => fact.DepartmentId!.Value)
            .Select(group => (Id: group.Key, Count: group.Count()))
            .ToList();
        var suppressed = groups.Where(group => group.Count < DepartmentMinGroup).Sum(group => group.Count);
        List<GrievanceDepartmentCountDto> result =
        [
            .. groups.Where(group => group.Count >= DepartmentMinGroup)
                .Select(group => new GrievanceDepartmentCountDto("Department", group.Id, departmentNames.GetValueOrDefault(group.Id, string.Empty), group.Count))
                .OrderBy(group => group.DepartmentName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(group => group.DepartmentId),
        ];
        if (notDisclosed > 0)
        {
            result.Add(new GrievanceDepartmentCountDto("NotDisclosed", null, NotDisclosedLabel, notDisclosed));
        }

        if (suppressed > 0)
        {
            result.Add(new GrievanceDepartmentCountDto("Suppressed", null, SuppressedLabel, suppressed));
        }

        return result;
    }

    /// <summary>(Resolved + Closed) / (counted - Rejected) x 100, 2 decimals rounded half away from zero; 0 when the divisor is 0.</summary>
    public static decimal ResolutionRatePct(IReadOnlyCollection<GrievanceCaseFact> facts)
    {
        var divisor = facts.Count(fact => fact.Status != GrievanceStatus.Rejected);
        return divisor == 0
            ? 0m
            : Math.Round(facts.Count(fact => fact.IsFinished) * 100m / divisor, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>The average resolution days of the cases now Resolved or Closed, 1 decimal; null (never 0) when there is none.</summary>
    public static decimal? AverageResolutionDays(IReadOnlyCollection<GrievanceCaseFact> facts)
    {
        var days = facts.Select(fact => fact.ResolutionDays).OfType<int>().ToList();
        return days.Count == 0 ? null : Math.Round((decimal)days.Sum() / days.Count, 1, MidpointRounding.AwayFromZero);
    }

    /// <summary>The last 12 calendar months, oldest first, the current month last, zeros included.</summary>
    public static IReadOnlyList<GrievanceMonthlyCountDto> MonthlyTrend(IReadOnlyCollection<GrievanceCaseFact> facts, DateOnly today)
    {
        var current = new DateOnly(today.Year, today.Month, 1);
        return
        [
            .. Enumerable.Range(0, TrendMonths).Select(index => current.AddMonths(index - TrendMonths + 1)).Select(month => new GrievanceMonthlyCountDto(
                month.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                facts.Count(fact => SameMonth(fact.SubmittedOn, month)),
                facts.Count(fact => fact.IsFinished && fact.ResolvedOn is { } resolved && SameMonth(resolved, month)))),
        ];
    }

    private static bool SameMonth(DateOnly date, DateOnly month) => date.Year == month.Year && date.Month == month.Month;
}

/// <summary>Dates in the business time zone for instants stored in UTC.</summary>
internal static class GrievanceDates
{
    /// <summary>The business date of an instant: the day whose start (business midnight, in UTC) is the last one at or before it.</summary>
    public static DateOnly DateOf(IClock clock, DateTime utc)
    {
        ArgumentNullException.ThrowIfNull(clock);
        var guess = DateOnly.FromDateTime(utc);
        for (var day = guess.AddDays(-1); day <= guess.AddDays(1); day = day.AddDays(1))
        {
            if (clock.StartOfDayUtc(day) <= utc && utc < clock.StartOfDayUtc(day.AddDays(1)))
            {
                return day;
            }
        }

        return guess;
    }
}
