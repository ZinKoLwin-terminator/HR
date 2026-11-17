using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Security;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Organization;
using Apex.Hrms.Domain.Performance;

namespace Apex.Hrms.Application.Performance.Insights;

/// <summary>The aggregates of one status over a set of scorecards, computed by the database.</summary>
internal sealed class KpiStatusGroup
{
    public ScorecardStatus Status { get; init; }

    public int Count { get; init; }

    /// <summary>How many name the caller as reviewer (manager dashboard only).</summary>
    public int Mine { get; init; }

    public decimal? ScoreSum { get; init; }

    public int Scored { get; init; }
}

internal static class KpiDashboardQueries
{
    public static readonly ScorecardStatus[] ManagerStage = [ScorecardStatus.Submitted, ScorecardStatus.Resubmitted, ScorecardStatus.UnderReview];

    /// <summary>Count, scored count and score sum per status (and, with <paramref name="reviewer"/>, how many name that reviewer): one query.</summary>
    public static IQueryable<KpiStatusGroup> ByStatus(IQueryable<Scorecard> scorecards, int reviewer) =>
        scorecards.GroupBy(scorecard => scorecard.Status).Select(group => new KpiStatusGroup
        {
            Status = group.Key,
            Count = group.Count(),
            Mine = group.Count(scorecard => scorecard.ReviewerEmployeeId == reviewer),
            ScoreSum = group.Sum(scorecard => scorecard.OverallScore),
            Scored = group.Count(scorecard => scorecard.OverallScore != null),
        });

    public static IReadOnlyDictionary<ScorecardStatus, int> Counts(IEnumerable<KpiStatusGroup> groups) =>
        groups.ToDictionary(group => group.Status, group => group.Count);

    /// <summary>The mean score of the Finalized scorecards (Rejected and unscored ones never count), 2 decimals; null when none.</summary>
    public static decimal? FinalizedAverage(IEnumerable<KpiStatusGroup> groups) =>
        groups.FirstOrDefault(group => group.Status == ScorecardStatus.Finalized) is { } finalized
            ? KpiFigures.Average(finalized.ScoreSum, finalized.Scored)
            : null;

    /// <summary>Highest score first, unscored last, then employee code.</summary>
    public static IOrderedQueryable<KpiScorecardRow> ByScore(IQueryable<KpiScorecardRow> rows) =>
        rows.OrderBy(row => row.OverallScore == null ? 1 : 0)
            .ThenByDescending(row => row.OverallScore)
            .ThenBy(row => row.EmployeeCode)
            .ThenBy(row => row.ScorecardId);
}

/// <summary>
/// The employee dashboard: the caller's own scorecards only (their employee id inside the scorecard scope), Planning excluded as in
/// My scorecards. Without <c>periodId</c> the period is the most recent Active one among the periods of the caller's scorecards, else
/// the most recent of them. A caller with no linked employee, or with no scorecard, gets an empty dashboard. At most four queries.
/// </summary>
internal sealed class KpiEmployeeDashboardHandler(KpiReportSource source, ICurrentUser user, IClock clock) : IKpiEmployeeDashboardHandler
{
    private sealed class CurrentRow
    {
        public int Id { get; init; }

        public ScorecardStatus Status { get; init; }

        public decimal? OverallScore { get; init; }

        public string? RatingLabel { get; init; }

        public int? ReviewerEmployeeId { get; init; }

        public string? ReviewerName { get; init; }

        public DateOnly? SubmittedOn { get; init; }

        public DateOnly? FinalizedOn { get; init; }

        public decimal? ManagerRating { get; init; }

        public string? ManagerComment { get; init; }
    }

