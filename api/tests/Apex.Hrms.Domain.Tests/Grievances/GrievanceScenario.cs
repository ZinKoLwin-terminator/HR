using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Grievances;

namespace Apex.Hrms.Domain.Tests.Grievances;

/// <summary>
/// A grievance case brought to a status through the real transitions, with its active assignments and pending proposal, as the
/// golden file describes (docs/test-data/grievance-transition-cases.json, <c>_comment</c>). Fictional data only.
/// </summary>
internal sealed class GrievanceScenario
{
    public const int NamedSubmitterEmployeeId = 100;
    public const int LeadEmployeeId = 300;
    public const string CaseNo = "GRV-2026-00001";

    public static readonly DateOnly Today = new(2026, 11, 18);
    public static readonly DateTime Now = new(2026, 11, 18, 8, 0, 0, DateTimeKind.Utc);
    public static readonly GrievancePlacement Placement = new(1, 2, 3, 4);

    public static readonly GrievanceActor Submitter = new(10, 100, "TST Submitter", GrievanceActorRole.Submitter);
    public static readonly GrievanceActor Management = new(20, 200, "TST HR Manager", GrievanceActorRole.Management);
    public static readonly GrievanceActor Investigator = new(30, 300, "TST Investigator", GrievanceActorRole.Investigator);
    public static readonly GrievanceActor Reviewer = new(40, 400, "TST Reviewer", GrievanceActorRole.Reviewer);

    private GrievanceScenario(GrievanceCase @case) => Case = @case;

    public GrievanceCase Case { get; }

    public List<GrievanceAssignment> ActiveAssignments { get; } = [];

    public GrievanceResolutionProposal? Proposal { get; private set; }

    public int? NamedSubmitter => Case.IsAnonymous ? null : NamedSubmitterEmployeeId;

    public bool HasActiveLead => ActiveAssignments.Any(assignment => assignment.Role == GrievanceAssignmentRole.Lead);

    public static GrievanceActor ActorNamed(string name) => name switch
    {
        "submitter" => Submitter,
        "management" => Management,
        "investigator" => Investigator,
        "reviewer" => Reviewer,
        _ => throw new ArgumentException($"Unknown actor '{name}'.", nameof(name)),
    };

    public static GrievanceCategory Category(bool active = true, bool allowsAnonymous = true, short defaultTargetDays = 14)
    {
        var category = GrievanceCategory.Create(
            new GrievanceCategoryDetails("TST-CAT", "TST Category", null, allowsAnonymous, defaultTargetDays, 1),
            RecordStatus.Active).Value;
        if (!active)
        {
            category.Deactivate();
        }

        return category;
    }

    public static GrievanceDraftDetails DraftDetails(bool isAnonymous = false) =>
        new(0, "Repeated unfair shift assignments", "My shifts are changed without notice every week.", null, null, null, null, isAnonymous);

    /// <summary>A draft (the start of every scenario).</summary>
    public static GrievanceScenario NewDraft(bool isAnonymous = false) =>
        new(GrievanceCase.CreateDraft(DraftDetails(isAnonymous), Category(), Today).Value);

    /// <summary>Brings a new case to the state the options describe.</summary>
    public static GrievanceScenario Reach(ScenarioOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var scenario = NewDraft(options.IsAnonymous);
        scenario.ReachStatus(options.Status, options);
        foreach (var member in options.Members)
        {
            scenario.Apply(scenario.Case.Assign(member, GrievanceAssignmentRole.Member, scenario.ActiveAssignments, scenario.NamedSubmitter, null, Management, Now));
        }

        if (options.LegalHold)
        {
            Assert.True(scenario.Case.SetLegalHold(true).IsSuccess);
        }

        if (options.Archived)
        {
            scenario.Apply(scenario.Case.Archive(Management, Now));
        }

        Assert.Equal(options.Status, scenario.Case.Status);
        return scenario;
    }

    /// <summary>Records what a successful step produced (new and ended assignments, the proposal); fails the test otherwise.</summary>
    public GrievanceStep Apply(Result<GrievanceStep> result)
    {
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        var step = result.Value;
        if (step.EndedAssignment is { } ended)
        {
            ActiveAssignments.Remove(ended);
        }

        foreach (var reopened in step.EndedAssignments)
        {
            ActiveAssignments.Remove(reopened);
        }

        if (step.Assignment is { } added)
        {
            ActiveAssignments.Add(added);
        }

        if (step.Proposal is { } proposal)
        {
            Proposal = proposal.IsPending ? proposal : null;
        }

        return step;
    }

