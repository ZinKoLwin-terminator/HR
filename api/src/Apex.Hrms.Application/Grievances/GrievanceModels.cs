using Apex.Hrms.Application.Performance.Scorecards;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Grievances;

// The HTTP contract of the grievance module (/api/grievances), typed after ui/src/app/core/models/grievance.models.ts. JSON is
// camelCase, enums are their exact texts, dates are yyyy-MM-dd, ...AtUtc fields are UTC date-times and rowVersion is base64.
// Confidentiality: the submitter of an anonymous case is on no object here; GrievanceRedaction builds every DTO.

// ---- Submitter view (grievance) ----

public sealed record GrievanceCategoryOptionDto(int Id, string Code, string Name, string Description, bool AllowsAnonymous, int DefaultTargetDays);

public sealed record GrievanceDepartmentOptionDto(int Id, string Name);

/// <summary><c>GET /api/grievances/options</c>: active categories by sort order and active departments by name.</summary>
public sealed record GrievanceOptionsDto(IReadOnlyList<GrievanceCategoryOptionDto> Categories, IReadOnlyList<GrievanceDepartmentOptionDto> Departments);

/// <summary>One of the caller's own cases. <c>Actor</c> texts elsewhere are "You", "HR team" or "System".</summary>
public sealed record MyGrievanceDto(
    int Id,
    string? CaseNo,
    int CategoryId,
    string CategoryName,
    string Subject,
    string Status,
    bool IsAnonymous,
    DateTime CreatedAtUtc,
    DateTime? SubmittedAtUtc,
    DateOnly? TargetResolutionDate,
    DateTime? LastUpdateAtUtc,
    bool HasUnreadUpdate,
    byte[] RowVersion) : IIdentified;

public sealed record MyGrievanceTimelineEntryDto(string FromStatus, string ToStatus, string Action, string Actor, string? Reason, DateTime OccurredAtUtc);

public sealed record MyGrievanceMessageDto(int Id, string Direction, string Kind, string Body, string Actor, int? InReplyToId, DateTime CreatedAtUtc);

public sealed record MyGrievanceAttachmentDto(int Id, string FileName, string ContentType, long SizeBytes, string UploadedBy, DateTime UploadedAtUtc);

/// <summary>
/// <c>GET /api/grievances/my/{id}</c>: never notes, corrective actions, priority, assignments or a case-team member's name. The rejection
/// reason only when Rejected; the resolution only when Resolved or Closed and HR made it visible.
/// </summary>
public sealed record MyGrievanceDetailDto(
    int Id,
    string? CaseNo,
    int CategoryId,
    string CategoryName,
    string Subject,
    string Status,
    bool IsAnonymous,
    DateTime CreatedAtUtc,
    DateTime? SubmittedAtUtc,
    DateOnly? TargetResolutionDate,
    DateTime? LastUpdateAtUtc,
    bool HasUnreadUpdate,
    byte[] RowVersion,
    string Description,
    DateOnly? IncidentDate,
    string? Location,
    string? InvolvedPersonText,
    int? InvolvedDepartmentId,
    string? InvolvedDepartmentName,
    DateTime? DeclarationAcceptedAtUtc,
    DateTime? AcknowledgedAtUtc,
    DateTime? ResolvedAtUtc,
    DateTime? ClosedAtUtc,
    string? RejectionReason,
    string? ResolutionSummary,
    IReadOnlyList<MyGrievanceTimelineEntryDto> Timeline,
    IReadOnlyList<MyGrievanceMessageDto> Messages,
    IReadOnlyList<MyGrievanceAttachmentDto> Attachments) : IIdentified;

public sealed record MyGrievanceFilter(string? Status = null);

/// <summary><c>GET /api/grievances/my/dashboard</c>: the caller's own counts and the five most recently updated cases.</summary>
public sealed record MyGrievanceDashboardDto(
    int Drafts,
    int Open,
    int AwaitingMyResponse,
    int Resolved,
    int Closed,
    int Rejected,
    int UnreadUpdates,
    IReadOnlyList<MyGrievanceDto> Recent);

