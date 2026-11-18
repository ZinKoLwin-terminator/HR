using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Grievances;

/// <summary>
/// Who acted on a case: the account, the employee linked to it (none for the built-in administrator), the display name and the
/// capacity. A submitter is never identified in what the case records (history, messages, files, access log): the submitter of a
/// named case is recorded as <see cref="NamedSubmitter"/> (the screens show the name from grv.CaseSubmitter) and the submitter of an
/// anonymous case as <see cref="AnonymousSubmitter"/>, whatever is passed in (see <see cref="Recorded"/>).
/// </summary>
public sealed record GrievanceActor(int? UserId, int? EmployeeId, string Name, GrievanceActorRole Role)
{
    public const string AnonymousSubmitterName = "Anonymous submitter";

    public const string NamedSubmitterName = "Submitter";

    /// <summary>The submitter of an anonymous case: no account, no employee.</summary>
    public static GrievanceActor AnonymousSubmitter { get; } = new(null, null, AnonymousSubmitterName, GrievanceActorRole.Submitter);

    /// <summary>The submitter of a named case as the case records it: no account, no employee (CaseSubmitter holds who it is).</summary>
    public static GrievanceActor NamedSubmitter { get; } = new(null, null, NamedSubmitterName, GrievanceActorRole.Submitter);

    public bool IsSubmitter => Role == GrievanceActorRole.Submitter;

    /// <summary>True for the two names only a submitter is recorded under (CK_StatusHistory_SubmitterActor, CK_Communication_SubmitterAuthor).</summary>
    public static bool IsSubmitterName(string? name) =>
        string.Equals(name?.Trim(), AnonymousSubmitterName, StringComparison.Ordinal) || string.Equals(name?.Trim(), NamedSubmitterName, StringComparison.Ordinal);

    /// <summary>The actor as a case of the given kind records it: a submitter loses the account, the employee and the name.</summary>
    public GrievanceActor Recorded(bool isAnonymousCase) =>
        !IsSubmitter ? this : isAnonymousCase ? AnonymousSubmitter : NamedSubmitter;

    /// <summary>
    /// Throws unless the actor may be written to a grievance row as it is: a submitter only as <see cref="NamedSubmitter"/> or
    /// <see cref="AnonymousSubmitter"/>, anybody else never under one of those names.
    /// </summary>
    internal void EnsureRecordable(string parameterName)
    {
        if (IsSubmitter ? this != NamedSubmitter && this != AnonymousSubmitter : IsSubmitterName(Name))
        {
            throw new ArgumentException("A submitter is recorded without an account, an employee or a name; nobody else under a submitter's name.", parameterName);
        }
    }
}

/// <summary>
/// One transition of a case (grv.StatusHistory). Append-only: the database user has no UPDATE or DELETE on the table. Reject,
/// Reopen, Escalate, RejectResolution and RequestInfo need a reason (as CK_StatusHistory_Reason).
/// </summary>
public sealed class GrievanceStatusHistoryEntry : Entity
{
    public const int ActorNameMaxLength = 130;
    public const int ReasonMaxLength = 1000;

    private static readonly GrievanceHistoryAction[] NeedsReason =
    [
        GrievanceHistoryAction.Reject,
        GrievanceHistoryAction.Reopen,
        GrievanceHistoryAction.Escalate,
        GrievanceHistoryAction.RejectResolution,
        GrievanceHistoryAction.RequestInfo,
    ];

    private GrievanceStatusHistoryEntry()
    {
    }

    public int CaseId { get; private set; }

    public GrievanceStatus FromStatus { get; private set; }

    public GrievanceStatus ToStatus { get; private set; }

    public GrievanceHistoryAction Action { get; private set; }

    public int? ActorUserId { get; private set; }

    public int? ActorEmployeeId { get; private set; }

    public string ActorName { get; private set; } = string.Empty;

    public GrievanceActorRole ActorRole { get; private set; }

    public string? Reason { get; private set; }

    /// <summary>Shown on the submitter's timeline (with the actor shown as "HR team", never by name).</summary>
    public bool VisibleToSubmitter { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    /// <summary>True for the actions that need a reason.</summary>
    public static bool RequiresReason(GrievanceHistoryAction action) => NeedsReason.Contains(action);

    /// <summary>A history entry, or a Validation failure on <c>reason</c> when a needed reason is missing or longer than 1000 characters.</summary>
    public static Result<GrievanceStatusHistoryEntry> Create(
        int caseId,
        GrievanceStatus fromStatus,
        GrievanceStatus toStatus,
        GrievanceHistoryAction action,
        GrievanceActor actor,
        string? reason,
        bool visibleToSubmitter,
        DateTime occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(actor);
        actor.EnsureRecordable(nameof(actor));

        var cleanReason = TextNormalization.CleanOptional(reason);
        var violations = new Violations()
            .Require("actorName", "Actor name", actor.Name)
            .AddIf(actor.Name?.Trim().Length > ActorNameMaxLength, "actorName", $"Actor name must be at most {ActorNameMaxLength} characters.")
            .AddIf(RequiresReason(action) && cleanReason is null, "reason", "A reason is required.", "Enter a reason.")
            .AddIf(cleanReason is { Length: > ReasonMaxLength }, "reason", $"The reason must be at most {ReasonMaxLength} characters.");
        if (violations.Any)
        {
            return Result.Failure<GrievanceStatusHistoryEntry>(violations.ToError());
        }

        return Result.Success(new GrievanceStatusHistoryEntry
        {
            CaseId = caseId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            Action = action,
            ActorUserId = actor.UserId,
            ActorEmployeeId = actor.EmployeeId,
            ActorName = actor.Name!.Trim(),
            ActorRole = actor.Role,
            Reason = cleanReason,
            VisibleToSubmitter = visibleToSubmitter,
            OccurredAtUtc = occurredAtUtc,
        });
    }
}

/// <summary>
/// What one workflow step produced: the history entry, and the rows that belong to it (an escalation with its own history entry,
/// a message, an assignment and the lead assignment it ended, a resolution proposal). The caller adds the new rows and saves
/// everything in one unit of work.
/// </summary>
public sealed record GrievanceStep(GrievanceStatusHistoryEntry Entry)
{
    /// <summary>A Critical case is escalated when acknowledged: the escalation's own history entry.</summary>
    public GrievanceStatusHistoryEntry? EscalationEntry { get; init; }

    public GrievanceEscalation? Escalation { get; init; }

    public GrievanceCommunication? Message { get; init; }

    public GrievanceAssignment? Assignment { get; init; }

    /// <summary>The lead assignment that a new Lead replaced (ended, already tracked by the caller).</summary>
    public GrievanceAssignment? EndedAssignment { get; init; }

    public GrievanceResolutionProposal? Proposal { get; init; }

    /// <summary>The assignments a reopening ended (already tracked by the caller), in the order of their history entries.</summary>
    public IReadOnlyList<GrievanceAssignment> EndedAssignments { get; init; } = [];

    /// <summary>History entries that follow the step's own (the Unassign entries of a reopening).</summary>
    public IReadOnlyList<GrievanceStatusHistoryEntry> FollowUpEntries { get; init; } = [];

    /// <summary>Every history entry of the step, in order.</summary>
    public IReadOnlyList<GrievanceStatusHistoryEntry> History =>
        [Entry, .. EscalationEntry is null ? Array.Empty<GrievanceStatusHistoryEntry>() : [EscalationEntry], .. FollowUpEntries];
}
