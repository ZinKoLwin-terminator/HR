using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Performance.Insights;

// The read-only KPI dashboards and reports (GET /api/kpi/dashboard/* and /api/kpi/reports/*). Every figure is computed by the
// database over the scorecards the caller may see (ScorecardScope); only employee code and name identify a person, and no
// comment (employee, manager or HR) or evidence file appears anywhere except the employee's own manager feedback.

/// <summary>The evaluation period a dashboard or report is about. <c>periodType</c> is Monthly, Quarterly, Half-Yearly, Yearly or Custom.</summary>
public sealed record KpiPeriodRefDto(
    int Id,
    string Name,
    string PeriodType,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status,
    DateOnly? SubmissionDueDate);

/// <summary>How many scorecards have one status. Dashboards list every status, in workflow order, with zero counts included.</summary>
public sealed record KpiStatusCountDto(string Status, int Count);

/// <summary>One employee's scorecard in a comparison, a top list or the attention list.</summary>
public sealed record KpiEmployeeScoreDto(
    int ScorecardId,
    int EmployeeId,
    string EmployeeCode,
    string EmployeeName,
    int DepartmentId,
    string DepartmentName,
    string Status,
    decimal? OverallScore,
    string? RatingLabel);

// ---- Employee dashboard ----

/// <summary>
/// <c>GET /api/kpi/dashboard/employee</c>: the caller's own scorecard of the period and their history. Every part is empty (null
/// or <c>[]</c>) when the caller has no linked employee or no scorecard (scorecards still in Planning are not shown, as in My scorecards).
/// </summary>
public sealed record KpiEmployeeDashboardDto(
    KpiPeriodRefDto? Period,
    KpiCurrentScorecardDto? Scorecard,
    IReadOnlyList<KpiDashboardItemDto> Items,
    KpiPendingSubmissionDto Pending,
    KpiManagerFeedbackDto? Feedback,
    IReadOnlyList<KpiHistoryEntryDto> History);

public sealed record KpiCurrentScorecardDto(
    int Id,
    string Status,
    decimal? OverallScore,
    string? RatingLabel,
    int? ReviewerEmployeeId,
    string? ReviewerName,
    DateOnly? SubmittedOn,
    DateOnly? FinalizedOn);

/// <summary>One KPI of the current scorecard, by sort order.</summary>
public sealed record KpiDashboardItemDto(
    int Id,
    string KpiCode,
    string KpiName,
    string? Unit,
    decimal Target,
    decimal? ActualValue,
    decimal? AchievementPct,
    decimal Weight,
    decimal? WeightedScore);

/// <summary>
/// <c>isPending</c>: the scorecard is Draft or Returned and its period is Active (the employee still has to submit). <c>dueDate</c> is
/// the period's submission due date; <c>isOverdue</c> is true when it is pending and today (business time zone) is after the due date.
/// </summary>
public sealed record KpiPendingSubmissionDto(bool IsPending, DateOnly? DueDate, bool IsOverdue);

/// <summary>The manager's overall rating (1 to 5) and comment on the current scorecard, as stored.</summary>
public sealed record KpiManagerFeedbackDto(decimal? ManagerRating, string? ManagerComment);

/// <summary>A scorecard of an earlier period (the period starts before the current one), newest first, at most 12.</summary>
public sealed record KpiHistoryEntryDto(
    int ScorecardId,
    int PeriodId,
    string PeriodName,
    string PeriodType,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status,
    decimal? OverallScore,
    string? RatingLabel);

// ---- Manager dashboard ----

/// <summary>
/// <c>GET /api/kpi/dashboard/manager</c>: the scorecards of the period of the caller's direct reports and those naming the caller as
/// reviewer, never the caller's own. <c>teamSize</c> is the number of those scorecards (one per employee and period);
/// <c>pendingReviews</c> the ones in the manager stage (Submitted, Resubmitted, Under Review) naming the caller as reviewer;
/// <c>approvedOrFinalized</c> the Approved and Finalized ones; <c>averageScore</c> the mean overall score of the Finalized ones
/// (2 decimals, null when none). <c>employees</c> is ordered by score (highest first, unscored last), then employee code.
/// </summary>
public sealed record KpiManagerDashboardDto(
    KpiPeriodRefDto? Period,
    int TeamSize,
    IReadOnlyList<KpiStatusCountDto> ByStatus,
    int PendingReviews,
    int ApprovedOrFinalized,
    decimal? AverageScore,
    IReadOnlyList<KpiEmployeeScoreDto> Employees);

