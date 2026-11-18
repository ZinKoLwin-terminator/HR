using Apex.Hrms.Domain.Grievances;

namespace Apex.Hrms.Application.Grievances;

/// <summary>An employee as the grievance screens name them.</summary>
internal sealed record GrievancePerson(int EmployeeId, string Code, string DisplayName);

/// <summary>The named submitter of a case with the placement copied at submission (never built for an anonymous case).</summary>
internal sealed record GrievanceNamedSubmitter(
    GrievancePerson Person,
    string? BranchName,
    string? DepartmentName,
    string? PositionName);

/// <summary>Everything a case-team detail is built from, loaded by <see cref="GrievanceReader"/>, before any trimming.</summary>
internal sealed record GrievanceCaseBundle(
    GrievanceCase Case,
    string CategoryName,
    string? InvolvedDepartmentName,
    GrievanceNamedSubmitter? Submitter,
    string? SubjectEmployeeName,
    IReadOnlyList<GrievanceAssignment> Assignments,
    IReadOnlyList<GrievanceInvestigationNote> Notes,
    IReadOnlyList<GrievanceCommunication> Messages,
    IReadOnlyList<GrievanceAttachment> Attachments,
    IReadOnlyList<GrievanceCorrectiveAction> CorrectiveActions,
    IReadOnlyList<GrievanceResolutionProposal> Proposals,
    IReadOnlyList<GrievanceEscalation> Escalations,
    IReadOnlyDictionary<int, GrievancePerson> People,
    IReadOnlyDictionary<int, string> AccountNames);

/// <summary>What a submitter's own detail is built from.</summary>
internal sealed record MyGrievanceBundle(
    GrievanceCase Case,
    string CategoryName,
    string? InvolvedDepartmentName,
    GrievanceCaseSubmitter Submitter,
    IReadOnlyList<GrievanceStatusHistoryEntry> History,
    IReadOnlyList<GrievanceCommunication> Messages,
    IReadOnlyList<GrievanceAttachment> Attachments);

/// <summary>
/// The one place that turns grievance records into what a caller may read. Rules:
/// <list type="bullet">
/// <item>Anonymous case: <c>submitter</c> is null, <c>submitterLabel</c> "Anonymous", the department is not disclosed (null here,
/// "Not disclosed" in reports); the history and messages already record the submitter as "Anonymous submitter" with no ids.</item>
/// <item>Named case: the history and messages record the submitter as "Submitter" with no ids; the case team sees the submitter's name and
/// employee id resolved through grv.CaseSubmitter (never a user id).</item>
/// <item>Submitter's view: never notes, corrective actions, priority, assignments, CaseTeam or Restricted files, or any case-team member's
/// name ("HR team"); the timeline only has the steps marked visible to the submitter, with a reason only for reject, reopen and
/// information requests; the rejection reason only when Rejected; the resolution only when Resolved or Closed and made visible.</item>
/// <item>Case team, by tier on the case: management sees everything; an investigator no corrective actions and no Restricted files; a
/// reviewer only Finding and Recommendation notes, no messages, only Shared files, no corrective actions, no assignment or escalation
/// reasons, and no history reasons except why a proposal was sent back (its decision comment). Several tiers see the union.</item>
/// </list>
/// </summary>
internal static class GrievanceRedaction
{
    public const string AnonymousLabel = "Anonymous";
    public const string NotDisclosed = "Not disclosed";
    public const string You = "You";
    public const string HrTeam = "HR team";
    public const string SystemActor = "System";

    private static readonly GrievanceHistoryAction[] ReasonsShownToSubmitter =
        [GrievanceHistoryAction.Reject, GrievanceHistoryAction.Reopen, GrievanceHistoryAction.RequestInfo];

    // ---- Submitter view ----

    public static string MyActor(GrievanceActorRole role) => role switch
    {
        GrievanceActorRole.Submitter => You,
        GrievanceActorRole.System => SystemActor,
        _ => HrTeam,
    };

