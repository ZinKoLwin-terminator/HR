using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Grievances;

/// <summary>
/// The life of a grievance case. Every status change is a method here that returns the <see cref="GrievanceStep"/> to save (its
/// history entry and the rows that go with it); nothing else moves a status.
/// <code>
/// Draft --submit--> Submitted --acknowledge--> Acknowledged --start review--> UnderReview --propose--> ResolutionProposed
///                                                   |  \--assign lead--> Assigned --start investigation--> UnderInvestigation --propose (lead)--^
///                                                   |                                                                    approve --> Resolved --close--> Closed
/// reject: Submitted, Acknowledged, UnderReview --> Rejected        reject proposal: --> UnderInvestigation (a lead exists) or UnderReview
/// request info: UnderReview, Assigned, UnderInvestigation --> AwaitingEmployeeResponse --respond--> back to ReturnStatus
/// reopen: Resolved, Rejected, Closed --> Reopened (then start review or assign a lead)
/// escalate (any open status) and archive (Closed or Rejected, no legal hold) keep the status.
/// </code>
/// Rules here: the statuses, the reasons, the summary, the declaration, nobody deciding their own proposal, Closed is final (only
/// reopen and archive touch it), an archived case never changes, and the submitter of an anonymous case is never identified in what
/// it writes. Who may act (permissions, scopes, assigned investigator, reviewer) and the row version are rules of the application
/// layer, which needs other records. A refused status is a Conflict (409); bad input a Validation failure (400); deciding your own
/// proposal Forbidden (403).
/// </summary>
public sealed partial class GrievanceCase
{
    /// <summary>The reason recorded for the immediate escalation of a Critical case at acknowledgement.</summary>
    public const string CriticalEscalationReason = "Critical priority: escalated when acknowledged.";

    /// <summary>The reason recorded when triage raises an open case to Critical (escalated at once, like at acknowledgement).</summary>
    public const string CriticalTriageEscalationReason = "Critical priority: escalated at triage.";

    /// <summary>The reason of the Unassign entries written when reopening a case ends its assignments.</summary>
    public const string ReopenUnassignReason = "Case reopened";

    /// <summary>
    /// The one answer to assigning the subject of a case or the submitter of a named case: it never says which of the two applied.
    /// </summary>
    public const string ConflictOfInterestMessage = "This person has a conflict of interest with this case. Choose another investigator.";

    /// <summary>The statuses of an open case (submitted, not yet resolved, closed or rejected).</summary>
    public static IReadOnlyList<GrievanceStatus> OpenStatuses { get; } =
    [
        GrievanceStatus.Submitted,
        GrievanceStatus.Acknowledged,
        GrievanceStatus.UnderReview,
        GrievanceStatus.Assigned,
        GrievanceStatus.UnderInvestigation,
        GrievanceStatus.AwaitingEmployeeResponse,
        GrievanceStatus.ResolutionProposed,
        GrievanceStatus.Reopened,
    ];

    private static readonly GrievanceStatus[] LeadAssignable = [GrievanceStatus.Acknowledged, GrievanceStatus.UnderReview, GrievanceStatus.Reopened];

    private static readonly GrievanceStatus[] LeadReplaceable =
        [GrievanceStatus.Assigned, GrievanceStatus.UnderInvestigation, GrievanceStatus.AwaitingEmployeeResponse, GrievanceStatus.ResolutionProposed];

    private static readonly GrievanceStatus[] Rejectable = [GrievanceStatus.Submitted, GrievanceStatus.Acknowledged, GrievanceStatus.UnderReview];

    private static readonly GrievanceStatus[] InfoRequestable = [GrievanceStatus.UnderReview, GrievanceStatus.Assigned, GrievanceStatus.UnderInvestigation];

    private static readonly GrievanceStatus[] Reopenable = [GrievanceStatus.Resolved, GrievanceStatus.Rejected, GrievanceStatus.Closed];

    /// <summary>True for an open status (<see cref="OpenStatuses"/>).</summary>
    public static bool IsActive(GrievanceStatus status) => OpenStatuses.Contains(status);

    /// <summary>The case waits for the submitter: their next message is the response (<see cref="Respond"/>), not a plain message.</summary>
    public bool AwaitsSubmitterResponse => Status == GrievanceStatus.AwaitingEmployeeResponse;

    /// <summary>
    /// The actor as recorded on this case: the submitter becomes <see cref="GrievanceActor.AnonymousSubmitter"/> on an anonymous case and
    /// <see cref="GrievanceActor.NamedSubmitter"/> on a named one (no account, no employee, no name); anybody else is kept.
    /// </summary>
    public GrievanceActor RecordedActor(GrievanceActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return actor.Recorded(IsAnonymous);
    }

    // ---- Submitter ----