/// <summary>The fields of a draft. A draft may be unfinished (a subject is required); submitting needs the full minimum lengths.</summary>
internal interface IGrievanceDraftFields
{
    int CategoryId { get; }

    string? Subject { get; }

    string? Description { get; }

    DateOnly? IncidentDate { get; }

    string? Location { get; }

    string? InvolvedPersonText { get; }

    int? InvolvedDepartmentId { get; }

    bool IsAnonymous { get; }
}

/// <summary><c>POST /api/grievances/my</c> (201): a new draft of the caller.</summary>
public sealed record GrievanceDraftCreateRequest(
    int CategoryId,
    string? Subject,
    string? Description,
    DateOnly? IncidentDate,
    string? Location,
    string? InvolvedPersonText,
    int? InvolvedDepartmentId,
    bool IsAnonymous) : IGrievanceDraftFields;

/// <summary><c>PUT /api/grievances/my/{id}</c>: Draft only, with the case's row version.</summary>
public sealed record GrievanceDraftUpdateRequest(
    string? RowVersion,
    int CategoryId,
    string? Subject,
    string? Description,
    DateOnly? IncidentDate,
    string? Location,
    string? InvolvedPersonText,
    int? InvolvedDepartmentId,
    bool IsAnonymous) : IGrievanceDraftFields, IVersionedRequest;

/// <summary><c>POST /api/grievances/my/{id}/submit</c>.</summary>
public sealed record SubmitGrievanceRequest(string? RowVersion, bool DeclarationAccepted) : IVersionedRequest;

/// <summary>
/// <c>POST /api/grievances/my/{id}/messages</c>. While the case awaits the submitter the message is the response: the row version is
/// checked and the case returns to its previous status. Otherwise a plain message: the row version is only checked for its format and
/// the case (and its row version) does not change.
/// </summary>
public sealed record MyGrievanceMessageRequest(string? RowVersion, string? Body, int? InReplyToId) : IVersionedRequest;

/// <summary>An upload: the case's row version, the client's file name (cleaned, never trusted) and the content.</summary>
public sealed record GrievanceFileUpload(string? RowVersion, string? FileName, byte[] Content);

/// <summary>
/// The answer to a submitter's upload (201): the new attachment and the case's row version, which a submitter's upload never changes.
/// </summary>
public sealed record MyGrievanceAttachmentUploadResult(MyGrievanceAttachmentDto Attachment, byte[] CaseRowVersion);

/// <summary>The submitter's own grievances (<c>/api/grievances/options</c> and <c>/my...</c>). A case of somebody else is NotFound.</summary>
public interface IMyGrievanceService
{
    Task<Result<GrievanceOptionsDto>> OptionsAsync(CancellationToken cancellationToken);

    Task<Result<PagedResult<MyGrievanceDto>>> QueryAsync(PageRequest<MyGrievanceFilter> request, CancellationToken cancellationToken);

    /// <summary>Not audited: a submitter's own reads leave no trace in the access log.</summary>
    Task<Result<MyGrievanceDetailDto>> GetAsync(int id, CancellationToken cancellationToken);

    Task<Result<MyGrievanceDashboardDto>> DashboardAsync(CancellationToken cancellationToken);

    /// <summary>A Shared, not removed attachment of one of the caller's cases (not audited).</summary>
    Task<Result<EvidenceDownload>> DownloadAttachmentAsync(int attachmentId, CancellationToken cancellationToken);

    Task<Result<MyGrievanceDetailDto>> CreateDraftAsync(GrievanceDraftCreateRequest request, CancellationToken cancellationToken);

    Task<Result<MyGrievanceDetailDto>> UpdateDraftAsync(int id, GrievanceDraftUpdateRequest request, CancellationToken cancellationToken);

    /// <summary>Removes a draft for good with its files (the database refuses deleting anything else).</summary>
    Task<Result> DeleteDraftAsync(int id, string? rowVersion, CancellationToken cancellationToken);