    /// <summary>The last change the submitter should notice: a draft's own last edit, otherwise the last case-team update or the submission.</summary>
    public static DateTime? LastUpdateOf(GrievanceCase grievance) =>
        grievance.Status == GrievanceStatus.Draft ? grievance.ModifiedAtUtc : grievance.LastSubmitterUpdateAtUtc ?? grievance.SubmittedAtUtc;

    public static MyGrievanceDto ToMy(GrievanceCase grievance, string categoryName, DateTime? seenAtUtc) => new(
        grievance.Id,
        grievance.CaseNo,
        grievance.CategoryId,
        categoryName,
        grievance.Subject,
        GrievanceText.Status.ToText(grievance.Status),
        grievance.IsAnonymous,
        grievance.CreatedAtUtc,
        grievance.SubmittedAtUtc,
        grievance.TargetResolutionDate,
        LastUpdateOf(grievance),
        HasUnread(grievance.LastSubmitterUpdateAtUtc, seenAtUtc),
        grievance.RowVersion);

    public static bool HasUnread(DateTime? lastUpdateAtUtc, DateTime? seenAtUtc) =>
        lastUpdateAtUtc is { } updated && (seenAtUtc is null || updated > seenAtUtc);

    public static MyGrievanceAttachmentDto ToMy(GrievanceAttachment attachment) => new(
        attachment.Id,
        attachment.FileName,
        attachment.ContentType,
        attachment.SizeBytes,
        MyActor(attachment.UploadedByRole),
        attachment.UploadedAtUtc);

    public static MyGrievanceDetailDto ToMyDetail(MyGrievanceBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        var grievance = bundle.Case;
        var summary = ToMy(grievance, bundle.CategoryName, bundle.Submitter.SubmitterSeenAtUtc);
        var resolutionShown = grievance.Status is GrievanceStatus.Resolved or GrievanceStatus.Closed && grievance.ResolutionVisibleToSubmitter;
        return new MyGrievanceDetailDto(
            summary.Id,
            summary.CaseNo,
            summary.CategoryId,
            summary.CategoryName,
            summary.Subject,
            summary.Status,
            summary.IsAnonymous,
            summary.CreatedAtUtc,
            summary.SubmittedAtUtc,
            summary.TargetResolutionDate,
            summary.LastUpdateAtUtc,
            summary.HasUnreadUpdate,
            summary.RowVersion,
            grievance.Description,
            grievance.IncidentDate,
            grievance.Location,
            grievance.InvolvedPersonText,
            grievance.InvolvedDepartmentId,
            bundle.InvolvedDepartmentName,
            grievance.DeclarationAcceptedAtUtc,
            grievance.AcknowledgedAtUtc,
            grievance.ResolvedAtUtc,
            grievance.ClosedAtUtc,
            grievance.Status == GrievanceStatus.Rejected ? grievance.RejectionReason : null,
            resolutionShown ? grievance.ResolutionSummary : null,
            [.. bundle.History
                .Where(entry => entry.VisibleToSubmitter)
                .OrderBy(entry => entry.OccurredAtUtc).ThenBy(entry => entry.Id)
                .Select(entry => new MyGrievanceTimelineEntryDto(
                    GrievanceText.Status.ToText(entry.FromStatus),
                    GrievanceText.Status.ToText(entry.ToStatus),
                    GrievanceText.HistoryAction.ToText(entry.Action),
                    MyActor(entry.ActorRole),
                    ReasonsShownToSubmitter.Contains(entry.Action) ? entry.Reason : null,
                    entry.OccurredAtUtc))],
            [.. bundle.Messages
                .OrderBy(message => message.CreatedAtUtc).ThenBy(message => message.Id)
                .Select(message => new MyGrievanceMessageDto(
                    message.Id,
                    GrievanceText.MessageDirection.ToText(message.Direction),
                    GrievanceText.MessageKind.ToText(message.Kind),
                    message.Body,
                    MyActor(message.AuthorRole),
                    message.InReplyToId,
                    message.CreatedAtUtc))],
            [.. bundle.Attachments
                .Where(attachment => attachment.Visibility == GrievanceAttachmentVisibility.Shared && !attachment.IsRemoved)
                .OrderBy(attachment => attachment.UploadedAtUtc).ThenBy(attachment => attachment.Id)
                .Select(ToMy)]);
    }