    /// <summary>
    /// The submitter submits the draft: Draft to Submitted with the case number (<see cref="FormatCaseNo"/>). Needs the accepted
    /// declaration, a draft that is still valid (<see cref="ValidateDraft"/>: an active category, anonymity only where allowed). A named
    /// case copies the submitter's placement; an anonymous case keeps none.
    /// </summary>
    public Result<GrievanceStep> Submit(
        GrievanceCategory category,
        bool declarationAccepted,
        string caseNo,
        GrievancePlacement? placement,
        GrievanceActor actor,
        DateOnly today,
        DateTime occurredAtUtc)
    {
        RequireSubmitter(actor);
        if (!IsValidCaseNo(caseNo))
        {
            throw new ArgumentException("A case number is GRV-yyyy-nnnnn.", nameof(caseNo));
        }

        var submittable = CheckSubmission(category, declarationAccepted, today);
        if (submittable.IsFailure)
        {
            return Fail(submittable);
        }

        return Move(GrievanceHistoryAction.Submit, GrievanceStatus.Submitted, actor, null, visible: true, occurredAtUtc, step =>
        {
            CaseNo = caseNo;
            SubmittedAtUtc = occurredAtUtc;
            DeclarationAcceptedAtUtc = occurredAtUtc;
            var copied = IsAnonymous ? null : placement;
            CompanyId = copied?.CompanyId;
            BranchId = copied?.BranchId;
            DepartmentId = copied?.DepartmentId;
            PositionId = copied?.PositionId;
            return step;
        });
    }

    /// <summary>
    /// Everything <see cref="Submit"/> checks, without changing anything: a draft (409 otherwise), still valid with the full minimum
    /// lengths, an active category that allows its anonymity, and the accepted declaration (400). Lets the caller refuse before it takes a
    /// case number.
    /// </summary>
    public Result CheckSubmission(GrievanceCategory category, bool declarationAccepted, DateOnly today)
    {
        var open = Ensure("only a draft can be submitted", GrievanceStatus.Draft);
        if (open.IsFailure)
        {
            return open;
        }

        return DraftViolations(CurrentDetails(), category, today, forSubmission: true)
            .AddIf(!declarationAccepted, "declarationAccepted", "Confirm the declaration to submit the grievance.")
            .ToResult();
    }

    /// <summary>
    /// The submitter answers a request for information: AwaitingEmployeeResponse back to <see cref="ReturnStatus"/>, with the answer
    /// as an InfoResponse message (1 to 4000 characters).
    /// </summary>
    public Result<GrievanceStep> Respond(string? message, int? inReplyToId, GrievanceActor actor, DateTime occurredAtUtc)
    {
        RequireSubmitter(actor);
        var open = Ensure("only a case awaiting your response can be answered", GrievanceStatus.AwaitingEmployeeResponse);
        if (open.IsFailure)
        {
            return Fail(open);
        }

        var body = GrievanceCommunication.CheckBody(message, GrievanceMessageKind.InfoResponse);
        if (body.IsFailure)
        {
            return Fail(body);
        }

        var target = ReturnStatus!.Value;
        return Move(GrievanceHistoryAction.Respond, target, actor, null, visible: true, occurredAtUtc, step =>
        {
            ReturnStatus = null;
            return step with
            {
                Message = GrievanceCommunication.Create(
                    Id, GrievanceMessageDirection.FromSubmitter, GrievanceMessageKind.InfoResponse, message!, RecordedActor(actor), inReplyToId, occurredAtUtc),
            };
        });
    }

    /// <summary>
    /// A plain message from the submitter (no status change, and the case row is not touched). Not on a draft, a closed or an archived
    /// case; while the case awaits the submitter the message is the response (<see cref="Respond"/>), so it is refused here.
    /// </summary>
    public Result<GrievanceCommunication> PostSubmitterMessage(string? message, GrievanceActor actor, DateTime occurredAtUtc)
    {
        RequireSubmitter(actor);
        var open = EnsureMessagesOpen();
        if (open.IsFailure)
        {
            return Result.Failure<GrievanceCommunication>(open.Error);
        }

        if (AwaitsSubmitterResponse)
        {
            return Result.Failure<GrievanceCommunication>(Conflict("the case is waiting for your response, so your message must be sent as the response"));
        }

        var body = GrievanceCommunication.CheckBody(message, GrievanceMessageKind.Message);
        return body.IsFailure
            ? Result.Failure<GrievanceCommunication>(body.Error)
            : Result.Success(GrievanceCommunication.Create(
                Id, GrievanceMessageDirection.FromSubmitter, GrievanceMessageKind.Message, message!, RecordedActor(actor), null, occurredAtUtc));
    }

    // ---- Case team ----

