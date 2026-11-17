using System.Linq.Expressions;
using Apex.Hrms.Application.Security;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Organization;
using Apex.Hrms.Domain.Performance;

namespace Apex.Hrms.Application.Performance.Insights;

// The KPI reports. Each starts from the caller's visible scorecards (KpiReportSource.Visible), applies the validated filters and
// the sort whitelist in the database, and reads one page plus a fixed number of small queries. Averages, minimums, maximums and
// ranks count Finalized scorecards only, so Rejected and unscored scorecards never move them; the row reports list every status.

/// <summary>One page of the per-period summary. Sort: <c>periodStart</c> (default, newest first), <c>periodName</c>, <c>periodType</c>.</summary>
internal sealed class KpiSummaryReportHandler(KpiReportSource source) : IKpiSummaryReportHandler
{
    private static readonly IReadOnlyDictionary<string, Expression<Func<KpiPeriodRow, object>>> SortKeys =
        new Dictionary<string, Expression<Func<KpiPeriodRow, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["periodStart"] = row => row.StartDate,
            ["periodName"] = row => row.Name,
            ["periodType"] = row => row.PeriodType,
        };

    public async Task<Result<PagedResult<KpiPeriodSummaryDto>>> HandleAsync(PageRequest<KpiReportFilter> request, CancellationToken cancellationToken)
    {
        var sort = KpiReportSource.Sort(request.Sort, SortKeys);
        var filtered = source.Filtered(request.Filters, request.Search);
        if (sort.IsFailure || filtered.IsFailure)
        {
            return Result.Failure<PagedResult<KpiPeriodSummaryDto>>(sort.IsFailure ? sort.Error : filtered.Error);
        }

        var read = source.Read;
        var cards = filtered.Value;
        var periods = KpiReportSource.PeriodRows(source.PeriodsOf(cards));
        var page = await source.PageAsync(
            periods,
            rows => sort.Value is { } chosen
                ? (chosen.Descending ? rows.OrderByDescending(chosen.Key) : rows.OrderBy(chosen.Key)).ThenBy(row => row.Id)
                : rows.OrderByDescending(row => row.StartDate).ThenByDescending(row => row.Id),
            request.PageIndex,
            request.PageSize,
            cancellationToken);

        var ids = page.Rows.Select(row => row.Id).ToArray();
        var planning = ScorecardStatus.Planning;
        var finalized = ScorecardStatus.Finalized;
        var rejected = ScorecardStatus.Rejected;
        var figures = ids.Length == 0
            ? []
            : (await read.ToListAsync(
                cards.Where(scorecard => ids.Contains(scorecard.PeriodId)).GroupBy(scorecard => scorecard.PeriodId).Select(group => new
                {
                    PeriodId = group.Key,
                    Scorecards = group.Count(),
                    Planning = group.Count(scorecard => scorecard.Status == planning),
                    Finalized = group.Count(scorecard => scorecard.Status == finalized),
                    Rejected = group.Count(scorecard => scorecard.Status == rejected),
                    Scored = group.Count(scorecard => scorecard.Status == finalized && scorecard.OverallScore != null),
                    ScoreSum = group.Sum(scorecard => scorecard.Status == finalized ? scorecard.OverallScore : null),
                    MinScore = group.Min(scorecard => scorecard.Status == finalized ? scorecard.OverallScore : null),
                    MaxScore = group.Max(scorecard => scorecard.Status == finalized ? scorecard.OverallScore : null),
                }),
                cancellationToken)).ToDictionary(figure => figure.PeriodId);

        return Result.Success(new PagedResult<KpiPeriodSummaryDto>(
            [.. page.Rows.Select(period =>
            {
                // The page and the figures are two queries: a period whose scorecards changed in between (deleted, say) has no figures
                // and reads as zero figures, never a KeyNotFoundException.
                var figure = figures.GetValueOrDefault(period.Id);
                var scorecards = figure?.Scorecards ?? 0;
                var inPlanning = figure?.Planning ?? 0;
                var finalizedCount = figure?.Finalized ?? 0;
                return new KpiPeriodSummaryDto(
                    period.Id,
                    period.Name,
                    KpiFrequencyText.ToText(period.PeriodType),
                    period.StartDate,
                    period.EndDate,
                    EvaluationPeriodStatusText.ToText(period.Status),
                    scorecards,
                    inPlanning,
                    finalizedCount,
                    figure?.Rejected ?? 0,
                    KpiFigures.Average(figure?.ScoreSum, figure?.Scored ?? 0),
                    figure?.MinScore,
                    figure?.MaxScore,
                    KpiFigures.CompletionRate(finalizedCount, scorecards - inPlanning));
            })],
            page.Total,
            page.PageIndex,
            page.PageSize));
    }
}