    private void ReachStatus(GrievanceStatus status, ScenarioOptions options)
    {
        if (status == GrievanceStatus.Draft)
        {
            return;
        }

        switch (status)
        {
            case GrievanceStatus.Submitted:
                Submitted(options);
                break;
            case GrievanceStatus.Rejected:
                Submitted(options);
                Apply(Case.Reject("TST rejected on the way.", Management, Now));
                break;
            case GrievanceStatus.Acknowledged:
                Acknowledged(options);
                break;
            case GrievanceStatus.UnderReview:
                Acknowledged(options);
                Apply(Case.StartReview(Management, Now));
                break;
            case GrievanceStatus.Assigned when options.ReassignedAfterReopen:
                // Closed with a lead, reopened (which ends the lead), and the same lead assigned again.
                ReachStatus(GrievanceStatus.Reopened, options with { ReopenedFrom = GrievanceStatus.Closed, HasLead = true });
                AssignLead();
                break;
            case GrievanceStatus.Assigned:
                Acknowledged(options);
                AssignLead();
                break;
            case GrievanceStatus.UnderInvestigation:
                Acknowledged(options);
                AssignLead();
                Apply(Case.StartInvestigation(Investigator, Now));
                break;
            case GrievanceStatus.AwaitingEmployeeResponse:
                ReachStatus(options.ReturnStatus ?? GrievanceStatus.UnderReview, options);
                Apply(Case.RequestInfo("TST question on the way?", Management, Now));
                break;
            case GrievanceStatus.ResolutionProposed:
                Proposed(options);
                break;
            case GrievanceStatus.Resolved:
                Proposed(options);
                Apply(Case.ApproveResolution(Proposal!, true, null, Reviewer, Now));
                break;
            case GrievanceStatus.Closed:
                ReachStatus(GrievanceStatus.Resolved, options);
                Apply(Case.Close(Management, Now));
                break;
            case GrievanceStatus.Reopened:
                ReachStatus(options.ReopenedFrom ?? GrievanceStatus.Closed, options);
                Apply(Case.Reopen("TST reopened on the way.", [.. ActiveAssignments], Management, Now));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status));
        }
    }

    private void Submitted(ScenarioOptions options)
    {
        Apply(Case.Submit(Category(), true, CaseNo, Placement, Submitter, Today, Now));
        if (options.IsEscalated)
        {
            Apply(Case.Escalate("TST escalated on the way.", Management, Now));
        }

        if (options.SubjectEmployeeId is { } subject)
        {
            Assert.True(Case.Triage(null, null, subject, null, NamedSubmitter, Today).IsSuccess);
        }
    }

    private void Acknowledged(ScenarioOptions options)
    {
        Submitted(options);
        Apply(Case.Acknowledge(GrievancePriority.Medium, null, Category(), Management, Today, Now));
    }

    private void AssignLead() =>
        Apply(Case.Assign(LeadEmployeeId, GrievanceAssignmentRole.Lead, ActiveAssignments, NamedSubmitter, null, Management, Now));

    private void Proposed(ScenarioOptions options)
    {
        if (options.HasLead)
        {
            Acknowledged(options);
            AssignLead();
            Apply(Case.StartInvestigation(Investigator, Now));
        }
        else
        {
            Acknowledged(options);
            Apply(Case.StartReview(Management, Now));
        }

        var proposer = options.ProposedBy is { } name ? ActorNamed(name) : options.HasLead ? Investigator : Management;
        Apply(Case.ProposeResolution("TST resolution on the way.", proposer, Now));
    }
}

/// <summary>The starting state of a golden case (its <c>case</c> object).</summary>
internal sealed record ScenarioOptions(GrievanceStatus Status)
{
    public bool IsAnonymous { get; init; }

    public GrievanceStatus? ReturnStatus { get; init; }

    public bool HasLead { get; init; }

    public IReadOnlyList<int> Members { get; init; } = [];

    public string? ProposedBy { get; init; }

    public GrievanceStatus? ReopenedFrom { get; init; }

    public bool IsEscalated { get; init; }

    public bool LegalHold { get; init; }

    public bool Archived { get; init; }

    public int? SubjectEmployeeId { get; init; }

    /// <summary>For Assigned: reached by closing a case with a lead, reopening it and assigning the same lead again.</summary>
    public bool ReassignedAfterReopen { get; init; }
}
