using System.Linq.Expressions;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Organization;
using Apex.Hrms.Application.Performance.Scorecards;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Organization;
using Apex.Hrms.Domain.Performance;

namespace Apex.Hrms.Application.Performance.Insights;

/// <summary>The read-model row of a scorecard in a dashboard or report: the scorecard joined to its employee, period and copied department.</summary>
internal sealed class KpiScorecardRow
{
    public int ScorecardId { get; init; }

    public int EmployeeId { get; init; }

    public string EmployeeCode { get; init; } = string.Empty;

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public int DepartmentId { get; init; }

    public string DepartmentName { get; init; } = string.Empty;

    public int PeriodId { get; init; }

    public string PeriodName { get; init; } = string.Empty;

    public KpiFrequency PeriodType { get; init; }

    public DateOnly PeriodStartDate { get; init; }

    public DateOnly PeriodEndDate { get; init; }

    public ScorecardStatus Status { get; init; }

    public decimal? OverallScore { get; init; }

    public string? RatingLabel { get; init; }

    public string EmployeeName => ScorecardMapping.FullName(FirstName, LastName);

    public KpiEmployeeScoreDto ToScore() => new(
        ScorecardId,
        EmployeeId,
        EmployeeCode,
        EmployeeName,
        DepartmentId,
        DepartmentName,
        ScorecardStatusText.ToText(Status),
        OverallScore,
        RatingLabel);

    public KpiReportScorecardRowDto ToReportRow() => new(
        ScorecardId,
        EmployeeId,
        EmployeeCode,
        EmployeeName,
        DepartmentId,
        DepartmentName,
        PeriodId,
        PeriodName,
        ScorecardStatusText.ToText(Status),
        OverallScore,
        RatingLabel);
}

/// <summary>The read-model row of one KPI of a scorecard with the scorecard's employee, period, department and the KPI's category.</summary>
internal sealed class KpiItemRow
{
    public int ItemId { get; init; }

    public int ScorecardId { get; init; }

    public int EmployeeId { get; init; }

    public string EmployeeCode { get; init; } = string.Empty;

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public int DepartmentId { get; init; }

    public string DepartmentName { get; init; } = string.Empty;

    public int PeriodId { get; init; }

    public string PeriodName { get; init; } = string.Empty;

    public DateOnly PeriodStartDate { get; init; }

    public ScorecardStatus Status { get; init; }

    public string KpiCode { get; init; } = string.Empty;

    public string KpiName { get; init; } = string.Empty;

    public string CategoryName { get; init; } = string.Empty;

    public string? Unit { get; init; }

    public decimal Target { get; init; }

    public decimal? ActualValue { get; init; }

    public decimal? AchievementPct { get; init; }

    public decimal Weight { get; init; }

    public decimal? WeightedScore { get; init; }

    public short SortOrder { get; init; }

    public KpiTargetVsActualRowDto ToDto() => new(
        ItemId,
        ScorecardId,
        EmployeeId,
        EmployeeCode,
        ScorecardMapping.FullName(FirstName, LastName),
        DepartmentId,
        DepartmentName,
        PeriodId,
        PeriodName,
        ScorecardStatusText.ToText(Status),
        KpiCode,
        KpiName,
        CategoryName,
        Unit,
        Target,
        ActualValue,
        AchievementPct,
        Weight,
        WeightedScore);
}

/// <summary>The columns of a period a dashboard or report names.</summary>
internal sealed class KpiPeriodRow
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public KpiFrequency PeriodType { get; init; }

    public DateOnly StartDate { get; init; }

    public DateOnly EndDate { get; init; }

    public EvaluationPeriodStatus Status { get; init; }

    public DateOnly? SubmissionDueDate { get; init; }

    public KpiPeriodRefDto ToDto() => new(
        Id,
        Name,
        KpiFrequencyText.ToText(PeriodType),
        StartDate,
        EndDate,
        EvaluationPeriodStatusText.ToText(Status),
        SubmissionDueDate);
}