/// <summary>
/// One employee's history. The employee must be in the caller's scope: themselves, a direct report or everyone by tier, or the
/// employee of a scorecard the caller reviews; anyone else (and an unknown id) is 404. Sort: <c>periodStart</c> (default, newest
/// first), <c>periodName</c>, <c>overallScore</c>, <c>status</c>. Four queries.
/// </summary>
internal sealed class KpiEmployeeReportHandler(KpiReportSource source, SelfService self) : IKpiEmployeeReportHandler
{
    private static readonly IReadOnlyDictionary<string, Expression<Func<KpiScorecardRow, object>>> SortKeys =
        new Dictionary<string, Expression<Func<KpiScorecardRow, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["periodStart"] = row => row.PeriodStartDate,
            ["periodName"] = row => row.PeriodName,
            ["overallScore"] = row => row.OverallScore ?? 0m,
            ["status"] = row => row.Status,
        };

    public async Task<Result<KpiEmployeeReportDto>> HandleAsync(int employeeId, PageRequest<KpiReportFilter> request, CancellationToken cancellationToken)
    {
        var sort = KpiReportSource.Sort(request.Sort, SortKeys);
        if (sort.IsFailure)
        {
            return Result.Failure<KpiEmployeeReportDto>(sort.Error);
        }

        var read = source.Read;
        var person = await read.FirstOrDefaultAsync(
            read.Query<Employee>().Where(employee => employee.Id == employeeId).Select(employee => new { employee.EmployeeCode, employee.FirstName, employee.LastName }),
            cancellationToken);
        var theirs = source.Visible().Where(scorecard => scorecard.EmployeeId == employeeId);
        if (person is null || (!self.CanAccess("kpi", employeeId) && await read.CountAsync(theirs, cancellationToken) == 0))
        {
            return Result.Failure<KpiEmployeeReportDto>(AppError.NotFound($"Employee #{employeeId} was not found."));
        }

        var filtered = source.Apply(theirs, request.Filters with { EmployeeId = null }, search: null);
        if (filtered.IsFailure)
        {
            return Result.Failure<KpiEmployeeReportDto>(filtered.Error);
        }

        var groups = await read.ToListAsync(KpiDashboardQueries.ByStatus(filtered.Value, 0), cancellationToken);
        var finalizedGroup = groups.FirstOrDefault(group => group.Status == ScorecardStatus.Finalized);

        var page = await source.PageAsync(
            source.Rows(filtered.Value),
            rows => sort.Value is { } chosen
                ? (chosen.Descending ? rows.OrderByDescending(chosen.Key) : rows.OrderBy(chosen.Key)).ThenBy(row => row.ScorecardId)
                : rows.OrderByDescending(row => row.PeriodStartDate).ThenByDescending(row => row.ScorecardId),
            request.PageIndex,
            request.PageSize,
            cancellationToken);

        var ids = page.Rows.Select(row => row.ScorecardId).ToArray();
        var items = ids.Length == 0
            ? []
            : await read.ToListAsync(ItemsOf(read, ids, request.Filters.CategoryId), cancellationToken);
        var byScorecard = items.GroupBy(item => item.ScorecardId).ToDictionary(group => group.Key, group => group.Select(item => item.Dto).ToList());

        return Result.Success(new KpiEmployeeReportDto(
            employeeId,
            person.EmployeeCode,
            Scorecards.ScorecardMapping.FullName(person.FirstName, person.LastName),
            finalizedGroup?.Count ?? 0,
            KpiDashboardQueries.FinalizedAverage(groups),
            new PagedResult<KpiEmployeeScorecardDto>(
                [.. page.Rows.Select(row => new KpiEmployeeScorecardDto(
                    row.ScorecardId,
                    row.PeriodId,
                    row.PeriodName,
                    KpiFrequencyText.ToText(row.PeriodType),
                    row.PeriodStartDate,
                    row.PeriodEndDate,
                    row.DepartmentId,
                    row.DepartmentName,
                    ScorecardStatusText.ToText(row.Status),
                    row.OverallScore,
                    row.RatingLabel,
                    byScorecard.GetValueOrDefault(row.ScorecardId) ?? []))],
                page.Total,
                page.PageIndex,
                page.PageSize)));
    }

    private sealed class ItemOfScorecard
    {
        public int ScorecardId { get; init; }

        public KpiReportItemDto Dto { get; init; } = null!;
    }

    /// <summary>The KPIs of the page's scorecards (of one category when asked), by sort order: one query.</summary>
    private static IQueryable<ItemOfScorecard> ItemsOf(Abstractions.IReadModel read, int[] scorecardIds, int? categoryId)
    {
        var items = read.Query<ScorecardItem>().Where(item => scorecardIds.Contains(item.ScorecardId));
        if (categoryId is { } category)
        {
            items = items.Where(item => item.CategoryId == category);
        }

        return from item in items
               join kpiCategory in read.Query<KpiCategory>() on item.CategoryId equals kpiCategory.Id
               orderby item.ScorecardId, item.SortOrder, item.Id
               select new ItemOfScorecard
               {
                   ScorecardId = item.ScorecardId,
                   Dto = new KpiReportItemDto(
                       item.Id,
                       item.KpiCode,
                       item.KpiName,
                       kpiCategory.Name,
                       item.Unit,
                       item.Target,
                       item.ActualValue,
                       item.AchievementPct,
                       item.Weight,
                       item.WeightedScore),
               };
    }
}

