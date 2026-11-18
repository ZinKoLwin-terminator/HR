using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Grievances;

/// <summary>
/// Who submitted a case (grv.CaseSubmitter), kept apart from the case. A named case stores the employee; an anonymous case only the
/// keyed pseudonym (HMACSHA256 of the employee id), never the id. <see cref="SubmitterSeenAtUtc"/> is the submitter's read receipt:
/// it lives here so that reading a case never changes the case row (its row version, its history).
/// </summary>
public sealed class GrievanceCaseSubmitter
{
    public const int PseudonymLength = 32;

    private GrievanceCaseSubmitter()
    {
    }

    public int CaseId { get; private set; }

    public int? EmployeeId { get; private set; }

    public byte[]? SubmitterPseudonym { get; private set; }

    public DateTime? SubmitterSeenAtUtc { get; private set; }

    public bool IsAnonymous => SubmitterPseudonym is not null;

    /// <summary>The named submitter of a case.</summary>
    public static GrievanceCaseSubmitter Named(int caseId, int employeeId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(employeeId);
        return new GrievanceCaseSubmitter { CaseId = caseId, EmployeeId = employeeId };
    }

    /// <summary>The anonymous submitter of a case: only the 32-byte pseudonym (a copy is kept).</summary>
    public static GrievanceCaseSubmitter Anonymous(int caseId, byte[] pseudonym)
    {
        ArgumentNullException.ThrowIfNull(pseudonym);
        if (pseudonym.Length != PseudonymLength)
        {
            throw new ArgumentException($"A pseudonym is {PseudonymLength} bytes.", nameof(pseudonym));
        }

        return new GrievanceCaseSubmitter { CaseId = caseId, SubmitterPseudonym = [.. pseudonym] };
    }

    /// <summary>True when the employee (with their pseudonym) is this submitter: by id on a named case, by pseudonym on an anonymous one.</summary>
    public bool Is(int employeeId, ReadOnlySpan<byte> pseudonymOfEmployee) =>
        SubmitterPseudonym is { } stored
            ? stored.AsSpan().SequenceEqual(pseudonymOfEmployee)
            : EmployeeId == employeeId;

    /// <summary>Records that the submitter looked at the case now (only moves forward).</summary>
    public void MarkSeen(DateTime nowUtc)
    {
        if (SubmitterSeenAtUtc is null || nowUtc > SubmitterSeenAtUtc)
        {
            SubmitterSeenAtUtc = nowUtc;
        }
    }

    /// <summary>True when the case team changed something the submitter may see after the submitter last looked.</summary>
    public bool HasUnseenUpdate(DateTime? lastSubmitterUpdateAtUtc) =>
        lastSubmitterUpdateAtUtc is { } updated && (SubmitterSeenAtUtc is null || updated > SubmitterSeenAtUtc);
}

/// <summary>An investigator on a case (grv.Assignment). Unassigning ends the row; it is never deleted.</summary>
public sealed class GrievanceAssignment : Entity
{
    public const int ReasonMaxLength = 1000;

    private GrievanceAssignment()
    {
    }

    public int CaseId { get; private set; }

    public int InvestigatorEmployeeId { get; private set; }

    public GrievanceAssignmentRole Role { get; private set; }

    public DateTime AssignedAtUtc { get; private set; }

    public int AssignedByUserId { get; private set; }

    public DateTime? UnassignedAtUtc { get; private set; }

    public int? UnassignedByUserId { get; private set; }

    public string? Reason { get; private set; }

    public bool IsActive => UnassignedAtUtc is null;

    internal static GrievanceAssignment Create(
        int caseId,
        int investigatorEmployeeId,
        GrievanceAssignmentRole role,
        int assignedByUserId,
        string? reason,
        DateTime assignedAtUtc) => new()
        {
            CaseId = caseId,
            InvestigatorEmployeeId = investigatorEmployeeId,
            Role = role,
            AssignedByUserId = assignedByUserId,
            AssignedAtUtc = assignedAtUtc,
            Reason = TextNormalization.CleanOptional(reason),
        };

    internal void End(int byUserId, DateTime atUtc)
    {
        UnassignedByUserId = byUserId;
        UnassignedAtUtc = atUtc < AssignedAtUtc ? AssignedAtUtc : atUtc;
    }
}

/// <summary>
/// A message between the case team and the submitter (grv.Communication), including information requests and the answers.
/// Append-only. A submitter's message keeps no ids: "Submitter" on a named case, "Anonymous submitter" on an anonymous one.
/// </summary>
public sealed class GrievanceCommunication : Entity
{
    public const int BodyMaxLength = 4000;