    /// <summary>
    /// HR acknowledges the case: Submitted to Acknowledged with a priority and a target resolution date (by default today plus the
    /// category's <see cref="GrievanceCategory.DefaultTargetDays"/>; never before today). A Critical case is escalated at once: the
    /// step then also has the escalation and its own history entry.
    /// </summary>
    public Result<GrievanceStep> Acknowledge(
        GrievancePriority priority,
        DateOnly? targetResolutionDate,
        GrievanceCategory category,
        GrievanceActor actor,
        DateOnly today,
        DateTime occurredAtUtc,
        string? comment = null)
    {
        ArgumentNullException.ThrowIfNull(category);
        var userId = RequireTeamUser(actor);
        if (category.Id != CategoryId)
        {
            throw new ArgumentException("The category must be the case's own.", nameof(category));
        }

        var open = Ensure("only a submitted case can be acknowledged", GrievanceStatus.Submitted);
        if (open.IsFailure)
        {
            return Fail(open);
        }

        var target = targetResolutionDate ?? today.AddDays(category.DefaultTargetDays);
        if (target < today)
        {
            return Result.Failure<GrievanceStep>(AppError.Validation(
                "The target resolution date cannot be in the past.", "targetResolutionDate", "Choose today or a later date."));
        }

        var entry = Entry(GrievanceHistoryAction.Acknowledge, GrievanceStatus.Acknowledged, actor, comment, visible: true, occurredAtUtc);
        if (entry.IsFailure)
        {
            return Fail(entry);
        }

        Status = GrievanceStatus.Acknowledged;
        Priority = priority;
        TargetResolutionDate = target;
        AcknowledgedAtUtc = occurredAtUtc;
        LastSubmitterUpdateAtUtc = occurredAtUtc;
        var step = new GrievanceStep(entry.Value);
        // A case that is already escalated (by hand, or by triage to Critical before it was acknowledged) is not escalated twice: the
        // database allows one open escalation per case.
        if (priority != GrievancePriority.Critical || IsEscalated)
        {
            return Result.Success(step);
        }

        var escalationEntry = Entry(GrievanceHistoryAction.Escalate, Status, actor, CriticalEscalationReason, visible: false, occurredAtUtc).Value;
        IsEscalated = true;
        return Result.Success(step with
        {
            EscalationEntry = escalationEntry,
            Escalation = GrievanceEscalation.Create(Id, CriticalEscalationReason, isImmediate: true, userId, occurredAtUtc),
        });
    }

    /// <summary>HR rejects the case with a reason (1 to 1000 characters): Submitted, Acknowledged or UnderReview to Rejected.</summary>
    public Result<GrievanceStep> Reject(string? reason, GrievanceActor actor, DateTime occurredAtUtc)
    {
        RequireTeamUser(actor);
        var open = Ensure("only a Submitted, Acknowledged or UnderReview case can be rejected", Rejectable);
        if (open.IsFailure)
        {
            return Fail(open);
        }

        return Move(GrievanceHistoryAction.Reject, GrievanceStatus.Rejected, actor, reason, visible: true, occurredAtUtc, step =>
        {
            RejectionReason = step.Entry.Reason;
            LastSubmitterUpdateAtUtc = occurredAtUtc;
            return step;
        });
    }

    /// <summary>HR reviews the case itself: Acknowledged or Reopened to UnderReview.</summary>
    public Result<GrievanceStep> StartReview(GrievanceActor actor, DateTime occurredAtUtc, string? comment = null)
    {
        RequireTeamUser(actor);
        var open = Ensure("only an Acknowledged or Reopened case can be put under review", GrievanceStatus.Acknowledged, GrievanceStatus.Reopened);
        return open.IsFailure ? Fail(open) : Move(GrievanceHistoryAction.StartReview, GrievanceStatus.UnderReview, actor, comment, visible: true, occurredAtUtc, Touch(occurredAtUtc));
    }