    public async Task<Result<KpiEmployeeDashboardDto>> HandleAsync(int? periodId, CancellationToken cancellationToken)
    {
        var read = source.Read;
        var mine = user.EmployeeId is { } own
            ? source.Visible().Where(scorecard => scorecard.EmployeeId == own && scorecard.Status != ScorecardStatus.Planning)
            : source.Visible().Where(Predicates.Never<Scorecard>());

        var chosen = await source.PeriodAsync(periodId, source.PeriodsOf(mine), cancellationToken);
        if (chosen.IsFailure)
        {
            return Result.Failure<KpiEmployeeDashboardDto>(chosen.Error);
        }

        if (chosen.Value is not { } period)
        {
            return Result.Success(Empty(null));
        }

        var employees = read.Query<Employee>();
        var current = await read.FirstOrDefaultAsync(
            mine.Where(scorecard => scorecard.PeriodId == period.Id).Select(scorecard => new CurrentRow
            {
                Id = scorecard.Id,
                Status = scorecard.Status,
                OverallScore = scorecard.OverallScore,
                RatingLabel = scorecard.RatingLabel,
                ReviewerEmployeeId = scorecard.ReviewerEmployeeId,
                ReviewerName = employees
                    .Where(employee => employee.Id == scorecard.ReviewerEmployeeId)
                    .Select(employee => employee.FirstName + " " + employee.LastName)
                    .FirstOrDefault(),
                SubmittedOn = scorecard.SubmittedOn,
                FinalizedOn = scorecard.FinalizedOn,
                ManagerRating = scorecard.ManagerRating,
                ManagerComment = scorecard.ManagerComment,
            }),
            cancellationToken);

        IReadOnlyList<KpiDashboardItemDto> items = [];
        if (current is not null)
        {
            var id = current.Id;
            items = await read.ToListAsync(
                read.Query<ScorecardItem>()
                    .Where(item => item.ScorecardId == id)
                    .OrderBy(item => item.SortOrder)
                    .ThenBy(item => item.Id)
                    .Select(item => new KpiDashboardItemDto(
                        item.Id,
                        item.KpiCode,
                        item.KpiName,
                        item.Unit,
                        item.Target,
                        item.ActualValue,
                        item.AchievementPct,
                        item.Weight,
                        item.WeightedScore)),
                cancellationToken);
        }

        var start = period.StartDate;
        var currentPeriod = period.Id;
        var earlier = await read.ToListAsync(
            source.Rows(mine.Where(scorecard => scorecard.PeriodId != currentPeriod))
                .Where(row => row.PeriodStartDate < start)
                .OrderByDescending(row => row.PeriodStartDate)
                .ThenByDescending(row => row.PeriodId)
                .Take(KpiFigures.HistoryLimit),
            cancellationToken);

        var pending = current is not null
            && current.Status is ScorecardStatus.Draft or ScorecardStatus.Returned
            && period.Status == EvaluationPeriodStatus.Active;
        var overdue = pending && period.SubmissionDueDate is { } due && clock.Today > due;

        return Result.Success(new KpiEmployeeDashboardDto(
            period.ToDto(),
            current is null
                ? null
                : new KpiCurrentScorecardDto(
                    current.Id,
                    ScorecardStatusText.ToText(current.Status),
                    current.OverallScore,
                    current.RatingLabel,
                    current.ReviewerEmployeeId,
                    current.ReviewerName,
                    current.SubmittedOn,
                    current.FinalizedOn),
            items,
            new KpiPendingSubmissionDto(pending, period.SubmissionDueDate, overdue),
            current is null ? null : new KpiManagerFeedbackDto(current.ManagerRating, current.ManagerComment),
            [.. earlier.Select(row => new KpiHistoryEntryDto(
                row.ScorecardId,
                row.PeriodId,
                row.PeriodName,
                KpiFrequencyText.ToText(row.PeriodType),
                row.PeriodStartDate,
                row.PeriodEndDate,
                ScorecardStatusText.ToText(row.Status),
                row.OverallScore,
                row.RatingLabel))]));
    }

    private static KpiEmployeeDashboardDto Empty(KpiPeriodRefDto? period) =>
        new(period, null, [], new KpiPendingSubmissionDto(false, null, false), null, []);
}

