using System.Linq.Expressions;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Security;
using Apex.Hrms.Domain.Grievances;

namespace Apex.Hrms.Application.Grievances;

/// <summary>The caller's capacities on one case, worked out from the grants and the case's own rows.</summary>
internal sealed record GrievanceCaseRoles(
    bool Management,
    bool ManagementEdit,
    bool ManagementDelete,
    bool Investigator,
    bool InvestigatorEdit,
    bool IsLead,
    bool Reviewer,
    bool ReviewApprove)
{
    /// <summary>The tiers as the detail shows them (<c>viewerRoles</c>).</summary>
    public IReadOnlyList<string> ViewerRoles =>
    [
        .. Management ? [nameof(GrievanceActorRole.Management)] : Array.Empty<string>(),
        .. Investigator ? [nameof(GrievanceActorRole.Investigator)] : Array.Empty<string>(),
        .. Reviewer ? [nameof(GrievanceActorRole.Reviewer)] : Array.Empty<string>(),
    ];

    /// <summary>The capacity recorded for a read (the access log): the widest tier held.</summary>
    public GrievanceActorRole ReadRole =>
        Management ? GrievanceActorRole.Management : Investigator ? GrievanceActorRole.Investigator : GrievanceActorRole.Reviewer;

    /// <summary>Management Edit, or an assigned investigator with investigation Edit (start investigation, request info, escalate, notes, messages, files).</summary>
    public bool CanWorkOnCase => ManagementEdit || InvestigatorEdit;

    /// <summary>The capacity recorded for case work: management when the caller has management Edit, otherwise investigator.</summary>
    public GrievanceActorRole WorkRole => ManagementEdit ? GrievanceActorRole.Management : GrievanceActorRole.Investigator;

    /// <summary>Notes, messages and every non-Restricted file: management or an assigned investigator.</summary>
    public bool SeesCaseWork => Management || Investigator;
}

/// <summary>
/// Who sees which grievance, decided inside the database predicates (a case outside the scope is a 404, as for every scoped resource).
/// <list type="bullet">
/// <item>Submitter (<c>grievance</c>): the cases they submitted, named (CaseSubmitter.EmployeeId) or anonymous (CaseSubmitter.SubmitterPseudonym
/// = HMAC of their employee id), drafts included.</item>
/// <item>Management (<c>grievance-management:View</c>): every submitted case; archived ones too (the lists show them only on request).</item>
/// <item>Investigator (<c>grievance-investigation:View</c>): submitted, not archived cases with the caller's active assignment.</item>
/// <item>Reviewer (<c>grievance-review:View</c>): submitted, not archived cases that are escalated, have a pending proposal, or whose proposal
/// the caller decided.</item>
/// </list>
/// Conflict-of-interest rule, applied to EVERY case-team tier (management included): a case is invisible to the employee it is about
/// (SubjectEmployeeId) and to its own submitter (named by employee id, anonymous by pseudonym). The second exclusion is silent on purpose:
/// nothing ever tells the case team that a colleague is an anonymous submitter. Consequently <c>assign</c> never refuses an investigator
/// because they submitted an ANONYMOUS case (that refusal would be an oracle); if HR assigns them anyway they simply cannot open the case
/// (404) and can recuse. On a NAMED case the submitter is known to the case team, so assigning them, like assigning the subject on any
/// case, is refused with one generic conflict-of-interest message that never says which of the two applied.
/// The built-in administrator has no employee: nothing is excluded for them and they cannot submit or be assigned.
/// Aggregates (dashboards, reports, slice 7) must NOT apply the submitter exclusion: two HR users' totals would then differ by the
/// cases each of them submitted.
/// </summary>
internal sealed class GrievanceAccess(ICurrentUser user, ICallerPermissions permissions, IGrievanceSubmitterDirectory submitters, IReadModel read)
{
    public const string SelfModule = "grievance";
    public const string InvestigationModule = "grievance-investigation";
    public const string ManagementModule = "grievance-management";
    public const string ReviewModule = "grievance-review";