/// <summary>
/// The scorecards whose copied department is the route's, for one period: <c>periodId</c>, or the most recent Active period among the
/// visible scorecards of the department, else the most recent. An unknown department is 404, and so is (with the same message) a
/// department the caller has no business reading about, so the route does not reveal department names: the caller must hold
/// <c>organization:View</c>, be of the HR tier, or see at least one scorecard of that department (any period, any filter). Otherwise a
/// department with nothing matching is an empty report. Sort: <c>employeeCode</c> (default), <c>employeeName</c>, <c>overallScore</c>,
/// <c>ratingLabel</c>, <c>status</c>.
/// </summary>
internal sealed class KpiDepartmentReportHandler(KpiReportSource source, SelfService self, ICallerPermissions permissions) : IKpiDepartmentReportHandler
{
    private static readonly IReadOnlyDictionary<string, Expression<Func<KpiScorecardRow, object>>> SortKeys =
        new Dictionary<string, Expression<Func<KpiScorecardRow, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["employeeCode"] = row => row.EmployeeCode,
            ["employeeName"] = row => row.FirstName + " " + row.LastName,
            ["overallScore"] = row => row.OverallScore ?? 0m,
            ["ratingLabel"] = row => row.RatingLabel ?? string.Empty,
            ["status"] = row => row.Status,
        };

    public async Task<Result<KpiDepartmentReportDto>> HandleAsync(int departmentId, PageRequest<KpiReportFilter> request, CancellationToken cancellationToken)
    {
        var sort = KpiReportSource.Sort(request.Sort, SortKeys);
        if (sort.IsFailure)
        {
            return Result.Failure<KpiDepartmentReportDto>(sort.Error);
        }

        var read = source.Read;
        var name = await read.FirstOrDefaultAsync(
            read.Query<Department>().Where(department => department.Id == departmentId).Select(department => department.Name),
            cancellationToken);
        if (name is null || !await MayReadAsync(departmentId, cancellationToken))
        {
            return Result.Failure<KpiDepartmentReportDto>(AppError.NotFound($"Department #{departmentId} was not found."));
        }

        var filtered = source.Filtered(request.Filters with { DepartmentId = departmentId, PeriodId = null }, request.Search);
        if (filtered.IsFailure)
        {
            return Result.Failure<KpiDepartmentReportDto>(filtered.Error);
        }

        var chosen = await source.PeriodAsync(request.Filters.PeriodId, source.PeriodsOf(filtered.Value), cancellationToken);
        if (chosen.IsFailure)
        {
            return Result.Failure<KpiDepartmentReportDto>(chosen.Error);
        }

        var periodId = chosen.Value?.Id ?? 0;
        var cards = filtered.Value.Where(scorecard => scorecard.PeriodId == periodId);
        var groups = await read.ToListAsync(KpiDashboardQueries.ByStatus(cards, 0), cancellationToken);

        var page = await source.PageAsync(
            source.Rows(cards),
            rows => sort.Value is { } key
                ? (key.Descending ? rows.OrderByDescending(key.Key) : rows.OrderBy(key.Key)).ThenBy(row => row.ScorecardId)
                : rows.OrderBy(row => row.EmployeeCode).ThenBy(row => row.ScorecardId),
            request.PageIndex,
            request.PageSize,
            cancellationToken);

        return Result.Success(new KpiDepartmentReportDto(
            departmentId,
            name,
            chosen.Value?.ToDto(),
            groups.Sum(group => group.Count),
            groups.Where(group => group.Status == ScorecardStatus.Finalized).Sum(group => group.Count),
            KpiDashboardQueries.FinalizedAverage(groups),
            new PagedResult<KpiReportScorecardRowDto>([.. page.Rows.Select(row => row.ToReportRow())], page.Total, page.PageIndex, page.PageSize)));
    }

    /// <summary>True when the caller may learn about the department: <c>organization:View</c>, the HR tier, or a visible scorecard of it.</summary>
    private async Task<bool> MayReadAsync(int departmentId, CancellationToken cancellationToken) =>
        permissions.Has("organization", "View")
        || self.TierOf("kpi") == AccessTier.All
        || await source.Read.CountAsync(source.Visible().Where(scorecard => scorecard.DepartmentId == departmentId), cancellationToken) > 0;
}