    /// <summary>
    /// HR assigns an investigator. A Lead on an Acknowledged, UnderReview or Reopened case moves it to Assigned; a Lead on a case that
    /// already has one replaces it (a reason is required; the old assignment is ended and the status is kept, also while it is being
    /// investigated). A Member is added without a status change on any open, acknowledged case. Never the case's subject, never the
    /// named submitter (<paramref name="namedSubmitterEmployeeId"/>; null for an anonymous case, whose submitter is not checked here so
    /// that a refusal can never reveal them), never somebody already assigned. <paramref name="activeAssignments"/> are the case's
    /// active assignments.
    /// </summary>
    public Result<GrievanceStep> Assign(
        int investigatorEmployeeId,
        GrievanceAssignmentRole role,
        IReadOnlyCollection<GrievanceAssignment> activeAssignments,
        int? namedSubmitterEmployeeId,
        string? reason,
        GrievanceActor actor,
        DateTime occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(activeAssignments);
        var userId = RequireTeamUser(actor);
        if (activeAssignments.Any(assignment => assignment.CaseId != Id || !assignment.IsActive))
        {
            throw new ArgumentException("Pass the active assignments of this case only.", nameof(activeAssignments));
        }

        var archived = EnsureNotArchived();
        if (archived.IsFailure)
        {
            return Fail(archived);
        }

        var currentLead = activeAssignments.SingleOrDefault(assignment => assignment.Role == GrievanceAssignmentRole.Lead);
        var replacesLead = role == GrievanceAssignmentRole.Lead && currentLead is not null;
        var target = Status;
        if (role == GrievanceAssignmentRole.Lead)
        {
            if (LeadAssignable.Contains(Status))
            {
                target = GrievanceStatus.Assigned;
            }
            else if (!(replacesLead && LeadReplaceable.Contains(Status)))
            {
                return Result.Failure<GrievanceStep>(Refusal(
                    "a lead investigator is assigned while it is Acknowledged, UnderReview or Reopened, and replaced while it is being investigated"));
            }
        }
        else if (Status is GrievanceStatus.Draft or GrievanceStatus.Submitted || !IsActive(Status))
        {
            return Result.Failure<GrievanceStep>(Refusal("investigators are added to an acknowledged, open case only"));
        }

        var cleanReason = TextNormalization.CleanOptional(reason);
        var violations = new Violations()
            .AddIf(
                investigatorEmployeeId == SubjectEmployeeId || investigatorEmployeeId == namedSubmitterEmployeeId,
                "investigatorEmployeeId",
                ConflictOfInterestMessage)
            .AddIf(replacesLead && cleanReason is null, "reason", "Enter why the lead investigator is replaced.")
            .AddIf(cleanReason is { Length: > GrievanceAssignment.ReasonMaxLength }, "reason", $"The reason must be at most {GrievanceAssignment.ReasonMaxLength} characters.");
        if (violations.Any)
        {
            return Result.Failure<GrievanceStep>(violations.ToError());
        }

        if (activeAssignments.Any(assignment => assignment.InvestigatorEmployeeId == investigatorEmployeeId))
        {
            return Result.Failure<GrievanceStep>(AppError.Conflict("This investigator is already assigned to the case."));
        }

        var changesStatus = target != Status;
        return Move(GrievanceHistoryAction.Assign, target, actor, cleanReason, visible: changesStatus, occurredAtUtc, step =>
        {
            if (changesStatus)
            {
                LastSubmitterUpdateAtUtc = occurredAtUtc;
            }

            if (replacesLead)
            {
                currentLead!.End(userId, occurredAtUtc);
            }

            return step with
            {
                Assignment = GrievanceAssignment.Create(Id, investigatorEmployeeId, role, userId, cleanReason, occurredAtUtc),
                EndedAssignment = replacesLead ? currentLead : null,
            };
        });
    }

    /// <summary>
    /// HR removes a Member from an open case (no status change). The active Lead cannot be unassigned, only replaced by assigning a new
    /// Lead (<see cref="Assign"/>).
    /// </summary>
    public Result<GrievanceStep> Unassign(GrievanceAssignment assignment, string? reason, GrievanceActor actor, DateTime occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        var userId = RequireTeamUser(actor);
        if (assignment.CaseId != Id)
        {
            throw new ArgumentException("The assignment must belong to this case.", nameof(assignment));
        }

        var open = Ensure("investigators are removed from an open case only", [.. OpenStatuses]);
        if (open.IsFailure)
        {
            return Fail(open);
        }

        if (!assignment.IsActive)
        {
            return Result.Failure<GrievanceStep>(AppError.Conflict("This investigator is no longer assigned to the case."));
        }

        if (assignment.Role == GrievanceAssignmentRole.Lead)
        {
            return Result.Failure<GrievanceStep>(AppError.Conflict("The lead investigator cannot be unassigned. Assign a new lead to replace them."));
        }

        return Move(GrievanceHistoryAction.Unassign, Status, actor, reason, visible: false, occurredAtUtc, step =>
        {
            assignment.End(userId, occurredAtUtc);
            return step with { EndedAssignment = assignment };
        });
    }

    /// <summary>The lead investigator (or HR) starts the investigation: Assigned to UnderInvestigation.</summary>
    public Result<GrievanceStep> StartInvestigation(GrievanceActor actor, DateTime occurredAtUtc, string? comment = null)
    {
        RequireTeamUser(actor);
        var open = Ensure("only an Assigned case can be put under investigation", GrievanceStatus.Assigned);
        return open.IsFailure
            ? Fail(open)
            : Move(GrievanceHistoryAction.StartInvestigation, GrievanceStatus.UnderInvestigation, actor, comment, visible: true, occurredAtUtc, Touch(occurredAtUtc));
    }