// ---- HR dashboard ----

/// <summary>
/// <c>GET /api/kpi/dashboard/hr</c> (HR tier only): every scorecard of the period, optionally of one company. See the README section
/// "HTTP contract (KPI dashboards, reports and export)" for the exact definition of each figure.
/// </summary>
public sealed record KpiHrDashboardDto(
    KpiPeriodRefDto? Period,
    int? CompanyId,
    int EmployeesWithKpis,
    int TotalAssignments,
    int CompletedEvaluations,
    int PendingSubmissions,
    int PendingApprovals,
    decimal? AverageScore,
    decimal CompletionRatePct,
    IReadOnlyList<KpiStatusCountDto> ByStatus,
    IReadOnlyList<KpiDepartmentPerformanceDto> Departments,
    IReadOnlyList<KpiEmployeeScoreDto> TopPerformers,
    decimal AttentionThreshold,
    int RequiringAttentionCount,
    IReadOnlyList<KpiEmployeeScoreDto> RequiringAttention,
    IReadOnlyList<KpiRatingCountDto> RatingDistribution);

/// <summary>Per department copied onto the scorecards: how many, how many Finalized, and their mean score (Finalized only).</summary>
public sealed record KpiDepartmentPerformanceDto(int DepartmentId, string DepartmentName, int Scorecards, int Finalized, decimal? AverageScore);

/// <summary>Finalized scorecards per rating label; <c>ratingLabel</c> is null for a score below every rating band.</summary>
public sealed record KpiRatingCountDto(string? RatingLabel, int Count);

// ---- Reports ----

/// <summary>
/// Query-string filters shared by the reports and the KPI Excel exports. <c>status</c> is a scorecard status text; <c>periodType</c>
/// is Monthly, Quarterly, Half-Yearly, Yearly (or Annual) or Custom; <c>year</c> is the year the period starts in; <c>categoryId</c>
/// keeps scorecards with at least one KPI of the category (and, in the item reports, only those KPIs). A report with a route id
/// (employee, department) ignores the matching filter.
/// </summary>
public sealed record KpiReportFilter(
    int? CompanyId = null,
    int? BranchId = null,
    int? DepartmentId = null,
    int? PositionId = null,
    int? EmployeeId = null,
    int? CategoryId = null,
    int? PeriodId = null,
    string? Status = null,
    string? PeriodType = null,
    int? Year = null);

/// <summary>
/// One period of <c>GET /api/kpi/reports/summary</c>. <c>scorecards</c> counts every scorecard (Planning included); the score figures
/// count Finalized scorecards only; <c>completionRatePct</c> is Finalized ÷ (scorecards − inPlanning) × 100, 2 decimals, 0 when nothing
/// is past Planning.
/// </summary>
public sealed record KpiPeriodSummaryDto(
    int PeriodId,
    string PeriodName,
    string PeriodType,
    DateOnly StartDate,
    DateOnly EndDate,
    string PeriodStatus,
    int Scorecards,
    int InPlanning,
    int Finalized,
    int Rejected,
    decimal? AverageScore,
    decimal? MinScore,
    decimal? MaxScore,
    decimal CompletionRatePct);

/// <summary>One KPI of a scorecard in a report. Never a comment.</summary>
public sealed record KpiReportItemDto(
    int Id,
    string KpiCode,
    string KpiName,
    string CategoryName,
    string? Unit,
    decimal Target,
    decimal? ActualValue,
    decimal? AchievementPct,
    decimal Weight,
    decimal? WeightedScore);

/// <summary>One scorecard of the employee report, with its KPIs.</summary>
public sealed record KpiEmployeeScorecardDto(
    int ScorecardId,
    int PeriodId,
    string PeriodName,
    string PeriodType,
    DateOnly StartDate,
    DateOnly EndDate,
    int DepartmentId,
    string DepartmentName,
    string Status,
    decimal? OverallScore,
    string? RatingLabel,
    IReadOnlyList<KpiReportItemDto> Items);

/// <summary>
/// <c>GET /api/kpi/reports/employee/{employeeId}</c>: the employee and one page of their scorecards (newest period first by default).
/// <c>finalized</c> and <c>averageScore</c> are over every scorecard that matches the filters, not only the page.
/// </summary>
public sealed record KpiEmployeeReportDto(
    int EmployeeId,
    string EmployeeCode,
    string EmployeeName,
    int Finalized,
    decimal? AverageScore,
    PagedResult<KpiEmployeeScorecardDto> Scorecards);

