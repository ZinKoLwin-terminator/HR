using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Performance.Scorecards;

/// <summary>
/// One scorecard in a list (<c>GET /api/kpi/scorecards/query</c>): the employee's KPI plan and result for one evaluation period.
/// Only the employee code and name identify the person. <c>status</c> is Planning, Draft, Submitted, Under Review, Returned,
/// Resubmitted, Approved, Rejected or Finalized; <c>totalWeight</c> and <c>itemCount</c> describe the assigned KPIs;
/// <c>overallScore</c> and <c>ratingLabel</c> are set by the API once a scorecard is scored. <c>submissionDueDate</c> is the period's
/// (null when the period has none), so a list can flag a late submission without reading each scorecard.
/// </summary>
public sealed record ScorecardDto(
    int Id,
    int EmployeeId,
    string EmployeeCode,
    string EmployeeName,
    int PeriodId,
    string PeriodName,
    int DepartmentId,
    string DepartmentName,
    string Status,
    int ItemCount,
    decimal TotalWeight,
    decimal? OverallScore,
    string? RatingLabel,
    int? ReviewerEmployeeId,
    string? ReviewerName,
    DateOnly? PlanFinalizedOn,
    DateOnly? SubmittedOn,
    DateOnly? FinalizedOn,
    DateOnly? SubmissionDueDate,
    byte[] RowVersion) : IIdentified;

/// <summary>
/// A scorecard with its assigned KPIs (by sort order) and its review history (oldest first). Besides the list fields it carries the
/// comments of the three steps, the period's status, submission due date and whether it needs an HR review (so a screen can tell a
/// late submission and which buttons to offer).
/// </summary>
public sealed record ScorecardDetailDto(
    int Id,
    int EmployeeId,
    string EmployeeCode,
    string EmployeeName,
    int PeriodId,
    string PeriodName,
    int DepartmentId,
    string DepartmentName,
    string Status,
    int ItemCount,
    decimal TotalWeight,
    decimal? OverallScore,
    string? RatingLabel,
    int? ReviewerEmployeeId,
    string? ReviewerName,
    DateOnly? PlanFinalizedOn,
    DateOnly? SubmittedOn,
    DateOnly? FinalizedOn,
    DateOnly? ReviewedOn,
    string PeriodStatus,
    DateOnly? SubmissionDueDate,
    bool RequiresHrReview,
    string? EmployeeComment,
    decimal? ManagerRating,
    string? ManagerComment,
    string? HrComment,
    byte[] RowVersion,
    IReadOnlyList<ScorecardItemDto> Items,
    IReadOnlyList<ReviewActionDto> History) : IIdentified;

/// <summary>
/// One assigned KPI. Everything from <c>kpiCode</c> to <c>maxAchievementPct</c> is the snapshot copied from the template when it was
/// assigned (later template edits never change it); the target, weight, dates and sort order are what HR planned.
/// <c>attachments</c> lists the evidence files of the KPI (at most 5, oldest first); they are only filled in the scorecard detail.
/// </summary>
public sealed record ScorecardItemDto(
    int Id,
    int TemplateId,
    int CategoryId,
    string CategoryName,
    string KpiCode,
    string KpiName,
    string MeasurementType,
    string? Unit,
    string Direction,
    string ScoringMethod,
    decimal Target,
    decimal? RangeMin,
    decimal? RangeMax,
    decimal? MaxRating,
    decimal Weight,
    decimal? MaxAchievementPct,
    DateOnly StartDate,
    DateOnly EndDate,
    int SortOrder,
    decimal? ActualValue,
    string? EmployeeComment,
    decimal? AchievementPct,
    decimal? WeightedScore,
    decimal? ManagerRating,
    string? ManagerComment,
    byte[] RowVersion,
    IReadOnlyList<AttachmentDto> Attachments) : IIdentified;