    /// <summary>
    /// The case team asks the submitter for more information: UnderReview, Assigned or UnderInvestigation to AwaitingEmployeeResponse,
    /// keeping the current status as <see cref="ReturnStatus"/>. The question (1 to 1000 characters) is both an InfoRequest message and
    /// the reason of the history entry.
    /// </summary>
    public Result<GrievanceStep> RequestInfo(string? message, GrievanceActor actor, DateTime occurredAtUtc)
    {
        RequireTeamUser(actor);
        var open = Ensure("more information can be requested while it is UnderReview, Assigned or UnderInvestigation", InfoRequestable);
        if (open.IsFailure)
        {
            return Fail(open);
        }

        var body = GrievanceCommunication.CheckBody(message, GrievanceMessageKind.InfoRequest);
        if (body.IsFailure)
        {
            return Fail(body);
        }

        var returnTo = Status;
        return Move(GrievanceHistoryAction.RequestInfo, GrievanceStatus.AwaitingEmployeeResponse, actor, message, visible: true, occurredAtUtc, step =>
        {
            ReturnStatus = returnTo;
            LastSubmitterUpdateAtUtc = occurredAtUtc;
            return step with
            {
                Message = GrievanceCommunication.Create(
                    Id, GrievanceMessageDirection.ToSubmitter, GrievanceMessageKind.InfoRequest, message!, actor, null, occurredAtUtc),
            };
        });
    }

    /// <summary>
    /// A plain message from the case team to the submitter (no status change; it updates the submitter's unread badge). Not on a draft,
    /// a closed or an archived case.
    /// </summary>
    public Result<GrievanceCommunication> PostTeamMessage(string? message, GrievanceActor actor, DateTime occurredAtUtc)
    {
        RequireTeamUser(actor);
        var open = EnsureMessagesOpen();
        if (open.IsFailure)
        {
            return Result.Failure<GrievanceCommunication>(open.Error);
        }

        var body = GrievanceCommunication.CheckBody(message, GrievanceMessageKind.Message);
        if (body.IsFailure)
        {
            return Result.Failure<GrievanceCommunication>(body.Error);
        }

        LastSubmitterUpdateAtUtc = occurredAtUtc;
        return Result.Success(GrievanceCommunication.Create(
            Id, GrievanceMessageDirection.ToSubmitter, GrievanceMessageKind.Message, message!, actor, null, occurredAtUtc));
    }

    /// <summary>
    /// The lead investigator (UnderInvestigation) or HR (UnderReview) proposes a resolution (summary of 1 to 4000 characters):
    /// to ResolutionProposed, with a pending proposal for the reviewers.
    /// </summary>
    public Result<GrievanceStep> ProposeResolution(string? summary, GrievanceActor actor, DateTime occurredAtUtc)
    {
        var userId = RequireTeamUser(actor);
        var open = Ensure("a resolution is proposed while it is UnderInvestigation or UnderReview", GrievanceStatus.UnderInvestigation, GrievanceStatus.UnderReview);
        if (open.IsFailure)
        {
            return Fail(open);
        }

        var clean = TextNormalization.CleanOptional(summary);
        var violations = new Violations()
            .AddIf(clean is null, "summary", "Enter the proposed resolution.")
            .AddIf(clean is { Length: > ResolutionSummaryMaxLength }, "summary", $"The resolution must be at most {ResolutionSummaryMaxLength} characters.");
        if (violations.Any)
        {
            return Result.Failure<GrievanceStep>(violations.ToError());
        }

        return Move(GrievanceHistoryAction.ProposeResolution, GrievanceStatus.ResolutionProposed, actor, null, visible: false, occurredAtUtc, step => step with
        {
            Proposal = GrievanceResolutionProposal.Create(Id, clean!, userId, actor.EmployeeId, occurredAtUtc),
        });
    }

    /// <summary>
    /// A reviewer approves the pending proposal: ResolutionProposed to Resolved. The summary is copied to the case and shown to the
    /// submitter when <paramref name="visibleToSubmitter"/>. The reviewer's optional comment (at most 1000 characters) stays on the
    /// proposal. Nobody approves a proposal they made (403).
    /// </summary>
    public Result<GrievanceStep> ApproveResolution(
        GrievanceResolutionProposal proposal,
        bool visibleToSubmitter,
        string? comment,
        GrievanceActor actor,
        DateTime occurredAtUtc)
    {
        var decided = CheckDecision(proposal, actor, comment, commentRequired: false);
        if (decided.IsFailure)
        {
            return Result.Failure<GrievanceStep>(decided.Error);
        }

        return Move(GrievanceHistoryAction.ApproveResolution, GrievanceStatus.Resolved, actor, null, visible: true, occurredAtUtc, step =>
        {
            proposal.Decide(ResolutionDecision.Approved, decided.Value, comment, occurredAtUtc);
            ResolutionSummary = proposal.Summary;
            ResolutionVisibleToSubmitter = visibleToSubmitter;
            ResolvedAtUtc = occurredAtUtc;
            LastSubmitterUpdateAtUtc = occurredAtUtc;
            return step with { Proposal = proposal };
        });
    }