/// <summary>
/// The manager dashboard: the scorecards of the caller's direct reports and those naming the caller as reviewer, never the caller's
/// own, inside the scorecard scope. Without <c>periodId</c> the period is chosen among the periods of that set. Three queries.
/// </summary>
internal sealed class KpiManagerDashboardHandler(KpiReportSource source, ICurrentUser user, ICallerTeam team) : IKpiManagerDashboardHandler
{
    public async Task<Result<KpiManagerDashboardDto>> HandleAsync(int? periodId, CancellationToken cancellationToken)
    {
        var read = source.Read;
        var me = user.EmployeeId ?? 0;
        int[] reports = [.. team.DirectReportIds];
        var set = user.EmployeeId is null
            ? source.Visible().Where(Predicates.Never<Scorecard>())
            : source.Visible().Where(scorecard =>
                scorecard.EmployeeId != me && (reports.Contains(scorecard.EmployeeId) || scorecard.ReviewerEmployeeId == me));

        var chosen = await source.PeriodAsync(periodId, source.PeriodsOf(set), cancellationToken);
        if (chosen.IsFailure)
        {
            return Result.Failure<KpiManagerDashboardDto>(chosen.Error);
        }

        if (chosen.Value is not { } period)
        {
            return Result.Success(new KpiManagerDashboardDto(null, 0, KpiFigures.ByStatus(new Dictionary<ScorecardStatus, int>()), 0, 0, null, []));
        }

        var id = period.Id;
        var inPeriod = set.Where(scorecard => scorecard.PeriodId == id);
        var groups = await read.ToListAsync(KpiDashboardQueries.ByStatus(inPeriod, me), cancellationToken);
        var rows = await read.ToListAsync(KpiDashboardQueries.ByScore(source.Rows(inPeriod)), cancellationToken);

        return Result.Success(new KpiManagerDashboardDto(
            period.ToDto(),
            groups.Sum(group => group.Count),
            KpiFigures.ByStatus(KpiDashboardQueries.Counts(groups)),
            groups.Where(group => KpiDashboardQueries.ManagerStage.Contains(group.Status)).Sum(group => group.Mine),
            groups.Where(group => group.Status is ScorecardStatus.Approved or ScorecardStatus.Finalized).Sum(group => group.Count),
            KpiDashboardQueries.FinalizedAverage(groups),
            [.. rows.Select(row => row.ToScore())]));
    }
}

/// <summary>
/// The HR dashboard (HR tier only: <c>kpi:Approve</c> with <c>employees:Edit</c>; anyone else 403): every scorecard of the period,
/// optionally of one company. Without <c>periodId</c> the period is the most recent Active period that is not Draft (of the company when
/// given), else the most recent such period. Ten queries, whatever the number of scorecards.
/// </summary>
internal sealed class KpiHrDashboardHandler(KpiReportSource source, SelfService self) : IKpiHrDashboardHandler
{
    public const string HrOnlyMessage = "The HR dashboard needs HR rights.";