    // ---- Case-team view ----

    public static string SubmitterLabel(bool isAnonymous, string? namedSubmitterName) =>
        isAnonymous ? AnonymousLabel : namedSubmitterName ?? string.Empty;

    public static GrievanceCaseDto ToCase(GrievanceCaseRow row, DateOnly today) => new(
        row.Id,
        row.CaseNo ?? string.Empty,
        row.CategoryId,
        row.CategoryName,
        row.Subject,
        GrievanceText.Status.ToText(row.Status),
        row.Priority is { } priority ? GrievanceText.Priority.ToText(priority) : null,
        row.IsAnonymous,
        SubmitterLabel(row.IsAnonymous, row.SubmitterName),
        row.IsAnonymous ? null : row.DepartmentId,
        row.IsAnonymous ? null : row.DepartmentName,
        row.SubmittedAtUtc ?? default,
        row.TargetResolutionDate,
        GrievanceCase.IsActive(row.Status) && row.TargetResolutionDate is { } target && target < today,
        row.IsEscalated,
        row.LeadEmployeeId,
        row.LeadName,
        row.ArchivedAtUtc,
        row.RowVersion);

    public static GrievanceAttachmentDto ToTeam(GrievanceAttachment attachment) => new(
        attachment.Id,
        attachment.FileName,
        attachment.ContentType,
        attachment.SizeBytes,
        GrievanceText.Visibility.ToText(attachment.Visibility),
        GrievanceText.ActorRole.ToText(attachment.UploadedByRole),
        attachment.UploadedAtUtc,
        attachment.CommunicationId,
        attachment.NoteId);

    public static GrievanceCorrectiveActionDto ToDto(GrievanceCorrectiveAction action) => new(
        action.Id,
        action.CaseId,
        action.ActionType,
        action.Description,
        action.ResponsibleText,
        action.DueDate,
        action.CompletedOn,
        GrievanceText.CorrectiveActionStatus.ToText(action.Status),
        action.RowVersion);

    /// <summary>True when the caller's tiers on the case may open a file of this visibility.</summary>
    public static bool MayOpen(GrievanceCaseRoles roles, GrievanceAttachmentVisibility visibility) => visibility switch
    {
        GrievanceAttachmentVisibility.Shared => roles.Management || roles.Investigator || roles.Reviewer,
        GrievanceAttachmentVisibility.CaseTeam => roles.Management || roles.Investigator,
        _ => roles.Management,
    };