    /// <summary>
    /// A reviewer rejects the pending proposal with a reason: back to UnderInvestigation when the case has an active lead
    /// (<paramref name="hasActiveLead"/>), otherwise UnderReview. Nobody rejects a proposal they made (403).
    /// </summary>
    public Result<GrievanceStep> RejectResolution(
        GrievanceResolutionProposal proposal,
        string? reason,
        bool hasActiveLead,
        GrievanceActor actor,
        DateTime occurredAtUtc)
    {
        var decided = CheckDecision(proposal, actor, reason, commentRequired: true);
        if (decided.IsFailure)
        {
            return Result.Failure<GrievanceStep>(decided.Error);
        }

        var target = hasActiveLead ? GrievanceStatus.UnderInvestigation : GrievanceStatus.UnderReview;
        return Move(GrievanceHistoryAction.RejectResolution, target, actor, reason, visible: false, occurredAtUtc, step =>
        {
            proposal.Decide(ResolutionDecision.Rejected, decided.Value, reason, occurredAtUtc);
            return step with { Proposal = proposal };
        });
    }

    /// <summary>HR closes a resolved case: Resolved to Closed. Closed is final: only reopening and archiving touch it afterwards.</summary>
    public Result<GrievanceStep> Close(GrievanceActor actor, DateTime occurredAtUtc, string? comment = null)
    {
        RequireTeamUser(actor);
        var open = Ensure("only a Resolved case can be closed", GrievanceStatus.Resolved);
        if (open.IsFailure)
        {
            return Fail(open);
        }

        return Move(GrievanceHistoryAction.Close, GrievanceStatus.Closed, actor, comment, visible: true, occurredAtUtc, step =>
        {
            ClosedAtUtc = occurredAtUtc;
            LastSubmitterUpdateAtUtc = occurredAtUtc;
            return step;
        });
    }

    /// <summary>
    /// HR reopens a Resolved, Rejected or Closed case with a reason: to Reopened (a Closed case also needs a reviewer's approval, an
    /// application rule). The resolution and rejection texts are kept; the resolved, closed and retention dates are cleared. Never an
    /// archived case. Every active assignment (<paramref name="activeAssignments"/>, the Lead included) ends in the same step, each with an
    /// Unassign history entry (reason <see cref="ReopenUnassignReason"/>, not shown to the submitter): from Reopened HR starts a review or
    /// assigns a Lead again (the same person may be assigned again).
    /// </summary>
    public Result<GrievanceStep> Reopen(string? reason, IReadOnlyCollection<GrievanceAssignment> activeAssignments, GrievanceActor actor, DateTime occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(activeAssignments);
        var userId = RequireTeamUser(actor);
        if (activeAssignments.Any(assignment => assignment.CaseId != Id || !assignment.IsActive))
        {
            throw new ArgumentException("Pass the active assignments of this case only.", nameof(activeAssignments));
        }

        var open = Ensure("only a Resolved, Rejected or Closed case can be reopened", Reopenable);
        if (open.IsFailure)
        {
            return Fail(open);
        }

        return Move(GrievanceHistoryAction.Reopen, GrievanceStatus.Reopened, actor, reason, visible: true, occurredAtUtc, step =>
        {
            ResolvedAtUtc = null;
            ClosedAtUtc = null;
            RetentionUntil = null;
            LastSubmitterUpdateAtUtc = occurredAtUtc;
            var ended = new List<GrievanceAssignment>();
            var entries = new List<GrievanceStatusHistoryEntry>();
            foreach (var assignment in activeAssignments.OrderBy(assignment => assignment.Id))
            {
                assignment.End(userId, occurredAtUtc);
                ended.Add(assignment);
                entries.Add(Entry(GrievanceHistoryAction.Unassign, Status, actor, ReopenUnassignReason, visible: false, occurredAtUtc).Value);
            }

            return step with { EndedAssignments = ended, FollowUpEntries = entries };
        });
    }

    /// <summary>
    /// HR or an assigned investigator escalates an open case to the reviewers with a reason (no status change). A case is escalated
    /// once: <see cref="IsEscalated"/> stays set for reporting.
    /// </summary>
    public Result<GrievanceStep> Escalate(string? reason, GrievanceActor actor, DateTime occurredAtUtc, bool isImmediate = false)
    {
        var userId = RequireTeamUser(actor);
        var open = Ensure("only an open case can be escalated", [.. OpenStatuses]);
        if (open.IsFailure)
        {
            return Fail(open);
        }

        if (IsEscalated)
        {
            return Result.Failure<GrievanceStep>(AppError.Conflict("This case is already escalated."));
        }

        return Move(GrievanceHistoryAction.Escalate, Status, actor, reason, visible: false, occurredAtUtc, step =>
        {
            IsEscalated = true;
            return step with { Escalation = GrievanceEscalation.Create(Id, step.Entry.Reason!, isImmediate, userId, occurredAtUtc) };
        });
    }