/// <summary>
/// Finalized scorecards (with a score) ranked by overall score, highest first. Equal scores share a rank (standard competition
/// ranking, 1, 2, 2, 4) and are listed by employee code. The order is fixed: <c>sort</c> may only be <c>rank</c> ascending. The
/// ranks are over the filtered set, so pass <c>periodId</c> for a ranking within one period. Three queries.
/// </summary>
internal sealed class KpiRankingReportHandler(KpiReportSource source) : IKpiRankingReportHandler
{
    public async Task<Result<PagedResult<KpiRankingRowDto>>> HandleAsync(PageRequest<KpiReportFilter> request, CancellationToken cancellationToken)
    {
        if (request.Sort is { Active: { Length: > 0 } active, Direction: var direction }
            && !(string.Equals(active, "rank", StringComparison.OrdinalIgnoreCase) && !string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure<PagedResult<KpiRankingRowDto>>(AppError.Validation(
                "The ranking is always ordered by rank (sort=rank:asc).", "sort", "Choose a sortable field."));
        }

        var filtered = source.Filtered(request.Filters, request.Search);
        if (filtered.IsFailure)
        {
            return Result.Failure<PagedResult<KpiRankingRowDto>>(filtered.Error);
        }

        var ranked = Ranked(filtered.Value);
        var page = await source.PageAsync(source.Rows(ranked), Order, request.PageIndex, request.PageSize, cancellationToken);
        if (page.Rows.Count == 0)
        {
            return Result.Success(new PagedResult<KpiRankingRowDto>([], page.Total, page.PageIndex, page.PageSize));
        }

        // The first row of the page may share its score with rows of the previous page: its rank is one more than the number of
        // higher scores. The rest follow by position.
        var first = page.Rows[0].OverallScore;
        var higher = await source.Read.CountAsync(ranked.Where(scorecard => scorecard.OverallScore > first), cancellationToken);
        return Result.Success(new PagedResult<KpiRankingRowDto>(
            ToDtos(page.Rows, higher + 1, (page.PageIndex * page.PageSize) + 1),
            page.Total,
            page.PageIndex,
            page.PageSize));
    }

    /// <summary>The Finalized, scored scorecards of a set: the only ones a ranking contains.</summary>
    public static IQueryable<Scorecard> Ranked(IQueryable<Scorecard> scorecards)
    {
        var finalized = ScorecardStatus.Finalized;
        return scorecards.Where(scorecard => scorecard.Status == finalized && scorecard.OverallScore != null);
    }

    public static IOrderedQueryable<KpiScorecardRow> Order(IQueryable<KpiScorecardRow> rows) =>
        rows.OrderByDescending(row => row.OverallScore).ThenBy(row => row.EmployeeCode).ThenBy(row => row.ScorecardId);

    public static IReadOnlyList<KpiRankingRowDto> ToDtos(IReadOnlyList<KpiScorecardRow> rows, int firstRank, int firstPosition)
    {
        var ranks = KpiFigures.CompetitionRanks([.. rows.Select(row => row.OverallScore ?? 0m)], firstRank, firstPosition);
        return [.. rows.Select((row, i) => new KpiRankingRowDto(
            ranks[i],
            row.ScorecardId,
            row.EmployeeId,
            row.EmployeeCode,
            row.EmployeeName,
            row.DepartmentId,
            row.DepartmentName,
            row.PeriodId,
            row.PeriodName,
            row.OverallScore ?? 0m,
            row.RatingLabel))];
    }
}

/// <summary>
/// Achievement per KPI template over the matching scorecards (with <c>categoryId</c>, only the KPIs of that category). Sort:
/// <c>kpiCode</c> (default), <c>kpiName</c>, <c>categoryName</c>, <c>assigned</c>, <c>counted</c>, <c>averageAchievement</c>. Two queries.
/// </summary>
internal sealed class KpiAchievementReportHandler(KpiReportSource source) : IKpiAchievementReportHandler
{
    private sealed class AchievementRow
    {
        public int TemplateId { get; init; }