/// <summary>
/// The sources of the KPI dashboards, reports and exports. <see cref="Visible"/> is the scorecard scope of the caller
/// (<c>ScorecardScope</c>: own, team or all), and every other query here starts from a set built on it, so nothing outside the
/// scope can reach a figure. Filters are validated (an unknown status or period type is 400) and applied inside the database.
/// </summary>
internal sealed class KpiReportSource(IReadModel read, ScorecardReader reader)
{
    /// <summary>The first and last year a <c>year</c> filter accepts.</summary>
    public const int FirstYear = 1900;

    public const int LastYear = 9998;

    public IReadModel Read => read;

    /// <summary>The scorecards the caller may see.</summary>
    public IQueryable<Scorecard> Visible() => reader.Scoped();

    /// <summary>
    /// The visible scorecards narrowed by <paramref name="filter"/>, <paramref name="search"/> (employee code or name) and an optional
    /// range of period dates (a period counts when it overlaps <paramref name="from"/>..<paramref name="to"/>).
    /// </summary>
    public Result<IQueryable<Scorecard>> Filtered(KpiReportFilter filter, string? search = null, DateOnly? from = null, DateOnly? to = null) =>
        Apply(Visible(), filter, search, from, to);

    public Result<IQueryable<Scorecard>> Apply(IQueryable<Scorecard> source, KpiReportFilter filter, string? search = null, DateOnly? from = null, DateOnly? to = null)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var predicate = Predicates.Always<Scorecard>();
        if (filter.CompanyId is { } companyId)
        {
            predicate = predicate.And(scorecard => scorecard.CompanyId == companyId);
        }

        if (filter.BranchId is { } branchId)
        {
            predicate = predicate.And(scorecard => scorecard.BranchId == branchId);
        }

        if (filter.DepartmentId is { } departmentId)
        {
            predicate = predicate.And(scorecard => scorecard.DepartmentId == departmentId);
        }

        if (filter.PositionId is { } positionId)
        {
            predicate = predicate.And(scorecard => scorecard.PositionId == positionId);
        }

        if (filter.EmployeeId is { } employeeId)
        {
            predicate = predicate.And(scorecard => scorecard.EmployeeId == employeeId);
        }

        if (filter.PeriodId is { } periodId)
        {
            predicate = predicate.And(scorecard => scorecard.PeriodId == periodId);
        }

        if (FilterPredicates.IsSet(filter.Status))
        {
            if (!ScorecardStatusText.TryParse(filter.Status!.Trim(), out var status))
            {
                return Invalid("status", $"Unknown status \"{filter.Status.Trim()}\". Use {ScorecardStatusText.Map.OneOf}.");
            }

            predicate = predicate.And(scorecard => scorecard.Status == status);
        }

        var periods = read.Query<EvaluationPeriod>();
        var narrowsPeriods = false;
        if (FilterPredicates.IsSet(filter.PeriodType))
        {
            var text = filter.PeriodType!.Trim();
            if (string.Equals(text, "Annual", StringComparison.OrdinalIgnoreCase))
            {
                text = KpiFrequencyText.ToText(KpiFrequency.Yearly);
            }

            if (!KpiFrequencyText.TryParse(text, out var type))
            {
                return Invalid("periodType", $"Unknown period type \"{filter.PeriodType.Trim()}\". Use {KpiFrequencyText.Map.OneOf} (or Annual).");
            }

            periods = periods.Where(period => period.PeriodType == type);
            narrowsPeriods = true;
        }

        if (filter.Year is { } year)
        {
            if (year is < FirstYear or > LastYear)
            {
                return Invalid("year", $"Year must be between {FirstYear} and {LastYear}.");
            }

            var first = new DateOnly(year, 1, 1);
            var next = first.AddYears(1);
            periods = periods.Where(period => period.StartDate >= first && period.StartDate < next);
            narrowsPeriods = true;
        }

        if (from is { } start)
        {
            periods = periods.Where(period => period.EndDate >= start);
            narrowsPeriods = true;
        }

