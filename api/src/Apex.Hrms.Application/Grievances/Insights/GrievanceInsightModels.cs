using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Grievances.Insights;

// The dashboards and reports of the grievance module, typed after ui/src/app/core/models/grievance-insight.models.ts. Every figure is
// worked out here (GrievanceFigures); the screens only display it. Metric definitions:
//  - counted: every case that is not a Draft, archived ones included (lists hide archived cases, aggregates do not);
//  - overdue: open and the target date is before today (business time zone);
//  - resolution rate: (Resolved + Closed) / (counted - Rejected) x 100, 2 decimals, half away from zero; 0 when the divisor is 0;
//  - resolution days: calendar days (business time zone) from the submission date to the resolution date; the average is over the
//    cases now Resolved or Closed, 1 decimal, null when there is none.
// Aggregates are the same for every viewer of a tier: no case is left out because the caller submitted it or is its subject
// (otherwise two HR users' totals would differ by exactly those cases). No aggregate names a person or carries a subject, a
// description or a note; recent activity carries case numbers only; departments with fewer than 3 cases are merged.
// Report and export ROWS are case-level detail: they leave out the cases whose subject employee is the caller (the case-list rule) and
// keep the cases the caller submitted (rows never show a submitter). See GrievanceInsights.ReportCases.

public sealed record GrievanceStatusCountDto(string Status, int Count);

public sealed record GrievancePriorityCountDto(string? Priority, int Count);

public sealed record GrievanceCategoryCountDto(int CategoryId, string CategoryName, int Count);

/// <summary><c>bucket</c> is Department, NotDisclosed or Suppressed; the merged groups have no department id.</summary>
public sealed record GrievanceDepartmentCountDto(string Bucket, int? DepartmentId, string DepartmentName, int Count);

/// <summary>One calendar month (<c>yyyy-MM</c>): cases submitted in it, and cases now Resolved or Closed resolved in it.</summary>
public sealed record GrievanceMonthlyCountDto(string Month, int Submitted, int Resolved);

/// <summary>One history step, without actor, reason or subject.</summary>
public sealed record GrievanceRecentActivityDto(int CaseId, string CaseNo, string Action, string ToStatus, DateTime OccurredAtUtc);

/// <summary><c>GET /api/grievances/dashboard/hr</c> (<c>grievance-management:View</c>).</summary>
public sealed record GrievanceHrDashboardDto(
    DateOnly AsOfDate,
    int Total,
    int New,
    int UnderReview,
    int UnderInvestigation,
    int AwaitingEmployeeResponse,
    int PendingResolution,
    int Resolved,
    int Closed,
    int Rejected,
    int Overdue,
    int Escalated,
    decimal ResolutionRatePct,
    decimal? AverageResolutionDays,
    IReadOnlyList<GrievanceStatusCountDto> ByStatus,
    IReadOnlyList<GrievancePriorityCountDto> ByPriority,
    IReadOnlyList<GrievanceCategoryCountDto> ByCategory,
    IReadOnlyList<GrievanceDepartmentCountDto> ByDepartment,
    IReadOnlyList<GrievanceMonthlyCountDto> MonthlyTrend,
    IReadOnlyList<GrievanceRecentActivityDto> RecentActivity);

public sealed record GrievanceDecisionCountDto(int Approved, int Rejected);

/// <summary>
/// <c>GET /api/grievances/dashboard/review</c> (<c>grievance-review:View</c>): aggregates over the review queue (open escalated cases and
/// cases with a pending proposal). <c>decidedByMe</c> is the only per-caller figure.
/// </summary>
public sealed record GrievanceReviewDashboardDto(
    DateOnly AsOfDate,
    int PendingProposals,
    int Escalated,
    int ImmediateEscalations,
    int Overdue,
    int? OldestPendingDays,
    GrievanceDecisionCountDto DecidedByMe,
    IReadOnlyList<GrievancePriorityCountDto> ByPriority,
    IReadOnlyList<GrievanceCategoryCountDto> ByCategory);

/// <summary>The filters of the case report, combined with AND (query string names as in the UI).</summary>
public sealed record GrievanceReportFilter(
    DateOnly? SubmittedFrom = null,
    DateOnly? SubmittedTo = null,
    int? DepartmentId = null,
    int? CategoryId = null,
    string? Status = null,
    string? Priority = null,
    int? InvestigatorEmployeeId = null,
    int? MinResolutionDays = null,
    int? MaxResolutionDays = null,
    bool? Overdue = null);

/// <summary>One case of the report and of the <c>grievance-cases</c> export. Never the subject, description, notes or submitter.</summary>
public sealed record GrievanceReportRowDto(
    int CaseId,
    string CaseNo,
    string CategoryName,
    string Status,
    string? Priority,
    int? DepartmentId,
    string? DepartmentName,
    DateOnly SubmittedOn,
    DateOnly? TargetResolutionDate,
    DateOnly? ResolvedOn,
    int? ResolutionDays,
    int AgeDays,
    bool IsOverdue,
    string? LeadInvestigatorName,
    bool IsEscalated);

/// <summary>The filters of the summary. No department filter: it would undo the small-group suppression for reviewers.</summary>
public sealed record GrievanceSummaryFilter(
    DateOnly? SubmittedFrom = null,
    DateOnly? SubmittedTo = null,
    int? CategoryId = null,
    string? Priority = null);

/// <summary><c>GET /api/grievances/reports/summary</c> (<c>grievance-management:View</c> or <c>grievance-review:View</c>).</summary>
public sealed record GrievanceSummaryDto(
    DateOnly AsOfDate,
    int Total,
    int Open,
    int Resolved,
    int Closed,
    int Rejected,
    int Overdue,
    int Escalated,
    decimal ResolutionRatePct,
    decimal? AverageResolutionDays,
    IReadOnlyList<GrievanceStatusCountDto> ByStatus,
    IReadOnlyList<GrievancePriorityCountDto> ByPriority,
    IReadOnlyList<GrievanceCategoryCountDto> ByCategory,
    IReadOnlyList<GrievanceDepartmentCountDto> ByDepartment);

/// <summary>The grievance dashboards and reports (read only).</summary>
public interface IGrievanceInsights
{
    Task<Result<GrievanceHrDashboardDto>> HrDashboardAsync(CancellationToken cancellationToken);

    Task<Result<GrievanceReviewDashboardDto>> ReviewDashboardAsync(CancellationToken cancellationToken);

    Task<Result<PagedResult<GrievanceReportRowDto>>> CasesReportAsync(PageRequest<GrievanceReportFilter> request, CancellationToken cancellationToken);

    Task<Result<GrievanceSummaryDto>> SummaryAsync(GrievanceSummaryFilter filter, CancellationToken cancellationToken);
}