    Task<Result<MyGrievanceDetailDto>> SubmitAsync(int id, SubmitGrievanceRequest request, CancellationToken cancellationToken);

    Task<Result<MyGrievanceDetailDto>> SendMessageAsync(int id, MyGrievanceMessageRequest request, CancellationToken cancellationToken);

    /// <summary>The checks that need only the case id, run before the request body is read (404, 409 on the status). Read-only.</summary>
    Task<Result> PrecheckUploadAsync(int id, CancellationToken cancellationToken);

    Task<Result<MyGrievanceAttachmentUploadResult>> UploadAttachmentAsync(int id, GrievanceFileUpload upload, CancellationToken cancellationToken);

    /// <summary>Clears the unread badge (204, idempotent). Writes grv.CaseSubmitter only: the case row and its row version never change.</summary>
    Task<Result> MarkSeenAsync(int id, CancellationToken cancellationToken);
}

// ---- Case-team view (grievance-management, grievance-investigation, grievance-review) ----

public sealed record GrievanceSubmitterDto(
    int EmployeeId,
    string EmployeeCode,
    string DisplayName,
    int? CompanyId,
    int? BranchId,
    string? BranchName,
    int? DepartmentId,
    string? DepartmentName,
    int? PositionId,
    string? PositionName);

/// <summary>One case in a case-team list (never a Draft).</summary>
public sealed record GrievanceCaseDto(
    int Id,
    string CaseNo,
    int CategoryId,
    string CategoryName,
    string Subject,
    string Status,
    string? Priority,
    bool IsAnonymous,
    string SubmitterLabel,
    int? DepartmentId,
    string? DepartmentName,
    DateTime SubmittedAtUtc,
    DateOnly? TargetResolutionDate,
    bool IsOverdue,
    bool IsEscalated,
    int? LeadInvestigatorEmployeeId,
    string? LeadInvestigatorName,
    DateTime? ArchivedAtUtc,
    byte[] RowVersion) : IIdentified;

public sealed record GrievanceAssignmentDto(
    int Id,
    int InvestigatorEmployeeId,
    string InvestigatorCode,
    string InvestigatorName,
    string Role,
    DateTime AssignedAtUtc,
    string AssignedByName,
    DateTime? UnassignedAtUtc,
    string? UnassignedByName,
    string? Reason);

public sealed record GrievanceNoteDto(
    int Id,
    string Kind,
    string Body,
    DateOnly? InterviewDate,
    string? IntervieweeText,
    int? AuthorEmployeeId,
    string AuthorName,
    DateTime CreatedAtUtc);

public sealed record GrievanceMessageDto(
    int Id,
    string Direction,
    string Kind,
    string Body,
    string AuthorRole,
    string AuthorName,
    int? InReplyToId,
    DateTime CreatedAtUtc);

public sealed record GrievanceAttachmentDto(
    int Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Visibility,
    string UploadedByRole,
    DateTime UploadedAtUtc,
    int? CommunicationId,
    int? NoteId);

public sealed record GrievanceCorrectiveActionDto(
    int Id,
    int CaseId,
    string ActionType,
    string Description,
    string? ResponsibleText,
    DateOnly? DueDate,
    DateOnly? CompletedOn,
    string Status,
    byte[] RowVersion) : IIdentified;

public sealed record GrievanceResolutionProposalDto(
    int Id,
    string Summary,
    int? ProposedByEmployeeId,
    string ProposedByName,
    DateTime ProposedAtUtc,
    string Decision,
    string? DecidedByName,
    DateTime? DecidedAtUtc,
    string? DecisionComment);

public sealed record GrievanceEscalationDto(
    int Id,
    string Reason,
    bool IsImmediate,
    string EscalatedByName,
    DateTime EscalatedAtUtc,
    DateTime? ClearedAtUtc,
    string? Outcome);