/// <summary>
/// One step of the approval history. <c>step</c> is Employee, Manager or HR; <c>action</c> is the name of the step ("Finalize Plan").
/// <c>actorUserId</c> is the account that acted (null when the step was recorded without one), so a screen can recognise the
/// caller's own steps by id rather than by display name. The server enforces the separation of duties by id itself.
/// </summary>
public sealed record ReviewActionDto(
    string Step,
    string Action,
    string FromStatus,
    string ToStatus,
    int? ActorUserId,
    string ActorName,
    string? Comment,
    decimal? OverallScore,
    DateTime OccurredAtUtc);

/// <summary>Query-string filters of the scorecard list. <c>search</c> (the paging part) matches the employee code or name.</summary>
public sealed record ScorecardFilter(
    int? PeriodId = null,
    int? EmployeeId = null,
    int? DepartmentId = null,
    int? CompanyId = null,
    string? Status = null);

// ---- Assigning KPIs ----

/// <summary>
/// One KPI to assign. <c>target</c> and <c>weight</c> default to the template's; <c>startDate</c> and <c>endDate</c> default to the
/// period's and must lie within it; <c>sortOrder</c> defaults to the next free position.
/// </summary>
public sealed record AssignmentItemRequest(
    int TemplateId,
    decimal? Target,
    decimal? Weight,
    DateOnly? StartDate,
    DateOnly? EndDate,
    int? SortOrder);

internal interface IAssignmentItems
{
    IReadOnlyList<AssignmentItemRequest>? Items { get; }
}

/// <summary>POST body of <c>/api/kpi/assignments</c>: one to 50 KPIs for one employee in one period.</summary>
public sealed record AssignKpiRequest(int EmployeeId, int PeriodId, IReadOnlyList<AssignmentItemRequest>? Items) : IAssignmentItems;

/// <summary>
/// POST body of <c>/api/kpi/assignments/bulk</c>: the same KPIs for many employees. Choose the employees either with an explicit
/// <c>employeeIds</c> list (1 to 500) or with one or more org filters (combined with AND), never both.
/// </summary>
public sealed record BulkAssignKpiRequest(
    int PeriodId,
    IReadOnlyList<int>? EmployeeIds,
    int? DepartmentId,
    int? BranchId,
    int? PositionId,
    int? JobGradeId,
    IReadOnlyList<AssignmentItemRequest>? Items) : IAssignmentItems;

/// <summary>The outcome of an assignment: the scorecard, and whether this call created it (the HTTP layer answers 201 then, otherwise 200).</summary>
public sealed record AssignmentResult(ScorecardDetailDto Scorecard, bool Created);

/// <summary>An employee the bulk assignment reached, and how many KPIs it added to their scorecard.</summary>
public sealed record AssignedEmployeeDto(int EmployeeId, string EmployeeCode, int ScorecardId, int ItemsAdded);

/// <summary>An employee the bulk assignment left out, and why. <c>employeeCode</c> is null for an id that matches no employee.</summary>
public sealed record SkippedEmployeeDto(int EmployeeId, string? EmployeeCode, string Reason);

public sealed record BulkAssignmentResult(IReadOnlyList<AssignedEmployeeDto> Assigned, IReadOnlyList<SkippedEmployeeDto> Skipped);

public interface IAssignKpiHandler
{
    Task<Result<AssignmentResult>> HandleAsync(AssignKpiRequest request, CancellationToken cancellationToken);
}

public interface IBulkAssignKpiHandler
{
    Task<Result<BulkAssignmentResult>> HandleAsync(BulkAssignKpiRequest request, CancellationToken cancellationToken);
}

// ---- Changing the plan ----

/// <summary>
/// PUT body of <c>/api/kpi/scorecard-items/{id}</c>: what HR may change on an assigned KPI while the scorecard is in Planning. The
/// snapshot of the template (code, name, scoring rule) is not editable. <c>sortOrder</c> keeps its value when omitted.
/// </summary>
public sealed record ScorecardItemUpdateRequest(
    decimal? Target,
    decimal? Weight,
    DateOnly? StartDate,
    DateOnly? EndDate,
    int? SortOrder,
    string? RowVersion) : IVersionedRequest;

