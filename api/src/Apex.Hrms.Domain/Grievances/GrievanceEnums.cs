using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Grievances;

/// <summary>The life of a grievance case. The texts are the enum names and match CK_Case_Status exactly.</summary>
public enum GrievanceStatus
{
    Draft,
    Submitted,
    Acknowledged,
    UnderReview,
    Assigned,
    UnderInvestigation,
    AwaitingEmployeeResponse,
    ResolutionProposed,
    Resolved,
    Closed,
    Rejected,
    Reopened,
}

/// <summary>How urgent HR rates a case when acknowledging it. Critical escalates at once.</summary>
public enum GrievancePriority
{
    Low,
    Medium,
    High,
    Critical,
}

/// <summary>A step in the status history of a case (CK_StatusHistory_Action). Assign, Unassign, Escalate and Archive may keep the status.</summary>
public enum GrievanceHistoryAction
{
    Submit,
    Acknowledge,
    StartReview,
    Assign,
    Unassign,
    StartInvestigation,
    RequestInfo,
    Respond,
    ProposeResolution,
    ApproveResolution,
    RejectResolution,
    Reject,
    Close,
    Reopen,
    Escalate,
    Archive,
}

/// <summary>In which capacity somebody acted on a case.</summary>
public enum GrievanceActorRole
{
    Submitter,
    Management,
    Investigator,
    Reviewer,
    System,
}

/// <summary>An investigator's part in a case: one Lead at most, any number of Members.</summary>
public enum GrievanceAssignmentRole
{
    Lead,
    Member,
}

/// <summary>What an investigation note records.</summary>
public enum InvestigationNoteKind
{
    Note,
    Interview,
    FollowUp,
    Finding,
    Recommendation,
}

/// <summary>Which way a message goes.</summary>
public enum GrievanceMessageDirection
{
    ToSubmitter,
    FromSubmitter,
}

/// <summary>A plain message, a request for more information, or the submitter's answer to one.</summary>
public enum GrievanceMessageKind
{
    Message,
    InfoRequest,
    InfoResponse,
}

/// <summary>Who may see a file: the submitter too (Shared), the case team (CaseTeam) or management only (Restricted).</summary>
public enum GrievanceAttachmentVisibility
{
    Shared,
    CaseTeam,
    Restricted,
}

/// <summary>A reviewer's decision on a proposed resolution.</summary>
public enum ResolutionDecision
{
    Pending,
    Approved,
    Rejected,
}

/// <summary>The progress of a corrective action.</summary>
public enum CorrectiveActionStatus
{
    Proposed,
    Approved,
    Rejected,
    Implemented,
}

/// <summary>
/// How an employee's grievance reached HR when HR records it for them (<c>GrievanceCase.RecordForEmployee</c>). Not a column: the
/// channel is written into the reason of the Submit history entry.
/// </summary>
public enum GrievanceIntakeChannel
{
    InPerson,
    Phone,
    Email,
    Letter,
    Other,
}

/// <summary>What the grievance access log records (CK_AuditLog_Action).</summary>
public enum GrievanceAuditAction
{
    ViewCase,
    ViewHistory,
    DownloadAttachment,
    UploadAttachment,
    Export,
    Assign,
    Unassign,
    StatusChange,
    Triage,
    AddNote,
    SendMessage,
    CorrectiveAction,
    AccessRefused,
}

/// <summary>
/// The texts of the grievance enums: exactly the enum names (the database CHECK constraints and the UI contract use them), so a
/// number, another casing or a spaced spelling is rejected.
/// </summary>
public static class GrievanceText
{
    public static readonly TextMap<GrievanceStatus> Status = NameMap<GrievanceStatus>();

    public static readonly TextMap<GrievancePriority> Priority = NameMap<GrievancePriority>();

    public static readonly TextMap<GrievanceHistoryAction> HistoryAction = NameMap<GrievanceHistoryAction>();

    public static readonly TextMap<GrievanceActorRole> ActorRole = NameMap<GrievanceActorRole>();

    public static readonly TextMap<GrievanceAssignmentRole> AssignmentRole = NameMap<GrievanceAssignmentRole>();

    public static readonly TextMap<InvestigationNoteKind> NoteKind = NameMap<InvestigationNoteKind>();

    public static readonly TextMap<GrievanceMessageDirection> MessageDirection = NameMap<GrievanceMessageDirection>();

    public static readonly TextMap<GrievanceMessageKind> MessageKind = NameMap<GrievanceMessageKind>();

    public static readonly TextMap<GrievanceAttachmentVisibility> Visibility = NameMap<GrievanceAttachmentVisibility>();

    public static readonly TextMap<ResolutionDecision> Decision = NameMap<ResolutionDecision>();

    public static readonly TextMap<CorrectiveActionStatus> CorrectiveActionStatus = NameMap<CorrectiveActionStatus>();

    public static readonly TextMap<GrievanceAuditAction> AuditAction = NameMap<GrievanceAuditAction>();

    public static readonly TextMap<GrievanceIntakeChannel> IntakeChannel = NameMap<GrievanceIntakeChannel>();

    private static TextMap<TEnum> NameMap<TEnum>()
        where TEnum : struct, Enum =>
        new(Enum.GetValues<TEnum>().ToDictionary(value => value, value => value.ToString()));
}