/// <summary><c>GET /api/grievances/cases/{id}</c> (audited): the sections are trimmed by the caller's tiers on the case.</summary>
public sealed record GrievanceCaseDetailDto(
    int Id,
    string CaseNo,
    int CategoryId,
    string CategoryName,
    string Subject,
    string Status,
    string? Priority,
    bool IsAnonymous,
    string SubmitterLabel,
    int? DepartmentId,
    string? DepartmentName,
    DateTime SubmittedAtUtc,
    DateOnly? TargetResolutionDate,
    bool IsOverdue,
    bool IsEscalated,
    int? LeadInvestigatorEmployeeId,
    string? LeadInvestigatorName,
    DateTime? ArchivedAtUtc,
    byte[] RowVersion,
    GrievanceSubmitterDto? Submitter,
    IReadOnlyList<string> ViewerRoles,
    bool IsLeadInvestigator,
    string Description,
    DateOnly? IncidentDate,
    string? Location,
    string? InvolvedPersonText,
    int? InvolvedDepartmentId,
    string? InvolvedDepartmentName,
    int? SubjectEmployeeId,
    string? SubjectEmployeeName,
    string? ReturnStatus,
    DateTime DeclarationAcceptedAtUtc,
    DateTime? AcknowledgedAtUtc,
    DateTime? ResolvedAtUtc,
    DateTime? ClosedAtUtc,
    string? ResolutionSummary,
    bool ResolutionVisibleToSubmitter,
    string? RejectionReason,
    DateOnly? RetentionUntil,
    bool LegalHold,
    IReadOnlyList<GrievanceAssignmentDto> Assignments,
    IReadOnlyList<GrievanceNoteDto> Notes,
    IReadOnlyList<GrievanceMessageDto> Messages,
    IReadOnlyList<GrievanceAttachmentDto> Attachments,
    IReadOnlyList<GrievanceCorrectiveActionDto> CorrectiveActions,
    GrievanceResolutionProposalDto? Proposal,
    IReadOnlyList<GrievanceEscalationDto> Escalations) : IIdentified;

/// <summary>One status-history row. The submitter is never identified by a user id; a named submitter is shown by name and employee.</summary>
public sealed record GrievanceHistoryEntryDto(
    int Id,
    string FromStatus,
    string ToStatus,
    string Action,
    string ActorRole,
    int? ActorUserId,
    int? ActorEmployeeId,
    string ActorName,
    string? Reason,
    bool VisibleToSubmitter,
    DateTime OccurredAtUtc);

public sealed record GrievanceAuditEntryDto(
    long Id,
    int? CaseId,
    string? CaseNo,
    string Action,
    int? ActorUserId,
    string? ActorName,
    string ActorRole,
    DateTime OccurredAtUtc,
    string? Detail);

/// <summary>The access-log filters: the action, and the business dates from and to (inclusive).</summary>
public sealed record GrievanceAuditFilter(string? Action = null, DateOnly? From = null, DateOnly? To = null);

/// <summary>The filters of the case-team lists, combined with AND. A department filter never matches an anonymous case.</summary>
public sealed record GrievanceCaseFilter(
    string? Status = null,
    string? Priority = null,
    int? CategoryId = null,
    int? DepartmentId = null,
    bool? IsAnonymous = null,
    bool? IsEscalated = null,
    bool? Overdue = null,
    bool? Archived = null,
    int? InvestigatorEmployeeId = null,
    DateOnly? SubmittedFrom = null,
    DateOnly? SubmittedTo = null);

public sealed record GrievanceInvestigatorOptionDto(
    int EmployeeId,
    string EmployeeCode,
    string DisplayName,
    string? DepartmentName,
    string? PositionName,
    int ActiveCaseCount);

/// <summary>One active employee for the triage subject-employee picker (<c>GET /employees</c>): no personal data beyond the name and placement.</summary>
public sealed record GrievanceEmployeeOptionDto(int EmployeeId, string EmployeeCode, string DisplayName, string? DepartmentName, string? PositionName);

/// <summary>The case-team reads. The scope of a case is the union of the caller's tiers, never a case the caller is the subject or the submitter of.</summary>
public interface IGrievanceCaseReader
{
    /// <summary><c>GET /cases/query</c> (management): every submitted case; archived ones only (and only them) with <c>archived=true</c>.</summary>
    Task<Result<PagedResult<GrievanceCaseDto>>> QueryAsync(PageRequest<GrievanceCaseFilter> request, CancellationToken cancellationToken);