    /// <summary>The generic answer to assigning a conflicted investigator; it never says whether the subject or the submitter was chosen.</summary>
    public const string ConflictOfInterest = GrievanceCase.ConflictOfInterestMessage;

    public int? EmployeeId => user.EmployeeId;

    public int? UserId => user.UserId;

    public bool Has(string module, string action) => permissions.Has(module, action);

    public bool HoldsManagement => Has(ManagementModule, "View");

    public bool HoldsInvestigation => Has(InvestigationModule, "View");

    public bool HoldsReview => Has(ReviewModule, "View");

    // ---- Statement separation (security review M1) ----
    // The caller's identity (employee id, user id) and anything that identifies their anonymous cases (the pseudonym, the ids of the
    // cases it finds) are NEVER sent in the same SQL statement. A trace or Query Store plan that captured the parameters of one
    // statement would otherwise link an employee to their anonymous grievances. So:
    //  - the named lookup (EmployeeId = @me) and the pseudonym lookup (SubmitterPseudonym = @pseudonym) are two statements, and each
    //    returns only case ids (IGrievanceSubmitterDirectory.CaseIdsOfAsync);
    //  - the other per-caller facts (cases about the caller, the caller's active assignments, the proposals the caller decided) are each
    //    read by a statement of their own that takes only that identity;
    //  - the scope predicates then take only lists of case ids. The excluded list merges "submitted by me" and "about me", so even
    //    the scope statement cannot tell an anonymous case of the caller from a case about them.

    /// <summary>The ids of the caller's own cases (named and anonymous, read by two separate statements), or none without an employee.</summary>
    public async Task<int[]> OwnCaseIdsAsync(CancellationToken cancellationToken) =>
        EmployeeId is { } employee ? [.. await submitters.CaseIdsOfAsync(employee, cancellationToken)] : [];

    /// <summary>The submitter's scope: their own cases, drafts included (ids only in the statement).</summary>
    public async Task<Expression<Func<GrievanceCase, bool>>> OwnScopeAsync(CancellationToken cancellationToken)
    {
        if (EmployeeId is null)
        {
            return Predicates.Never<GrievanceCase>();
        }

        var mine = await OwnCaseIdsAsync(cancellationToken);
        return grievance => mine.Contains(grievance.Id);
    }

    /// <summary>The scope of every case-team read and command: the union of the tiers the caller holds, with the exclusions above.</summary>
    public Task<Expression<Func<GrievanceCase, bool>>> CaseTeamScopeAsync(CancellationToken cancellationToken) =>
        TeamScopeAsync(HoldsManagement, HoldsInvestigation, HoldsReview, cancellationToken);

