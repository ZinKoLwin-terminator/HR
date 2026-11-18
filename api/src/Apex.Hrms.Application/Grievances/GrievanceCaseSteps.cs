using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Grievances;

namespace Apex.Hrms.Application.Grievances;

/// <summary>A case loaded for a case-team step (tracked) with the caller's capacities on it.</summary>
internal sealed record OpenedCase(GrievanceCase Case, GrievanceCaseRoles Roles);

/// <summary>What a step writes besides its rows: the access-log entry and who is told.</summary>
internal sealed record StepEffects(GrievanceAuditAction AuditAction, string? AuditDetail, GrievanceActorRole Role)
{
    /// <summary>Tell the reviewers (a proposal, an immediate escalation) with this text.</summary>
    public string? ReviewersHear { get; init; }

    /// <summary>Tell management with this text (the case team's own steps never need it).</summary>
    public string? ManagementHears { get; init; }

    /// <summary>Tell this investigator (an assignment).</summary>
    public int? InvestigatorToTell { get; init; }

    /// <summary>Tell the named submitter even without a visible history entry (a message, a shared file).</summary>
    public bool TellSubmitter { get; init; }
}

/// <summary>
/// What every case-team step shares, in this order: the row version the client sent must be present and well formed (400); the case
/// must be in the caller's scope (404, see <see cref="GrievanceAccess"/>); the caller must hold the capacity the step needs (403, logged
/// as AccessRefused); the row version must be current (409). The step's rows, the case, the access-log entry and the notifications are
/// then written by ONE save, and the case is always marked modified, so its row version is the concurrency token of the whole case.
/// </summary>
internal sealed class GrievanceCaseSteps(
    IRepository<GrievanceCase> cases,
    IRepository<GrievanceStatusHistoryEntry> history,
    IRepository<GrievanceEscalation> escalations,
    IRepository<GrievanceCommunication> messages,
    IRepository<GrievanceAssignment> assignments,
    IRepository<GrievanceResolutionProposal> proposals,
    GrievanceAccess access,
    GrievanceReader reader,
    GrievanceAuditTrail audit,
    GrievanceNotifications notifications,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public DateTime UtcNow => clock.UtcNow.UtcDateTime;

    public DateOnly Today => clock.Today;

    public GrievanceAccess Access => access;

    public static AppError CaseNotFound(int id) => NotFoundError.For("Grievance case", id);

    /// <summary>
    /// Loads the case for a step. <paramref name="authorize"/> returns the 403 when the caller lacks the capacity; <paramref name="checkStale"/>
    /// is false for steps that leave the case row as it is (a note), whose row version is then only checked for its format.
    /// </summary>
    public async Task<Result<OpenedCase>> OpenAsync(
        int id,
        string? rowVersion,
        Func<GrievanceCaseRoles, GrievanceCase, Result> authorize,
        CancellationToken cancellationToken,
        bool checkStale = true,
        AppError? notFound = null)
    {
        var expected = RowVersionToken.Decode(rowVersion);
        if (expected.IsFailure)
        {
            return Result.Failure<OpenedCase>(expected.Error);
        }

        var grievance = await cases.FindAsync(id, await access.CaseTeamScopeAsync(cancellationToken), cancellationToken);
        if (grievance is null)
        {
            return Result.Failure<OpenedCase>(notFound ?? CaseNotFound(id));
        }

        var roles = await access.RolesOnAsync(grievance, cancellationToken);
        var allowed = authorize(roles, grievance);
        if (allowed.IsFailure)
        {
            await RefuseAsync(grievance.Id, roles, allowed.Error, cancellationToken);
            return Result.Failure<OpenedCase>(allowed.Error);
        }

        if (checkStale)
        {
            if (!grievance.RowVersion.AsSpan().SequenceEqual(expected.Value))
            {
                return Result.Failure<OpenedCase>(RowVersionToken.Stale);
            }

            cases.ExpectRowVersion(grievance, expected.Value);
        }

        return Result.Success(new OpenedCase(grievance, roles));
    }

    /// <summary>A refused capacity (403) is logged as AccessRefused and saved on its own; other failures are just returned.</summary>
    public async Task RefuseAsync(int caseId, GrievanceCaseRoles roles, AppError error, CancellationToken cancellationToken, string? detail = null)
    {
        ArgumentNullException.ThrowIfNull(roles);
        ArgumentNullException.ThrowIfNull(error);
        if (error.Type != ErrorType.Forbidden && detail is null)
        {
            return;
        }

        audit.Team(caseId, GrievanceAuditAction.AccessRefused, roles.ReadRole, detail ?? "forbidden");
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Writes a workflow step: the history entries, the rows that came with it (a new escalation, message, assignment or proposal; an
    /// ended assignment or a decided proposal is already tracked), the access-log entry, the notifications, the case itself; one save.
    /// Answers with the case detail as the caller sees it now.
    /// </summary>
    public async Task<Result<GrievanceCaseDetailDto>> SaveStepAsync(OpenedCase opened, GrievanceStep step, StepEffects effects, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(opened);
        ArgumentNullException.ThrowIfNull(step);
        foreach (var entry in step.History)
        {
            history.Add(entry);
        }

        if (step.Escalation is { } escalation)
        {
            escalations.Add(escalation);
        }

        if (step.Message is { } message)
        {
            messages.Add(message);
        }

        if (step.Assignment is { Id: 0 } assignment)
        {
            assignments.Add(assignment);
        }

        if (step.Proposal is { Id: 0 } proposal)
        {
            proposals.Add(proposal);
        }

        var tellSubmitter = effects.TellSubmitter || step.History.Any(entry => entry.VisibleToSubmitter);
        var reviewers = effects.ReviewersHear ?? (step.Escalation is { IsImmediate: true } ? "it was escalated as urgent" : null);
        return await CommitAsync(opened, effects with { TellSubmitter = tellSubmitter, ReviewersHear = reviewers }, cancellationToken);
    }

    /// <summary>Saves a change of the case (and whatever else was staged) with its access-log entry and notifications; answers with the detail.</summary>
    public async Task<Result<GrievanceCaseDetailDto>> CommitAsync(OpenedCase opened, StepEffects effects, CancellationToken cancellationToken, bool caseChanged = true)
    {
        var saved = await SaveAsync(opened, effects, cancellationToken, caseChanged);
        return saved.IsFailure ? Result.Failure<GrievanceCaseDetailDto>(saved.Error) : Result.Success(await DetailAsync(opened.Case, cancellationToken));
    }

    /// <summary>
    /// Saves a change of the case (and whatever else was staged) with its access-log entry and notifications, without building the
    /// detail: for an answer that does not carry it (an upload answers with the file and the row version only).
    /// </summary>
    public async Task<Result> SaveAsync(OpenedCase opened, StepEffects effects, CancellationToken cancellationToken, bool caseChanged = true)
    {
        ArgumentNullException.ThrowIfNull(opened);
        ArgumentNullException.ThrowIfNull(effects);
        var grievance = opened.Case;
        if (caseChanged)
        {
            cases.MarkModified(grievance);
        }

        audit.Team(grievance.Id, effects.AuditAction, effects.Role, effects.AuditDetail);
        var told = await NotifyAsync(grievance, effects, cancellationToken);
        return told.IsFailure ? told : await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The case detail as the caller sees it now (their tiers worked out again: a step may have changed them).</summary>
    public async Task<GrievanceCaseDetailDto> DetailAsync(GrievanceCase grievance, CancellationToken cancellationToken)
    {
        var roles = await access.RolesOnAsync(grievance, cancellationToken);
        return GrievanceRedaction.ToCaseDetail(await reader.BundleAsync(grievance, cancellationToken), roles, clock.Today);
    }

    public GrievanceActor Actor(GrievanceActorRole role) => access.TeamActor(role);

    private async Task<Result> NotifyAsync(GrievanceCase grievance, StepEffects effects, CancellationToken cancellationToken)
    {
        var results = new List<Result>();
        if (effects.TellSubmitter)
        {
            results.Add(await notifications.ToNamedSubmitterAsync(grievance, cancellationToken));
        }

        if (effects.ReviewersHear is { } reviewers)
        {
            results.Add(await notifications.ToReviewersAsync(grievance, reviewers, cancellationToken));
        }

        if (effects.ManagementHears is { } management)
        {
            results.Add(await notifications.ToManagementAsync(grievance, management, byCaseTeam: true, cancellationToken));
        }

        if (effects.InvestigatorToTell is { } investigator)
        {
            results.Add(await notifications.ToInvestigatorAsync(grievance, investigator, cancellationToken));
        }

        return results.FirstOrDefault(result => result.IsFailure) ?? Result.Success();
    }
}