    /// <summary>A request for information doubles as the reason of its history entry, so it is shorter.</summary>
    public const int InfoRequestMaxLength = GrievanceStatusHistoryEntry.ReasonMaxLength;

    private GrievanceCommunication()
    {
    }

    public int CaseId { get; private set; }

    public GrievanceMessageDirection Direction { get; private set; }

    public GrievanceMessageKind Kind { get; private set; }

    public string Body { get; private set; } = string.Empty;

    public GrievanceActorRole AuthorRole { get; private set; }

    public int? AuthorUserId { get; private set; }

    public int? AuthorEmployeeId { get; private set; }

    public string AuthorName { get; private set; } = string.Empty;

    public int? InReplyToId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>The text rules of a message body: 1 to 4000 characters, 1 to 1000 for an information request.</summary>
    public static Result CheckBody(string? body, GrievanceMessageKind kind, string field = "message")
    {
        var clean = TextNormalization.CleanOptional(body);
        var max = kind == GrievanceMessageKind.InfoRequest ? InfoRequestMaxLength : BodyMaxLength;
        return new Violations()
            .AddIf(clean is null, field, "Enter a message.")
            .AddIf(clean is { } text && text.Length > max, field, $"The message must be at most {max} characters.")
            .ToResult();
    }

    internal static GrievanceCommunication Create(
        int caseId,
        GrievanceMessageDirection direction,
        GrievanceMessageKind kind,
        string body,
        GrievanceActor author,
        int? inReplyToId,
        DateTime createdAtUtc)
    {
        author.EnsureRecordable(nameof(author));
        return new()
        {
            CaseId = caseId,
            Direction = direction,
            Kind = kind,
            Body = body.Trim(),
            AuthorRole = author.Role,
            AuthorUserId = author.UserId,
            AuthorEmployeeId = author.EmployeeId,
            AuthorName = author.Name.Trim(),
            InReplyToId = kind == GrievanceMessageKind.InfoResponse ? inReplyToId : null,
            CreatedAtUtc = createdAtUtc,
        };
    }
}

/// <summary>A proposed resolution waiting for a reviewer (grv.ResolutionProposal). Nobody decides their own proposal.</summary>
public sealed class GrievanceResolutionProposal : Entity
{
    public const int SummaryMaxLength = 4000;
    public const int CommentMaxLength = 1000;

    private GrievanceResolutionProposal()
    {
    }

    public int CaseId { get; private set; }

    public string Summary { get; private set; } = string.Empty;

    public int ProposedByUserId { get; private set; }

    public int? ProposedByEmployeeId { get; private set; }

    public DateTime ProposedAtUtc { get; private set; }

    public ResolutionDecision Decision { get; private set; }

    public int? DecidedByUserId { get; private set; }

    public DateTime? DecidedAtUtc { get; private set; }

    public string? DecisionComment { get; private set; }

    public bool IsPending => Decision == ResolutionDecision.Pending;

    internal static GrievanceResolutionProposal Create(int caseId, string summary, int proposedByUserId, int? proposedByEmployeeId, DateTime proposedAtUtc) => new()
    {
        CaseId = caseId,
        Summary = summary.Trim(),
        ProposedByUserId = proposedByUserId,
        ProposedByEmployeeId = proposedByEmployeeId,
        ProposedAtUtc = proposedAtUtc,
        Decision = ResolutionDecision.Pending,
    };

    internal void Decide(ResolutionDecision decision, int decidedByUserId, string? comment, DateTime decidedAtUtc)
    {
        Decision = decision;
        DecidedByUserId = decidedByUserId;
        DecidedAtUtc = decidedAtUtc;
        DecisionComment = TextNormalization.CleanOptional(comment);
    }
}

/// <summary>A case raised to the reviewers (grv.Escalation). The case keeps <see cref="GrievanceCase.IsEscalated"/> for reporting.</summary>
public sealed class GrievanceEscalation : Entity
{
    public const int ReasonMaxLength = 1000;

    private GrievanceEscalation()
    {
    }

    public int CaseId { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public bool IsImmediate { get; private set; }

    public int? EscalatedByUserId { get; private set; }

    public DateTime EscalatedAtUtc { get; private set; }

    public DateTime? ClearedAtUtc { get; private set; }

    public string? Outcome { get; private set; }

    internal static GrievanceEscalation Create(int caseId, string reason, bool isImmediate, int? escalatedByUserId, DateTime escalatedAtUtc) => new()
    {
        CaseId = caseId,
        Reason = reason.Trim(),
        IsImmediate = isImmediate,
        EscalatedByUserId = escalatedByUserId,
        EscalatedAtUtc = escalatedAtUtc,
    };
}