    /// <summary>
    /// The scope of the given tiers (each only if the caller holds it), always without drafts and with the conflict-of-interest exclusions.
    /// Every per-caller fact is read first, by its own statement; the predicate holds only case-id lists (see the note above).
    /// </summary>
    public async Task<Expression<Func<GrievanceCase, bool>>> TeamScopeAsync(bool management, bool investigator, bool reviewer, CancellationToken cancellationToken)
    {
        management &= HoldsManagement;
        investigator &= HoldsInvestigation;
        reviewer &= HoldsReview;
        if (!management && !investigator && !reviewer)
        {
            return Predicates.Never<GrievanceCase>();
        }

        int[] excluded = [];
        int[] assigned = [];
        if (EmployeeId is { } me)
        {
            var own = await OwnCaseIdsAsync(cancellationToken);
            var about = await read.ToListAsync(
                read.Query<GrievanceCase>().Where(grievance => grievance.SubjectEmployeeId == me).Select(grievance => grievance.Id),
                cancellationToken);
            excluded = [.. own.Concat(about).Distinct().Order()];
            if (investigator)
            {
                assigned = [.. await read.ToListAsync(
                    read.Query<GrievanceAssignment>()
                        .Where(assignment => assignment.InvestigatorEmployeeId == me && assignment.UnassignedAtUtc == null)
                        .Select(assignment => assignment.CaseId),
                    cancellationToken)];
            }
        }
        else
        {
            // No employee (the built-in administrator): nothing to exclude and never an assignment.
            investigator = false;
        }

        int[] decided = [];
        if (reviewer && UserId is { } userId)
        {
            decided = [.. await read.ToListAsync(
                read.Query<GrievanceResolutionProposal>().Where(proposal => proposal.DecidedByUserId == userId).Select(proposal => proposal.CaseId),
                cancellationToken)];
        }

        var proposals = read.Query<GrievanceResolutionProposal>();
        return grievance => grievance.Status != GrievanceStatus.Draft
            && !excluded.Contains(grievance.Id)
            && (management
                || (investigator && grievance.ArchivedAtUtc == null && assigned.Contains(grievance.Id))
                || (reviewer && grievance.ArchivedAtUtc == null
                    && (grievance.IsEscalated
                        || decided.Contains(grievance.Id)
                        || proposals.Any(proposal => proposal.CaseId == grievance.Id && proposal.Decision == ResolutionDecision.Pending))));
    }

    /// <summary>The caller's capacities on one case (already known to be in the caller's scope).</summary>
    public async Task<GrievanceCaseRoles> RolesOnAsync(GrievanceCase grievance, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(grievance);
        var management = HoldsManagement;
        GrievanceAssignment? mine = null;
        if (EmployeeId is { } me && HoldsInvestigation && !grievance.IsArchived)
        {
            mine = await read.FirstOrDefaultAsync(
                read.Query<GrievanceAssignment>().Where(assignment => assignment.CaseId == grievance.Id
                    && assignment.InvestigatorEmployeeId == me
                    && assignment.UnassignedAtUtc == null),
                cancellationToken);
        }

        var reviewer = false;
        if (HoldsReview && !grievance.IsArchived)
        {
            var userId = UserId ?? 0;
            reviewer = grievance.IsEscalated || await read.CountAsync(
                read.Query<GrievanceResolutionProposal>().Where(proposal => proposal.CaseId == grievance.Id
                    && (proposal.Decision == ResolutionDecision.Pending || proposal.DecidedByUserId == userId)),
                cancellationToken) > 0;
        }

        var investigator = mine is not null;
        return new GrievanceCaseRoles(
            management,
            management && Has(ManagementModule, "Edit"),
            management && Has(ManagementModule, "Delete"),
            investigator,
            investigator && Has(InvestigationModule, "Edit"),
            investigator && mine!.Role == GrievanceAssignmentRole.Lead && Has(InvestigationModule, "Edit"),
            reviewer,
            Has(ReviewModule, "Approve"));
    }

    /// <summary>
    /// The actor a case-team step is recorded under: the signed-in account, its employee and display name, in the given capacity. A
    /// display name that is one of the two submitter names is marked so it can never pass for a submitter in the history.
    /// </summary>
    public GrievanceActor TeamActor(GrievanceActorRole role)
    {
        var name = string.IsNullOrWhiteSpace(user.DisplayName) ? "HR user" : user.DisplayName.Trim();
        if (GrievanceActor.IsSubmitterName(name))
        {
            name += " (staff)";
        }

        if (name.Length > GrievanceStatusHistoryEntry.ActorNameMaxLength)
        {
            name = name[..GrievanceStatusHistoryEntry.ActorNameMaxLength];
        }

        return new GrievanceActor(user.UserId, user.EmployeeId, name, role);
    }

    /// <summary>The submitter acting on their own case (the case records them without ids: <see cref="GrievanceCase.RecordedActor"/>).</summary>
    public GrievanceActor SubmitterActor() =>
        new(user.UserId, user.EmployeeId, GrievanceActor.NamedSubmitterName, GrievanceActorRole.Submitter);
}