        public string KpiCode { get; init; } = string.Empty;

        public string KpiName { get; init; } = string.Empty;

        public int CategoryId { get; init; }

        public string CategoryName { get; init; } = string.Empty;

        public int Assigned { get; init; }

        public int Counted { get; init; }

        public decimal? Sum { get; init; }

        public decimal? Min { get; init; }

        public decimal? Max { get; init; }
    }

    private static readonly IReadOnlyDictionary<string, Expression<Func<AchievementRow, object>>> SortKeys =
        new Dictionary<string, Expression<Func<AchievementRow, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["kpiCode"] = row => row.KpiCode,
            ["kpiName"] = row => row.KpiName,
            ["categoryName"] = row => row.CategoryName,
            ["assigned"] = row => row.Assigned,
            ["counted"] = row => row.Counted,
            // The same definition as the shown figure (KpiFigures.Average: the sum over the counted KPIs), unrounded; none counted sorts as 0.
            ["averageAchievement"] = row => row.Counted == 0 ? 0m : (row.Sum ?? 0m) / row.Counted,
        };

    public async Task<Result<PagedResult<KpiAchievementRowDto>>> HandleAsync(PageRequest<KpiReportFilter> request, CancellationToken cancellationToken)
    {
        var sort = KpiReportSource.Sort(request.Sort, SortKeys);
        var filtered = source.Filtered(request.Filters, request.Search);
        if (sort.IsFailure || filtered.IsFailure)
        {
            return Result.Failure<PagedResult<KpiAchievementRowDto>>(sort.IsFailure ? sort.Error : filtered.Error);
        }

        var read = source.Read;
        var finalized = ScorecardStatus.Finalized;
        var items = read.Query<ScorecardItem>();
        if (request.Filters.CategoryId is { } category)
        {
            items = items.Where(item => item.CategoryId == category);
        }

        var facts =
            from item in items
            join scorecard in filtered.Value on item.ScorecardId equals scorecard.Id
            select new { item.TemplateId, Counts = scorecard.Status == finalized && item.AchievementPct != null, item.AchievementPct };
        var perTemplate = facts.GroupBy(fact => fact.TemplateId).Select(group => new
        {
            TemplateId = group.Key,
            Assigned = group.Count(),
            Counted = group.Count(fact => fact.Counts),
            Sum = group.Sum(fact => fact.Counts ? fact.AchievementPct : null),
            Min = group.Min(fact => fact.Counts ? fact.AchievementPct : null),
            Max = group.Max(fact => fact.Counts ? fact.AchievementPct : null),
        });
        var rows =
            from grouped in perTemplate
            join template in read.Query<KpiTemplate>() on grouped.TemplateId equals template.Id
            join kpiCategory in read.Query<KpiCategory>() on template.CategoryId equals kpiCategory.Id
            select new AchievementRow
            {
                TemplateId = grouped.TemplateId,
                KpiCode = template.Code,
                KpiName = template.Name,
                CategoryId = template.CategoryId,
                CategoryName = kpiCategory.Name,
                Assigned = grouped.Assigned,
                Counted = grouped.Counted,
                Sum = grouped.Sum,
                Min = grouped.Min,
                Max = grouped.Max,
            };

        var page = await source.PageAsync(
            rows,
            ordered => sort.Value is { } key
                ? (key.Descending ? ordered.OrderByDescending(key.Key) : ordered.OrderBy(key.Key)).ThenBy(row => row.TemplateId)
                : ordered.OrderBy(row => row.KpiCode).ThenBy(row => row.TemplateId),
            request.PageIndex,
            request.PageSize,
            cancellationToken);

        return Result.Success(new PagedResult<KpiAchievementRowDto>(
            [.. page.Rows.Select(row => new KpiAchievementRowDto(
                row.TemplateId,
                row.KpiCode,
                row.KpiName,
                row.CategoryId,
                row.CategoryName,
                row.Assigned,
                row.Counted,
                KpiFigures.Average(row.Sum, row.Counted),
                row.Min,
                row.Max))],
            page.Total,
            page.PageIndex,
            page.PageSize));
    }
}