/// <summary>A scorecard row of the department report.</summary>
public sealed record KpiReportScorecardRowDto(
    int ScorecardId,
    int EmployeeId,
    string EmployeeCode,
    string EmployeeName,
    int DepartmentId,
    string DepartmentName,
    int PeriodId,
    string PeriodName,
    string Status,
    decimal? OverallScore,
    string? RatingLabel);

/// <summary>
/// <c>GET /api/kpi/reports/department/{departmentId}</c>: the scorecards whose copied department is this one (so an employee who moved
/// later still counts here), for one period. The totals are over every matching scorecard, not only the page.
/// </summary>
public sealed record KpiDepartmentReportDto(
    int DepartmentId,
    string DepartmentName,
    KpiPeriodRefDto? Period,
    int Scorecards,
    int Finalized,
    decimal? AverageScore,
    PagedResult<KpiReportScorecardRowDto> Rows);

/// <summary>One row of <c>GET /api/kpi/reports/ranking</c>: Finalized scorecards only. Equal scores share a rank (1, 2, 2, 4).</summary>
public sealed record KpiRankingRowDto(
    int Rank,
    int ScorecardId,
    int EmployeeId,
    string EmployeeCode,
    string EmployeeName,
    int DepartmentId,
    string DepartmentName,
    int PeriodId,
    string PeriodName,
    decimal OverallScore,
    string? RatingLabel);

/// <summary>
/// One template of <c>GET /api/kpi/reports/kpi-achievement</c>: <c>assigned</c> counts the KPIs assigned from it in the matching
/// scorecards; <c>counted</c> those of Finalized scorecards with an achievement, which the average, minimum and maximum are over.
/// </summary>
public sealed record KpiAchievementRowDto(
    int TemplateId,
    string KpiCode,
    string KpiName,
    int CategoryId,
    string CategoryName,
    int Assigned,
    int Counted,
    decimal? AverageAchievementPct,
    decimal? MinAchievementPct,
    decimal? MaxAchievementPct);

/// <summary>One KPI row of <c>GET /api/kpi/reports/target-vs-actual</c>, whatever the scorecard's status (shown).</summary>
public sealed record KpiTargetVsActualRowDto(
    int ItemId,
    int ScorecardId,
    int EmployeeId,
    string EmployeeCode,
    string EmployeeName,
    int DepartmentId,
    string DepartmentName,
    int PeriodId,
    string PeriodName,
    string Status,
    string KpiCode,
    string KpiName,
    string CategoryName,
    string? Unit,
    decimal Target,
    decimal? ActualValue,
    decimal? AchievementPct,
    decimal Weight,
    decimal? WeightedScore);

// ---- Handlers ----

public interface IKpiEmployeeDashboardHandler
{
    Task<Result<KpiEmployeeDashboardDto>> HandleAsync(int? periodId, CancellationToken cancellationToken);
}

public interface IKpiManagerDashboardHandler
{
    Task<Result<KpiManagerDashboardDto>> HandleAsync(int? periodId, CancellationToken cancellationToken);
}

public interface IKpiHrDashboardHandler
{
    Task<Result<KpiHrDashboardDto>> HandleAsync(int? periodId, int? companyId, CancellationToken cancellationToken);
}

public interface IKpiSummaryReportHandler
{
    Task<Result<PagedResult<KpiPeriodSummaryDto>>> HandleAsync(PageRequest<KpiReportFilter> request, CancellationToken cancellationToken);
}

public interface IKpiEmployeeReportHandler
{
    Task<Result<KpiEmployeeReportDto>> HandleAsync(int employeeId, PageRequest<KpiReportFilter> request, CancellationToken cancellationToken);
}

public interface IKpiDepartmentReportHandler
{
    Task<Result<KpiDepartmentReportDto>> HandleAsync(int departmentId, PageRequest<KpiReportFilter> request, CancellationToken cancellationToken);
}

public interface IKpiRankingReportHandler
{
    Task<Result<PagedResult<KpiRankingRowDto>>> HandleAsync(PageRequest<KpiReportFilter> request, CancellationToken cancellationToken);
}

public interface IKpiAchievementReportHandler
{
    Task<Result<PagedResult<KpiAchievementRowDto>>> HandleAsync(PageRequest<KpiReportFilter> request, CancellationToken cancellationToken);
}

public interface IKpiTargetVsActualReportHandler
{
    Task<Result<PagedResult<KpiTargetVsActualRowDto>>> HandleAsync(PageRequest<KpiReportFilter> request, CancellationToken cancellationToken);
}