    public static GrievanceCaseDetailDto ToCaseDetail(GrievanceCaseBundle bundle, GrievanceCaseRoles roles, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(roles);
        var grievance = bundle.Case;
        var named = grievance.IsAnonymous ? null : bundle.Submitter;
        var lead = bundle.Assignments.FirstOrDefault(assignment => assignment.IsActive && assignment.Role == GrievanceAssignmentRole.Lead);
        var leadPerson = lead is null ? null : bundle.People.GetValueOrDefault(lead.InvestigatorEmployeeId);
        var seesWork = roles.SeesCaseWork;

        // A reviewer who is not also management or an assigned investigator reads findings, recommendations and the proposal (with its
        // decision comment) only (D7, security review L8): no assignment or escalation reasons either.
        var reviewerOnly = !seesWork;
        string Account(int? userId) => userId is { } id ? bundle.AccountNames.GetValueOrDefault(id, "Former user") : SystemActor;

        var proposal = bundle.Proposals.FirstOrDefault(candidate => candidate.IsPending)
            ?? bundle.Proposals.Where(candidate => !candidate.IsPending).OrderByDescending(candidate => candidate.DecidedAtUtc).ThenByDescending(candidate => candidate.Id).FirstOrDefault();

        return new GrievanceCaseDetailDto(
            grievance.Id,
            grievance.CaseNo ?? string.Empty,
            grievance.CategoryId,
            bundle.CategoryName,
            grievance.Subject,
            GrievanceText.Status.ToText(grievance.Status),
            grievance.Priority is { } priority ? GrievanceText.Priority.ToText(priority) : null,
            grievance.IsAnonymous,
            SubmitterLabel(grievance.IsAnonymous, named?.Person.DisplayName),
            named is null ? null : grievance.DepartmentId,
            named?.DepartmentName,
            grievance.SubmittedAtUtc ?? default,
            grievance.TargetResolutionDate,
            grievance.IsOverdue(today),
            grievance.IsEscalated,
            lead?.InvestigatorEmployeeId,
            leadPerson?.DisplayName,
            grievance.ArchivedAtUtc,
            grievance.RowVersion,
            named is null
                ? null
                : new GrievanceSubmitterDto(
                    named.Person.EmployeeId,
                    named.Person.Code,
                    named.Person.DisplayName,
                    grievance.CompanyId,
                    grievance.BranchId,
                    named.BranchName,
                    grievance.DepartmentId,
                    named.DepartmentName,
                    grievance.PositionId,
                    named.PositionName),
            roles.ViewerRoles,
            roles.IsLead,
            grievance.Description,
            grievance.IncidentDate,
            grievance.Location,
            grievance.InvolvedPersonText,
            grievance.InvolvedDepartmentId,
            bundle.InvolvedDepartmentName,
            grievance.SubjectEmployeeId,
            bundle.SubjectEmployeeName,
            grievance.ReturnStatus is { } returnStatus ? GrievanceText.Status.ToText(returnStatus) : null,
            grievance.DeclarationAcceptedAtUtc ?? default,
            grievance.AcknowledgedAtUtc,
            grievance.ResolvedAtUtc,
            grievance.ClosedAtUtc,
            grievance.ResolutionSummary,
            grievance.ResolutionVisibleToSubmitter,
            grievance.RejectionReason,
            grievance.RetentionUntil,
            grievance.LegalHold,
            [.. bundle.Assignments
                .OrderBy(assignment => assignment.IsActive ? 0 : 1)
                .ThenByDescending(assignment => assignment.AssignedAtUtc)
                .ThenByDescending(assignment => assignment.Id)
                .Select(assignment =>
                {
                    var person = bundle.People.GetValueOrDefault(assignment.InvestigatorEmployeeId);
                    return new GrievanceAssignmentDto(
                        assignment.Id,
                        assignment.InvestigatorEmployeeId,
                        person?.Code ?? string.Empty,
                        person?.DisplayName ?? string.Empty,
                        GrievanceText.AssignmentRole.ToText(assignment.Role),
                        assignment.AssignedAtUtc,
                        Account(assignment.AssignedByUserId),
                        assignment.UnassignedAtUtc,
                        assignment.UnassignedByUserId is null ? null : Account(assignment.UnassignedByUserId),
                        reviewerOnly ? null : assignment.Reason);
                })],
            [.. bundle.Notes
                .Where(note => seesWork || (roles.Reviewer && note.IsVisibleToReviewers))
                .OrderBy(note => note.CreatedAtUtc).ThenBy(note => note.Id)
                .Select(note => new GrievanceNoteDto(
                    note.Id,
                    GrievanceText.NoteKind.ToText(note.Kind),
                    note.Body,
                    note.InterviewDate,
                    note.IntervieweeText,
                    note.AuthorEmployeeId,
                    note.AuthorName,
                    note.CreatedAtUtc))],
            seesWork
                ? [.. bundle.Messages
                    .OrderBy(message => message.CreatedAtUtc).ThenBy(message => message.Id)
                    .Select(message => new GrievanceMessageDto(
                        message.Id,
                        GrievanceText.MessageDirection.ToText(message.Direction),
                        GrievanceText.MessageKind.ToText(message.Kind),
                        message.Body,
                        GrievanceText.ActorRole.ToText(message.AuthorRole),
                        AuthorName(message.AuthorRole, message.AuthorName, grievance.IsAnonymous, named),
                        message.InReplyToId,
                        message.CreatedAtUtc))]
                : [],
            [.. bundle.Attachments
                .Where(attachment => !attachment.IsRemoved && MayOpen(roles, attachment.Visibility))
                .OrderBy(attachment => attachment.UploadedAtUtc).ThenBy(attachment => attachment.Id)
                .Select(ToTeam)],
            roles.Management ? [.. bundle.CorrectiveActions.OrderBy(action => action.Id).Select(ToDto)] : [],
            proposal is null
                ? null
                : new GrievanceResolutionProposalDto(
                    proposal.Id,
                    proposal.Summary,
                    proposal.ProposedByEmployeeId,
                    proposal.ProposedByEmployeeId is { } proposer && bundle.People.TryGetValue(proposer, out var person)
                        ? person.DisplayName
                        : Account(proposal.ProposedByUserId),
                    proposal.ProposedAtUtc,
                    GrievanceText.Decision.ToText(proposal.Decision),
                    proposal.DecidedByUserId is null ? null : Account(proposal.DecidedByUserId),
                    proposal.DecidedAtUtc,
                    proposal.DecisionComment),
            [.. bundle.Escalations
                .OrderByDescending(escalation => escalation.EscalatedAtUtc).ThenByDescending(escalation => escalation.Id)
                .Select(escalation => new GrievanceEscalationDto(
                    escalation.Id,
                    reviewerOnly ? string.Empty : escalation.Reason,
                    escalation.IsImmediate,
                    Account(escalation.EscalatedByUserId),
                    escalation.EscalatedAtUtc,
                    escalation.ClearedAtUtc,
                    escalation.Outcome))]);
    }