    public async Task<Result<KpiHrDashboardDto>> HandleAsync(int? periodId, int? companyId, CancellationToken cancellationToken)
    {
        if (self.TierOf("kpi") != AccessTier.All)
        {
            return Result.Failure<KpiHrDashboardDto>(AppError.Forbidden(HrOnlyMessage));
        }

        var read = source.Read;
        var draft = EvaluationPeriodStatus.Draft;
        var candidates = read.Query<EvaluationPeriod>().Where(period => period.Status != draft);
        var visible = source.Visible();
        if (companyId is { } company)
        {
            candidates = candidates.Where(period => period.CompanyId == company);
            visible = visible.Where(scorecard => scorecard.CompanyId == company);
        }

        var chosen = await source.PeriodAsync(periodId, candidates, cancellationToken);
        if (chosen.IsFailure)
        {
            return Result.Failure<KpiHrDashboardDto>(chosen.Error);
        }

        var active = RecordStatus.Active;
        var bands = await read.ToListAsync(
            read.Query<RatingBand>().Where(band => band.Status == active).Select(band => new { band.Label, band.MinScore }),
            cancellationToken);
        var threshold = KpiFigures.AttentionThreshold(bands.Select(band => band.MinScore));

        if (chosen.Value is not { } period)
        {
            return Result.Success(new KpiHrDashboardDto(
                null, companyId, 0, 0, 0, 0, 0, null, 0m, KpiFigures.ByStatus(new Dictionary<ScorecardStatus, int>()), [], [], threshold, 0, [], []));
        }

        var id = period.Id;
        var finalized = ScorecardStatus.Finalized;
        var inPeriod = visible.Where(scorecard => scorecard.PeriodId == id);
        var items = read.Query<ScorecardItem>();
        var cardIds = inPeriod.Select(scorecard => scorecard.Id);

        var groups = await read.ToListAsync(KpiDashboardQueries.ByStatus(inPeriod, 0), cancellationToken);
        var withKpis = await read.CountAsync(inPeriod.Where(scorecard => items.Any(item => item.ScorecardId == scorecard.Id)), cancellationToken);
        var assignments = await read.CountAsync(items.Where(item => cardIds.Contains(item.ScorecardId)), cancellationToken);

        var perDepartment = inPeriod.GroupBy(scorecard => scorecard.DepartmentId).Select(group => new
        {
            DepartmentId = group.Key,
            Scorecards = group.Count(),
            Finalized = group.Count(scorecard => scorecard.Status == finalized),
            ScoreSum = group.Sum(scorecard => scorecard.Status == finalized ? scorecard.OverallScore : null),
            Scored = group.Count(scorecard => scorecard.Status == finalized && scorecard.OverallScore != null),
        });
        var departmentGroups =
            from grouped in perDepartment
            join department in read.Query<Department>() on grouped.DepartmentId equals department.Id
            orderby department.Name, department.Id
            select new { grouped.DepartmentId, department.Name, grouped.Scorecards, grouped.Finalized, grouped.ScoreSum, grouped.Scored };
        var departments = await read.ToListAsync(departmentGroups, cancellationToken);

        var scored = inPeriod.Where(scorecard => scorecard.Status == finalized && scorecard.OverallScore != null);
        var top = await read.ToListAsync(KpiDashboardQueries.ByScore(source.Rows(scored)).Take(KpiFigures.TopPerformerCount), cancellationToken);

        var low = scored.Where(scorecard => scorecard.OverallScore < threshold);
        var attentionCount = await read.CountAsync(low, cancellationToken);
        var attention = await read.ToListAsync(
            source.Rows(low).OrderBy(row => row.OverallScore).ThenBy(row => row.EmployeeCode).ThenBy(row => row.ScorecardId).Take(KpiFigures.AttentionListLimit),
            cancellationToken);

        var labels = await read.ToListAsync(
            inPeriod.Where(scorecard => scorecard.Status == finalized)
                .GroupBy(scorecard => scorecard.RatingLabel)
                .Select(group => new { Label = group.Key, Count = group.Count() }),
            cancellationToken);
        var bandOrder = bands.ToDictionary(band => band.Label, band => band.MinScore, StringComparer.Ordinal);

        var counts = KpiDashboardQueries.Counts(groups);
        int CountOf(params ScorecardStatus[] statuses) => statuses.Sum(status => counts.GetValueOrDefault(status));
        var evaluated = groups.Sum(group => group.Count) - CountOf(ScorecardStatus.Planning);

        return Result.Success(new KpiHrDashboardDto(
            period.ToDto(),
            companyId,
            withKpis,
            assignments,
            CountOf(ScorecardStatus.Finalized),
            CountOf(ScorecardStatus.Draft, ScorecardStatus.Returned),
            CountOf(ScorecardStatus.Submitted, ScorecardStatus.Resubmitted, ScorecardStatus.UnderReview, ScorecardStatus.Approved),
            KpiDashboardQueries.FinalizedAverage(groups),
            KpiFigures.CompletionRate(CountOf(ScorecardStatus.Finalized), evaluated),
            KpiFigures.ByStatus(counts),
            [.. departments.Select(row => new KpiDepartmentPerformanceDto(row.DepartmentId, row.Name, row.Scorecards, row.Finalized, KpiFigures.Average(row.ScoreSum, row.Scored)))],
            [.. top.Select(row => row.ToScore())],
            threshold,
            attentionCount,
            [.. attention.Select(row => row.ToScore())],
            [.. labels
                .OrderBy(label => label.Label is not null && bandOrder.ContainsKey(label.Label) ? 0 : 1)
                .ThenByDescending(label => label.Label is not null && bandOrder.TryGetValue(label.Label, out var min) ? min : 0m)
                .ThenBy(label => label.Label, StringComparer.Ordinal)
                .Select(label => new KpiRatingCountDto(label.Label, label.Count))]));
    }
}