    /// <summary>HR archives (soft-deletes) a Closed or Rejected case that is not on legal hold. The status is kept; nothing changes it afterwards.</summary>
    public Result<GrievanceStep> Archive(GrievanceActor actor, DateTime occurredAtUtc, string? comment = null)
    {
        RequireTeamUser(actor);
        var open = Ensure("only a Closed or Rejected case can be archived", GrievanceStatus.Closed, GrievanceStatus.Rejected);
        if (open.IsFailure)
        {
            return Fail(open);
        }

        if (LegalHold)
        {
            return Result.Failure<GrievanceStep>(AppError.Conflict("This case is on legal hold and cannot be archived."));
        }

        return Move(GrievanceHistoryAction.Archive, Status, actor, comment, visible: false, occurredAtUtc, step =>
        {
            ArchivedAtUtc = occurredAtUtc;
            return step;
        });
    }

    /// <summary>
    /// HR's triage of an open case: the priority, the target date (not moved into the past), and the employee the case is about (never
    /// the named submitter). No history entry (the access log records it). Not on a closed, resolved, rejected or archived case.
    /// </summary>
    public Result Triage(
        GrievancePriority? priority,
        DateOnly? targetResolutionDate,
        int? subjectEmployeeId,
        int? involvedDepartmentId,
        int? namedSubmitterEmployeeId,
        DateOnly today)
    {
        var open = Ensure("only an open case can be triaged", [.. OpenStatuses]);
        if (open.IsFailure)
        {
            return open;
        }

        var violations = new Violations()
            .AddIf(
                targetResolutionDate is { } target && target != TargetResolutionDate && target < today,
                "targetResolutionDate",
                "Choose today or a later date.",
                "The target resolution date cannot be in the past.")
            .AddIf(
                subjectEmployeeId is not null && subjectEmployeeId == namedSubmitterEmployeeId,
                "subjectEmployeeId",
                "The submitter cannot be the subject of their own grievance.")
            .AddIf(Status != GrievanceStatus.Submitted && priority is null, "priority", "An acknowledged case keeps a priority.")
            .AddIf(
                AcknowledgedAtUtc is not null && targetResolutionDate is null,
                "targetResolutionDate",
                "An acknowledged case keeps a target resolution date.");
        if (violations.Any)
        {
            return violations.ToResult();
        }

        Priority = priority;
        TargetResolutionDate = targetResolutionDate;
        SubjectEmployeeId = subjectEmployeeId;
        InvolvedDepartmentId = involvedDepartmentId;
        return Result.Success();
    }

    /// <summary>
    /// Places or lifts a legal hold (a held case is never archived or purged). Allowed on any submitted case, also a Closed or an
    /// archived one (the database lets the hold change on a Closed case). No history entry; the access log records it.
    /// </summary>
    public Result SetLegalHold(bool onHold)
    {
        if (Status == GrievanceStatus.Draft)
        {
            return Refuse("a draft cannot be put on legal hold");
        }

        LegalHold = onHold;
        return Result.Success();
    }

    /// <summary>
    /// HR moves an open case to another category at triage. The new category must be active, and an anonymous case stays anonymous, so
    /// its new category must allow that. Keeping the current category always succeeds. No history entry; the access log records it.
    /// </summary>
    public Result Reclassify(GrievanceCategory category)
    {
        ArgumentNullException.ThrowIfNull(category);
        var open = Ensure("only an open case can be moved to another category", [.. OpenStatuses]);
        if (open.IsFailure || category.Id == CategoryId)
        {
            return open;
        }

        var violations = new Violations()
            .AddIf(!category.IsActive, "categoryId", "Choose an active category.")
            .AddIf(IsAnonymous && !category.AllowsAnonymous, "categoryId", "This case is anonymous; choose a category that allows anonymous grievances.");
        if (violations.Any)
        {
            return violations.ToResult();
        }

        CategoryId = category.Id;
        return Result.Success();
    }

    /// <summary>
    /// Sets how long a finished case is kept: <paramref name="retentionYears"/> years from <paramref name="finishedOn"/>, the day it was
    /// closed or rejected. Only a Closed or Rejected case has a retention date (reopening clears it).
    /// </summary>
    public void ApplyRetention(DateOnly finishedOn, int retentionYears)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(retentionYears);
        if (Status is not (GrievanceStatus.Closed or GrievanceStatus.Rejected))
        {
            throw new InvalidOperationException("Only a closed or rejected case has a retention date.");
        }