    /// <summary><c>GET /investigations</c>: the cases with the caller's active assignment.</summary>
    Task<Result<PagedResult<GrievanceCaseDto>>> InvestigationsAsync(PageRequest<GrievanceCaseFilter> request, CancellationToken cancellationToken);

    /// <summary><c>GET /reviews/pending</c>: open escalated cases and pending proposals, oldest first.</summary>
    Task<Result<PagedResult<GrievanceCaseDto>>> PendingReviewsAsync(PageRequest<GrievanceCaseFilter> request, CancellationToken cancellationToken);

    /// <summary><c>GET /cases/{id}</c> (audited ViewCase).</summary>
    Task<Result<GrievanceCaseDetailDto>> GetAsync(int id, CancellationToken cancellationToken);

    /// <summary><c>GET /cases/{id}/history</c> (audited ViewHistory), oldest first.</summary>
    Task<Result<IReadOnlyList<GrievanceHistoryEntryDto>>> HistoryAsync(int id, CancellationToken cancellationToken);

    /// <summary><c>GET /cases/{id}/audit</c> (management, administrator at the endpoint), newest first.</summary>
    Task<Result<PagedResult<GrievanceAuditEntryDto>>> AuditAsync(int id, PageRequest<GrievanceAuditFilter> request, CancellationToken cancellationToken);

    /// <summary>
    /// <c>GET /audit</c> (management View and administrator): the whole access log, newest first: entries without a case (exports) and
    /// entries of the cases in the caller's management scope (so never a case the caller submitted or is the subject of).
    /// </summary>
    Task<Result<PagedResult<GrievanceAuditEntryDto>>> AuditLogAsync(PageRequest<GrievanceAuditFilter> request, CancellationToken cancellationToken);

    /// <summary>
    /// <c>GET /investigators</c>: employees holding <c>grievance-investigation:Edit</c>, by display name. Never filtered per case: a
    /// missing name would reveal an anonymous submitter. <c>assign</c> refuses a conflicted choice instead.
    /// </summary>
    Task<Result<IReadOnlyList<GrievanceInvestigatorOptionDto>>> InvestigatorsAsync(string? search, CancellationToken cancellationToken);

    /// <summary>
    /// <c>GET /employees?search=</c> (<c>grievance-management:Edit</c>): at most 20 active employees (not resigned, terminated or left) whose
    /// code or name matches, by display name, for the subject-employee picker of triage. It does not need <c>employees:View</c> and shows
    /// nothing the employee lookup does not.
    /// </summary>
    Task<Result<IReadOnlyList<GrievanceEmployeeOptionDto>>> EmployeeOptionsAsync(string? search, CancellationToken cancellationToken);

    /// <summary><c>GET /attachments/{id}</c> (audited DownloadAttachment): only files the caller's tiers on the case may open.</summary>
    Task<Result<EvidenceDownload>> DownloadAttachmentAsync(int attachmentId, CancellationToken cancellationToken);
}

// ---- Case-team commands: every one answers with the case detail and its new row version ----

public sealed record AcknowledgeGrievanceCommand(int Id, string? RowVersion, string? Priority, DateOnly? TargetResolutionDate, string? Comment) : IEntityCommand;

public sealed record StartGrievanceReviewCommand(int Id, string? RowVersion, string? Comment) : IEntityCommand;

public sealed record AssignGrievanceInvestigatorCommand(int Id, string? RowVersion, int InvestigatorEmployeeId, string? Role, string? Reason) : IEntityCommand;

public sealed record UnassignGrievanceInvestigatorCommand(int Id, string? RowVersion, int AssignmentId, string? Reason) : IEntityCommand;

public sealed record StartGrievanceInvestigationCommand(int Id, string? RowVersion, string? Comment) : IEntityCommand;