/// <summary>
/// KPI rows (target, actual, achievement, weight, weighted score) of the matching scorecards, every status. Sort: <c>periodStart</c>
/// (default: newest period first, then employee code and KPI order), <c>periodName</c>, <c>employeeCode</c>, <c>employeeName</c>,
/// <c>departmentName</c>, <c>kpiCode</c>, <c>kpiName</c>, <c>categoryName</c>, <c>achievementPct</c>, <c>weightedScore</c>, <c>status</c>.
/// </summary>
internal sealed class KpiTargetVsActualReportHandler(KpiReportSource source) : IKpiTargetVsActualReportHandler
{
    public static IReadOnlyDictionary<string, Expression<Func<KpiItemRow, object>>> SortKeys { get; } =
        new Dictionary<string, Expression<Func<KpiItemRow, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["periodStart"] = row => row.PeriodStartDate,
            ["periodName"] = row => row.PeriodName,
            ["employeeCode"] = row => row.EmployeeCode,
            ["employeeName"] = row => row.FirstName + " " + row.LastName,
            ["departmentName"] = row => row.DepartmentName,
            ["kpiCode"] = row => row.KpiCode,
            ["kpiName"] = row => row.KpiName,
            ["categoryName"] = row => row.CategoryName,
            ["achievementPct"] = row => row.AchievementPct ?? 0m,
            ["weightedScore"] = row => row.WeightedScore ?? 0m,
            ["status"] = row => row.Status,
        };

    public async Task<Result<PagedResult<KpiTargetVsActualRowDto>>> HandleAsync(PageRequest<KpiReportFilter> request, CancellationToken cancellationToken)
    {
        var sort = KpiReportSource.Sort(request.Sort, SortKeys);
        var filtered = source.Filtered(request.Filters, request.Search);
        if (sort.IsFailure || filtered.IsFailure)
        {
            return Result.Failure<PagedResult<KpiTargetVsActualRowDto>>(sort.IsFailure ? sort.Error : filtered.Error);
        }

        var page = await source.PageAsync(
            source.ItemRows(filtered.Value, request.Filters.CategoryId),
            rows => sort.Value is { } key
                ? (key.Descending ? rows.OrderByDescending(key.Key) : rows.OrderBy(key.Key)).ThenBy(row => row.ItemId)
                : DefaultOrder(rows),
            request.PageIndex,
            request.PageSize,
            cancellationToken);

        return Result.Success(new PagedResult<KpiTargetVsActualRowDto>([.. page.Rows.Select(row => row.ToDto())], page.Total, page.PageIndex, page.PageSize));
    }

    public static IOrderedQueryable<KpiItemRow> DefaultOrder(IQueryable<KpiItemRow> rows) =>
        rows.OrderByDescending(row => row.PeriodStartDate)
            .ThenBy(row => row.EmployeeCode)
            .ThenBy(row => row.ScorecardId)
            .ThenBy(row => row.SortOrder)
            .ThenBy(row => row.ItemId);
}