        RetentionUntil = finishedOn.AddYears(retentionYears);
    }

    /// <summary>
    /// The case team added something the submitter sees without a status change (a Shared file): the submitter's unread badge moves.
    /// Only on an open case (files are added to open cases only).
    /// </summary>
    public Result RecordSubmitterVisibleChange(DateTime occurredAtUtc)
    {
        var open = Ensure("the submitter is told about changes to an open case only", [.. OpenStatuses]);
        if (open.IsSuccess)
        {
            LastSubmitterUpdateAtUtc = occurredAtUtc;
        }

        return open;
    }

    /// <summary>Notes are added to an open, not archived case only (409 otherwise).</summary>
    public Result EnsureInvestigationOpen() => Ensure("notes are added to an open case only", [.. OpenStatuses]);

    /// <summary>Files are added to an open case, or by the submitter to their draft (409 otherwise).</summary>
    public Result EnsureFilesOpen() => Ensure("files are added to a draft or an open case only", [GrievanceStatus.Draft, .. OpenStatuses]);

    // ---- Helpers ----

    private static void RequireSubmitter(GrievanceActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (!actor.IsSubmitter)
        {
            throw new ArgumentException("Only the submitter takes this step.", nameof(actor));
        }
    }

    private static int RequireTeamUser(GrievanceActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (actor.IsSubmitter || actor.UserId is not { } userId)
        {
            throw new ArgumentException("A case-team step is taken by a signed-in member of the case team.", nameof(actor));
        }

        return userId;
    }

    private static Result<GrievanceStep> Fail(Result failed) => Result.Failure<GrievanceStep>(failed.Error);

    private Result<int> CheckDecision(GrievanceResolutionProposal proposal, GrievanceActor actor, string? comment, bool commentRequired)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        var userId = RequireTeamUser(actor);
        if (proposal.CaseId != Id)
        {
            throw new ArgumentException("The proposal must belong to this case.", nameof(proposal));
        }

        var open = Ensure("only a proposed resolution can be decided", GrievanceStatus.ResolutionProposed);
        if (open.IsFailure)
        {
            return Result.Failure<int>(open.Error);
        }

        if (!proposal.IsPending)
        {
            return Result.Failure<int>(AppError.Conflict("This proposal has already been decided."));
        }

        if (proposal.ProposedByUserId == userId)
        {
            return Result.Failure<int>(AppError.Forbidden("You cannot decide a resolution you proposed."));
        }

        var clean = TextNormalization.CleanOptional(comment);
        var violations = new Violations()
            .AddIf(commentRequired && clean is null, "reason", "A reason is required.", "Enter a reason for rejecting the proposed resolution.")
            .AddIf(
                clean is { Length: > GrievanceResolutionProposal.CommentMaxLength },
                commentRequired ? "reason" : "comment",
                $"The comment must be at most {GrievanceResolutionProposal.CommentMaxLength} characters.");
        return violations.Any ? Result.Failure<int>(violations.ToError()) : Result.Success(userId);
    }

    private Result EnsureMessagesOpen()
    {
        var archived = EnsureNotArchived();
        if (archived.IsFailure)
        {
            return archived;
        }

        return Status is GrievanceStatus.Draft or GrievanceStatus.Closed
            ? Refuse("messages are exchanged on a submitted case that is not closed")
            : Result.Success();
    }

    /// <summary>The status is one of <paramref name="allowed"/> and the case is not archived; a Conflict naming the status otherwise.</summary>
    private Result Ensure(string what, params GrievanceStatus[] allowed)
    {
        var archived = EnsureNotArchived();
        if (archived.IsFailure)
        {
            return archived;
        }

        return allowed.Contains(Status) ? Result.Success() : Refuse(what);
    }

    private Result EnsureNotArchived() => IsArchived
        ? Result.Failure(AppError.Conflict("The case is archived and can no longer be changed."))
        : Result.Success();

    private Func<GrievanceStep, GrievanceStep> Touch(DateTime occurredAtUtc) => step =>
    {
        LastSubmitterUpdateAtUtc = occurredAtUtc;
        return step;
    };

    /// <summary>Creates the history entry of a move from the current status; when it is valid, moves to the target and applies the rest.</summary>
    private Result<GrievanceStep> Move(
        GrievanceHistoryAction action,
        GrievanceStatus target,
        GrievanceActor actor,
        string? reason,
        bool visible,
        DateTime occurredAtUtc,
        Func<GrievanceStep, GrievanceStep> apply)
    {
        var entry = Entry(action, target, actor, reason, visible, occurredAtUtc);
        if (entry.IsFailure)
        {
            return Fail(entry);
        }

        Status = target;
        return Result.Success(apply(new GrievanceStep(entry.Value)));
    }

    private Result<GrievanceStatusHistoryEntry> Entry(
        GrievanceHistoryAction action,
        GrievanceStatus target,
        GrievanceActor actor,
        string? reason,
        bool visible,
        DateTime occurredAtUtc) =>
        GrievanceStatusHistoryEntry.Create(Id, Status, target, action, RecordedActor(actor), reason, visible, occurredAtUtc);

    private AppError Refusal(string what) => Conflict(what);

    private AppError Conflict(string what) => AppError.Conflict($"The case is {GrievanceText.Status.ToText(Status)}; {what}.");

    private Result Refuse(string what) => Result.Failure(Conflict(what));
}