public sealed record RequestGrievanceInfoCommand(int Id, string? RowVersion, string? Message) : IEntityCommand;

public sealed record ProposeGrievanceResolutionCommand(int Id, string? RowVersion, string? Summary) : IEntityCommand;

public sealed record ApproveGrievanceResolutionCommand(int Id, string? RowVersion, bool VisibleToSubmitter, string? Comment) : IEntityCommand;

public sealed record RejectGrievanceResolutionCommand(int Id, string? RowVersion, string? Reason) : IEntityCommand;

public sealed record RejectGrievanceCommand(int Id, string? RowVersion, string? Reason) : IEntityCommand;

public sealed record CloseGrievanceCommand(int Id, string? RowVersion, string? Comment) : IEntityCommand;

public sealed record ReopenGrievanceCommand(int Id, string? RowVersion, string? Reason) : IEntityCommand;

public sealed record EscalateGrievanceCommand(int Id, string? RowVersion, string? Reason, bool IsImmediate) : IEntityCommand;

public sealed record ArchiveGrievanceCommand(int Id, string? RowVersion, string? Comment) : IEntityCommand;

/// <summary><c>PUT /cases/{id}/triage</c>. On a case that is no longer open only the legal hold may change.</summary>
public sealed record TriageGrievanceCommand(
    int Id,
    string? RowVersion,
    int CategoryId,
    string? Priority,
    DateOnly? TargetResolutionDate,
    int? SubjectEmployeeId,
    int? InvolvedDepartmentId,
    bool? LegalHold) : IEntityCommand;

/// <summary><c>POST /cases/{id}/messages</c>: a Message to the submitter (a case-team message never replies to anything).</summary>
public sealed record SendGrievanceCaseMessageCommand(int Id, string? RowVersion, string? Body) : IEntityCommand;

/// <summary><c>POST /cases/{id}/notes</c>: append-only. The case does not change, so the row version is only checked for its format.</summary>
public sealed record AddGrievanceNoteCommand(
    int Id,
    string? RowVersion,
    string? Kind,
    string? Body,
    DateOnly? InterviewDate,
    string? IntervieweeText) : IEntityCommand;

/// <summary><c>POST /cases/{id}/actions</c> (201). The row version is the case's.</summary>
public sealed record GrievanceCorrectiveActionCreateRequest(
    string? RowVersion,
    string? ActionType,
    string? Description,
    string? ResponsibleText,
    DateOnly? DueDate) : IVersionedRequest;

/// <summary><c>PUT /case-actions/{id}</c>. The row version is the action's own.</summary>
public sealed record GrievanceCorrectiveActionUpdateRequest(
    string? RowVersion,
    string? ActionType,
    string? Description,
    string? ResponsibleText,
    DateOnly? DueDate,
    DateOnly? CompletedOn,
    string? Status) : IVersionedRequest;

/// <summary>The answer to a case-team upload: the attachment and the case's row version now.</summary>
public sealed record GrievanceAttachmentUploadResult(GrievanceAttachmentDto Attachment, byte[] CaseRowVersion);

/// <summary>The case work that is not a workflow command: files and corrective actions.</summary>
public interface IGrievanceCaseWork
{
    /// <summary>The checks that need only the case id (404, 403, 409 on the status), before the body is read. Read-only.</summary>
    Task<Result> PrecheckUploadAsync(int id, CancellationToken cancellationToken);

    /// <summary><c>POST /cases/{id}/attachments</c> (multipart rowVersion, visibility, file).</summary>
    Task<Result<GrievanceAttachmentUploadResult>> UploadAttachmentAsync(int id, string? visibility, GrievanceFileUpload upload, CancellationToken cancellationToken);

    Task<Result<GrievanceCorrectiveActionDto>> AddCorrectiveActionAsync(int id, GrievanceCorrectiveActionCreateRequest request, CancellationToken cancellationToken);

    Task<Result<GrievanceCorrectiveActionDto>> UpdateCorrectiveActionAsync(int actionId, GrievanceCorrectiveActionUpdateRequest request, CancellationToken cancellationToken);
}