        if (to is { } end)
        {
            periods = periods.Where(period => period.StartDate <= end);
            narrowsPeriods = true;
        }

        var scorecards = source.Where(predicate);
        if (narrowsPeriods)
        {
            var periodIds = periods.Select(period => period.Id);
            scorecards = scorecards.Where(scorecard => periodIds.Contains(scorecard.PeriodId));
        }

        if (filter.CategoryId is { } categoryId)
        {
            var items = read.Query<ScorecardItem>().Where(item => item.CategoryId == categoryId);
            scorecards = scorecards.Where(scorecard => items.Any(item => item.ScorecardId == scorecard.Id));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            if (search.Length > Paging.MaxSearchLength)
            {
                return Invalid("search", $"Search must be at most {Paging.MaxSearchLength} characters.");
            }

            var matching = read.Query<Employee>().Where(ScorecardQuerySpecification.Search(search.Trim())).Select(employee => employee.Id);
            scorecards = scorecards.Where(scorecard => matching.Contains(scorecard.EmployeeId));
        }

        return Result.Success(scorecards);
    }

    /// <summary>The scorecards joined to employee, period and copied department.</summary>
    public IQueryable<KpiScorecardRow> Rows(IQueryable<Scorecard> source) =>
        from scorecard in source
        join employee in read.Query<Employee>() on scorecard.EmployeeId equals employee.Id
        join period in read.Query<EvaluationPeriod>() on scorecard.PeriodId equals period.Id
        join department in read.Query<Department>() on scorecard.DepartmentId equals department.Id
        select new KpiScorecardRow
        {
            ScorecardId = scorecard.Id,
            EmployeeId = scorecard.EmployeeId,
            EmployeeCode = employee.EmployeeCode,
            FirstName = employee.FirstName,
            LastName = employee.LastName,
            DepartmentId = scorecard.DepartmentId,
            DepartmentName = department.Name,
            PeriodId = scorecard.PeriodId,
            PeriodName = period.Name,
            PeriodType = period.PeriodType,
            PeriodStartDate = period.StartDate,
            PeriodEndDate = period.EndDate,
            Status = scorecard.Status,
            OverallScore = scorecard.OverallScore,
            RatingLabel = scorecard.RatingLabel,
        };

    /// <summary>The KPIs of <paramref name="source"/> (of one category when <paramref name="categoryId"/> is set) with their scorecard's names.</summary>
    public IQueryable<KpiItemRow> ItemRows(IQueryable<Scorecard> source, int? categoryId)
    {
        var items = read.Query<ScorecardItem>();
        if (categoryId is { } category)
        {
            items = items.Where(item => item.CategoryId == category);
        }

        return from item in items
               join scorecard in source on item.ScorecardId equals scorecard.Id
               join employee in read.Query<Employee>() on scorecard.EmployeeId equals employee.Id
               join period in read.Query<EvaluationPeriod>() on scorecard.PeriodId equals period.Id
               join department in read.Query<Department>() on scorecard.DepartmentId equals department.Id
               join kpiCategory in read.Query<KpiCategory>() on item.CategoryId equals kpiCategory.Id
               select new KpiItemRow
               {
                   ItemId = item.Id,
                   ScorecardId = scorecard.Id,
                   EmployeeId = scorecard.EmployeeId,
                   EmployeeCode = employee.EmployeeCode,
                   FirstName = employee.FirstName,
                   LastName = employee.LastName,
                   DepartmentId = scorecard.DepartmentId,
                   DepartmentName = department.Name,
                   PeriodId = scorecard.PeriodId,
                   PeriodName = period.Name,
                   PeriodStartDate = period.StartDate,
                   Status = scorecard.Status,
                   KpiCode = item.KpiCode,
                   KpiName = item.KpiName,
                   CategoryName = kpiCategory.Name,
                   Unit = item.Unit,
                   Target = item.Target,
                   ActualValue = item.ActualValue,
                   AchievementPct = item.AchievementPct,
                   Weight = item.Weight,
                   WeightedScore = item.WeightedScore,
                   SortOrder = item.SortOrder,
               };
    }

    /// <summary>
    /// The period a dashboard or report is about: <paramref name="periodId"/> when given (404 when no such period), otherwise the most
    /// recent Active period among <paramref name="candidates"/> (by start date, then id), else the most recent of them, else none.
    /// One query either way.
    /// </summary>
    public async Task<Result<KpiPeriodRow?>> PeriodAsync(int? periodId, IQueryable<EvaluationPeriod> candidates, CancellationToken cancellationToken)
    {
        if (periodId is { } id)
        {
            var named = await read.FirstOrDefaultAsync(PeriodRows(read.Query<EvaluationPeriod>().Where(period => period.Id == id)), cancellationToken);
            return named is null
                ? Result.Failure<KpiPeriodRow?>(AppError.NotFound($"Evaluation period #{id} was not found."))
                : Result.Success<KpiPeriodRow?>(named);
        }

        var active = EvaluationPeriodStatus.Active;
        var chosen = await read.FirstOrDefaultAsync(
            PeriodRows(candidates
                .OrderBy(period => period.Status == active ? 0 : 1)
                .ThenByDescending(period => period.StartDate)
                .ThenByDescending(period => period.Id)),
            cancellationToken);
        return Result.Success(chosen);
    }

    /// <summary>The periods that have at least one scorecard in <paramref name="scorecards"/>.</summary>
    public IQueryable<EvaluationPeriod> PeriodsOf(IQueryable<Scorecard> scorecards)
    {
        var ids = scorecards.Select(scorecard => scorecard.PeriodId);
        return read.Query<EvaluationPeriod>().Where(period => ids.Contains(period.Id));
    }

    public static IQueryable<KpiPeriodRow> PeriodRows(IQueryable<EvaluationPeriod> periods) =>
        periods.Select(period => new KpiPeriodRow
        {
            Id = period.Id,
            Name = period.Name,
            PeriodType = period.PeriodType,
            StartDate = period.StartDate,
            EndDate = period.EndDate,
            Status = period.Status,
            SubmissionDueDate = period.SubmissionDueDate,
        });

    /// <summary>The sort of a report: a whitelisted key (400 on <c>sort</c> otherwise) and the direction; null when the request names none.</summary>
    public static Result<(Expression<Func<TRow, object>> Key, bool Descending)?> Sort<TRow>(
        SortState? sort,
        IReadOnlyDictionary<string, Expression<Func<TRow, object>>> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        if (sort is not { Active: { Length: > 0 } active, Direction: { Length: > 0 } direction })
        {
            return Result.Success<(Expression<Func<TRow, object>>, bool)?>(null);
        }

        if (!keys.TryGetValue(active, out var key))
        {
            return Result.Failure<(Expression<Func<TRow, object>>, bool)?>(AppError.Validation(
                $"Cannot sort by \"{active}\". Sortable fields: {string.Join(", ", keys.Keys)}.", "sort", "Choose a sortable field."));
        }

        return Result.Success<(Expression<Func<TRow, object>>, bool)?>((key, string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>One page of an ordered query: the total, the clamped page index and the rows, read in two queries.</summary>
    public async Task<(List<T> Rows, int Total, int PageIndex, int PageSize)> PageAsync<T>(
        IQueryable<T> unordered,
        Func<IQueryable<T>, IOrderedQueryable<T>> order,
        int requestedIndex,
        int requestedSize,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);
        var total = await read.CountAsync(unordered, cancellationToken);
        var pageSize = Paging.ClampPageSize(requestedSize);
        var pageIndex = Paging.ClampPageIndex(Math.Max(0, requestedIndex), total, pageSize);
        var rows = total == 0
            ? []
            : await read.ToListAsync(order(unordered).Skip(pageIndex * pageSize).Take(pageSize), cancellationToken);
        return (rows, total, pageIndex, pageSize);
    }

    private static Result<IQueryable<Scorecard>> Invalid(string field, string message) =>
        Result.Failure<IQueryable<Scorecard>>(AppError.Validation(message, field, message));
}