    /// <summary>
    /// One history row for the case team. A submitter row stays without a user id; on a named case it shows the submitter's name and
    /// employee id (known to the case team anyway), on an anonymous case "Anonymous submitter" with no ids. A reviewer who is not also
    /// management or an assigned investigator does not read the text of an information request (it is a message to the submitter).
    /// </summary>
    public static GrievanceHistoryEntryDto ToHistory(GrievanceStatusHistoryEntry entry, bool isAnonymous, GrievancePerson? namedSubmitter, GrievanceCaseRoles roles)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(roles);
        var bySubmitter = entry.ActorRole == GrievanceActorRole.Submitter;
        var showNamed = bySubmitter && !isAnonymous && namedSubmitter is not null;
        return new GrievanceHistoryEntryDto(
            entry.Id,
            GrievanceText.Status.ToText(entry.FromStatus),
            GrievanceText.Status.ToText(entry.ToStatus),
            GrievanceText.HistoryAction.ToText(entry.Action),
            GrievanceText.ActorRole.ToText(entry.ActorRole),
            bySubmitter ? null : entry.ActorUserId,
            showNamed ? namedSubmitter!.EmployeeId : bySubmitter ? null : entry.ActorEmployeeId,
            showNamed ? namedSubmitter!.DisplayName : bySubmitter && isAnonymous ? GrievanceActor.AnonymousSubmitterName : entry.ActorName,
            roles.SeesCaseWork || entry.Action == GrievanceHistoryAction.RejectResolution ? entry.Reason : null,
            entry.VisibleToSubmitter,
            entry.OccurredAtUtc);
    }

    private static string AuthorName(GrievanceActorRole role, string stored, bool isAnonymous, GrievanceNamedSubmitter? named) =>
        role != GrievanceActorRole.Submitter
            ? stored
            : isAnonymous ? GrievanceActor.AnonymousSubmitterName : named?.Person.DisplayName ?? GrievanceActor.NamedSubmitterName;
}