/// <summary>Finalizes the plan of a scorecard: Planning to Draft.</summary>
public sealed record FinalizePlanCommand(int Id, string? RowVersion) : IEntityCommand<FinalizePlanCommand>
{
    public static FinalizePlanCommand From(int id, string? rowVersion) => new(id, rowVersion);
}

// ---- Results, submission and review ----

/// <summary>
/// One KPI result in <c>PUT /api/kpi/scorecards/{id}/results</c>. <c>actualValue</c> null clears the result (and its achievement
/// and weighted score); <c>employeeComment</c> is at most 1000 characters.
/// </summary>
public sealed record ResultItemRequest(int Id, decimal? ActualValue, string? EmployeeComment);

/// <summary>
/// PUT body of <c>/api/kpi/scorecards/{id}/results</c>: the employee's draft results. Only the scorecard's own employee, while
/// it is Draft or Returned and its period is Active (or Completed, for a Returned scorecard). <c>items</c> lists the KPIs to change (each item of the scorecard at most
/// once; the others stay as they are). It saves a draft: the scorecard gets no score until it is submitted.
/// </summary>
public sealed record SaveScorecardResultsRequest(
    string? RowVersion,
    string? EmployeeComment,
    IReadOnlyList<ResultItemRequest>? Items) : IVersionedRequest;

/// <summary>One KPI rating in <c>PUT /api/kpi/scorecards/{id}/review</c>: <c>managerRating</c> 1 to 5 with at most 2 decimals, or null.</summary>
public sealed record ReviewItemRequest(int Id, decimal? ManagerRating, string? ManagerComment);

/// <summary>
/// PUT body of <c>/api/kpi/scorecards/{id}/review</c>: the reviewer's draft (overall rating and comment, and a rating and comment
/// per KPI) while the scorecard is Submitted, Resubmitted or Under Review. No status change and no history entry.
/// </summary>
public sealed record SaveScorecardReviewRequest(
    string? RowVersion,
    decimal? ManagerRating,
    string? ManagerComment,
    IReadOnlyList<ReviewItemRequest>? Items) : IVersionedRequest;

/// <summary>Draft or Returned to Submitted or Resubmitted (the employee).</summary>
public sealed record SubmitScorecardCommand(int Id, string? RowVersion) : IEntityCommand<SubmitScorecardCommand>
{
    public static SubmitScorecardCommand From(int id, string? rowVersion) => new(id, rowVersion);
}

/// <summary>Submitted or Resubmitted to Under Review (the reviewer).</summary>
public sealed record StartScorecardReviewCommand(int Id, string? RowVersion) : IEntityCommand<StartScorecardReviewCommand>
{
    public static StartScorecardReviewCommand From(int id, string? rowVersion) => new(id, rowVersion);
}

/// <summary>The reviewer approves. The comment is optional (at most 1000 characters).</summary>
public sealed record ApproveScorecardCommand(int Id, string? RowVersion, string? Comment) : IEntityCommand;

/// <summary>Sends the scorecard back to the employee. The comment is the reason and is required.</summary>
public sealed record ReturnScorecardCommand(int Id, string? RowVersion, string? Comment) : IEntityCommand;

/// <summary>Rejects the scorecard (final). The comment is the reason and is required.</summary>
public sealed record RejectScorecardCommand(int Id, string? RowVersion, string? Comment) : IEntityCommand;

/// <summary>HR's final review: Approved to Finalized. The comment is optional and kept as the HR comment.</summary>
public sealed record FinalizeScorecardCommand(int Id, string? RowVersion, string? Comment) : IEntityCommand;

/// <summary>The caller's own scorecards (<c>GET /api/kpi/my-scorecards</c>).</summary>
public interface IMyScorecardsHandler
{
    Task<Result<PagedResult<ScorecardDto>>> HandleAsync(PageRequest<ScorecardFilter> request, CancellationToken cancellationToken);
}

/// <summary>The scorecards waiting for the caller's review (<c>GET /api/kpi/reviews/pending</c>).</summary>
public interface IPendingReviewsHandler
{
    Task<Result<PagedResult<ScorecardDto>>> HandleAsync(PageRequest<ScorecardFilter> request, CancellationToken cancellationToken);
}
