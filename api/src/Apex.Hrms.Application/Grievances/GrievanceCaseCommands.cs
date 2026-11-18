using System.Globalization;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Grievances;
using Apex.Hrms.Domain.Organization;
using FluentValidation;

namespace Apex.Hrms.Application.Grievances;

/// <summary>The 403 texts of the case-team steps (they name the capacity, never the case).</summary>
internal static class GrievanceRefusals
{
    public const string ManagementOnly = "Only HR management can take this step on a grievance case.";
    public const string CaseWork = "Only HR management or an investigator assigned to this case can take this step.";
    public const string LeadOnly = "Only the lead investigator can propose a resolution while the case is under investigation.";
    public const string ReviewOnly = "Only a grievance reviewer can decide a proposed resolution.";
    public const string InvestigatorDecides = "An investigator on this case cannot decide its resolution.";

    /// <summary>
    /// Deciding a proposal (security review L1): the caller must hold the reviewer tier on this case AND review Approve, and must not be
    /// an active investigator of it (nobody reviews their own investigation; the proposer is refused by the domain too).
    /// </summary>
    public static Result Decider(GrievanceCaseRoles roles)
    {
        ArgumentNullException.ThrowIfNull(roles);
        var reviewer = Allow(roles.Reviewer && roles.ReviewApprove, ReviewOnly);
        return reviewer.IsFailure ? reviewer : Allow(!roles.Investigator, InvestigatorDecides);
    }
    public const string ReopenClosed = "Reopening a closed case also needs the grievance reviewer's approval.";
    public const string ArchiveOnly = "Only HR management with the delete permission can archive a grievance case.";
    public const string RestrictedFile = "Only HR management can add a restricted file.";

    public static Result Allow(bool allowed, string message) => allowed ? Result.Success() : Result.Failure(AppError.Forbidden(message));
}

/// <summary>
/// The shared flow of a case-team command: request shape (400), then <see cref="GrievanceCaseSteps.OpenAsync"/> (row version 400, scope
/// 404, capacity 403, stale 409), then the domain step (409 on the status, 400 on input, 403 for deciding one's own proposal) and one save.
/// </summary>
internal abstract class GrievanceCommandHandler<TCommand>(IValidator<TCommand> validator, GrievanceCaseSteps steps)
    : ICommandHandler<TCommand, GrievanceCaseDetailDto>
    where TCommand : IEntityCommand
{
    protected GrievanceCaseSteps Steps => steps;

    /// <summary>False for a step that does not change the case row: its row version is then only checked for its format.</summary>
    protected virtual bool ChecksRowVersion => true;

    public async Task<Result<GrievanceCaseDetailDto>> HandleAsync(TCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var version = RowVersionToken.Decode(command.RowVersion);
        if (version.IsFailure)
        {
            return Result.Failure<GrievanceCaseDetailDto>(version.Error);
        }

        var valid = await validator.CheckAsync(command, cancellationToken);
        if (valid.IsFailure)
        {
            return Result.Failure<GrievanceCaseDetailDto>(valid.Error);
        }

        var opened = await steps.OpenAsync(command.Id, command.RowVersion, Authorize, cancellationToken, ChecksRowVersion);
        return opened.IsFailure
            ? Result.Failure<GrievanceCaseDetailDto>(opened.Error)
            : await RunAsync(opened.Value, command, cancellationToken);
    }

    protected abstract Result Authorize(GrievanceCaseRoles roles, GrievanceCase grievance);

    protected abstract Task<Result<GrievanceCaseDetailDto>> RunAsync(OpenedCase opened, TCommand command, CancellationToken cancellationToken);

    /// <summary>Saves a status step with a StatusChange entry (the pair of statuses) in the access log.</summary>
    protected async Task<Result<GrievanceCaseDetailDto>> SaveTransitionAsync(
        OpenedCase opened,
        Result<GrievanceStep> step,
        GrievanceActorRole role,
        CancellationToken cancellationToken,
        Func<StepEffects, StepEffects>? more = null)
    {
        ArgumentNullException.ThrowIfNull(opened);
        if (step.IsFailure)
        {
            return Result.Failure<GrievanceCaseDetailDto>(step.Error);
        }

        var entry = step.Value.Entry;
        var effects = new StepEffects(GrievanceAuditAction.StatusChange, GrievanceAuditTrail.Transition(entry.FromStatus, entry.ToStatus), role);
        return await steps.SaveStepAsync(opened, step.Value, more is null ? effects : more(effects), cancellationToken);
    }
}

/// <summary>Submitted to Acknowledged with a priority and a target date (management Edit). Critical escalates at once and alerts the reviewers.</summary>
internal sealed class AcknowledgeGrievanceHandler(
    IValidator<AcknowledgeGrievanceCommand> validator,
    GrievanceCaseSteps steps,
    IRepository<GrievanceCategory> categories) : GrievanceCommandHandler<AcknowledgeGrievanceCommand>(validator, steps)
{
    protected override Result Authorize(GrievanceCaseRoles roles, GrievanceCase grievance) =>
        GrievanceRefusals.Allow(roles.ManagementEdit, GrievanceRefusals.ManagementOnly);

    protected override async Task<Result<GrievanceCaseDetailDto>> RunAsync(OpenedCase opened, AcknowledgeGrievanceCommand command, CancellationToken cancellationToken)
    {
        var category = await categories.FindAsync(opened.Case.CategoryId, cancellationToken)
            ?? throw new InvalidOperationException($"The category of grievance case #{opened.Case.Id} is missing.");
        var step = opened.Case.Acknowledge(
            GrievanceText.Priority.Parse(command.Priority),
            command.TargetResolutionDate,
            category,
            Steps.Actor(GrievanceActorRole.Management),
            Steps.Today,
            Steps.UtcNow,
            command.Comment);
        return await SaveTransitionAsync(opened, step, GrievanceActorRole.Management, cancellationToken);
    }
}

/// <summary>Acknowledged or Reopened to UnderReview (management Edit).</summary>
internal sealed class StartGrievanceReviewHandler(IValidator<StartGrievanceReviewCommand> validator, GrievanceCaseSteps steps)
    : GrievanceCommandHandler<StartGrievanceReviewCommand>(validator, steps)
{
    protected override Result Authorize(GrievanceCaseRoles roles, GrievanceCase grievance) =>
        GrievanceRefusals.Allow(roles.ManagementEdit, GrievanceRefusals.ManagementOnly);

    protected override Task<Result<GrievanceCaseDetailDto>> RunAsync(OpenedCase opened, StartGrievanceReviewCommand command, CancellationToken cancellationToken) =>
        SaveTransitionAsync(opened, opened.Case.StartReview(Steps.Actor(GrievanceActorRole.Management), Steps.UtcNow, command.Comment), GrievanceActorRole.Management, cancellationToken);
}

/// <summary>
/// Assigns an investigator (management Edit). The investigator must be an employee who can investigate (holds
/// <c>grievance-investigation:Edit</c> through an account that can sign in). Conflict of interest: the subject of the case (named or
/// anonymous case) and the submitter of a NAMED case are refused with one generic Validation error on <c>investigatorEmployeeId</c>
/// that never says which applied; the refusal is logged (AccessRefused, "assign:conflict"). The submitter of an ANONYMOUS case is never
/// checked here (a refusal would reveal them); the case-team scope hides the case from them, so they get a 404 and can recuse.
/// </summary>
internal sealed class AssignGrievanceInvestigatorHandler(
    IValidator<AssignGrievanceInvestigatorCommand> validator,
    GrievanceCaseSteps steps,
    GrievanceReader reader,
    IRepository<GrievanceAssignment> assignments,
    IPermissionHolders holders,
    IReadModel read) : GrievanceCommandHandler<AssignGrievanceInvestigatorCommand>(validator, steps)
{
    public const string NotAnInvestigator = "Choose an employee who can investigate grievances.";

    protected override Result Authorize(GrievanceCaseRoles roles, GrievanceCase grievance) =>
        GrievanceRefusals.Allow(roles.ManagementEdit, GrievanceRefusals.ManagementOnly);

    protected override async Task<Result<GrievanceCaseDetailDto>> RunAsync(OpenedCase opened, AssignGrievanceInvestigatorCommand command, CancellationToken cancellationToken)
    {
        var grievance = opened.Case;
        var investigator = command.InvestigatorEmployeeId;
        var eligible = holders.EmployeesHolding(GrievanceAccess.InvestigationModule, "Edit");
        if (await read.CountAsync(eligible.Where(id => id == investigator), cancellationToken) == 0)
        {
            return Result.Failure<GrievanceCaseDetailDto>(AppError.Validation(NotAnInvestigator, "investigatorEmployeeId", NotAnInvestigator));
        }

        var namedSubmitter = (await reader.NamedSubmitterAsync(grievance, cancellationToken))?.EmployeeId;
        if (investigator == grievance.SubjectEmployeeId || (namedSubmitter is { } submitter && investigator == submitter))
        {
            var conflict = AppError.Validation(GrievanceAccess.ConflictOfInterest, "investigatorEmployeeId", GrievanceAccess.ConflictOfInterest);
            await Steps.RefuseAsync(grievance.Id, opened.Roles, conflict, cancellationToken, "assign:conflict");
            return Result.Failure<GrievanceCaseDetailDto>(conflict);
        }

        var id = grievance.Id;
        var active = await assignments.ListForChangeAsync(assignment => assignment.CaseId == id && assignment.UnassignedAtUtc == null, cancellationToken);
        var role = GrievanceText.AssignmentRole.Parse(command.Role);
        var step = grievance.Assign(investigator, role, active, namedSubmitter, command.Reason, Steps.Actor(GrievanceActorRole.Management), Steps.UtcNow);
        if (step.IsFailure)
        {
            return Result.Failure<GrievanceCaseDetailDto>(step.Error);
        }

        var detail = string.Create(CultureInfo.InvariantCulture, $"employee:{investigator};role:{GrievanceText.AssignmentRole.ToText(role)};{GrievanceAuditTrail.Transition(step.Value.Entry.FromStatus, step.Value.Entry.ToStatus)}");
        return await Steps.SaveStepAsync(
            opened,
            step.Value,
            new StepEffects(GrievanceAuditAction.Assign, detail, GrievanceActorRole.Management) { InvestigatorToTell = investigator },
            cancellationToken);
    }
}

/// <summary>Ends a Member's assignment on an open case (management Edit). The Lead is replaced by assigning a new Lead, never unassigned.</summary>
internal sealed class UnassignGrievanceInvestigatorHandler(
    IValidator<UnassignGrievanceInvestigatorCommand> validator,
    GrievanceCaseSteps steps,
    IRepository<GrievanceAssignment> assignments) : GrievanceCommandHandler<UnassignGrievanceInvestigatorCommand>(validator, steps)
{
    protected override Result Authorize(GrievanceCaseRoles roles, GrievanceCase grievance) =>
        GrievanceRefusals.Allow(roles.ManagementEdit, GrievanceRefusals.ManagementOnly);

    protected override async Task<Result<GrievanceCaseDetailDto>> RunAsync(OpenedCase opened, UnassignGrievanceInvestigatorCommand command, CancellationToken cancellationToken)
    {
        var assignment = await assignments.FindAsync(command.AssignmentId, cancellationToken);
        if (assignment is null || assignment.CaseId != opened.Case.Id)
        {
            return Result.Failure<GrievanceCaseDetailDto>(NotFoundError.For("Assignment", command.AssignmentId));
        }

        var step = opened.Case.Unassign(assignment, command.Reason, Steps.Actor(GrievanceActorRole.Management), Steps.UtcNow);
        return step.IsFailure
            ? Result.Failure<GrievanceCaseDetailDto>(step.Error)
            : await Steps.SaveStepAsync(
                opened,
                step.Value,
                new StepEffects(GrievanceAuditAction.Unassign, string.Create(CultureInfo.InvariantCulture, $"assignment:{assignment.Id};employee:{assignment.InvestigatorEmployeeId}"), GrievanceActorRole.Management),
                cancellationToken);
    }
}

/// <summary>Assigned to UnderInvestigation (an assigned investigator with investigation Edit, or management Edit).</summary>
internal sealed class StartGrievanceInvestigationHandler(IValidator<StartGrievanceInvestigationCommand> validator, GrievanceCaseSteps steps)
    : GrievanceCommandHandler<StartGrievanceInvestigationCommand>(validator, steps)
{
    protected override Result Authorize(GrievanceCaseRoles roles, GrievanceCase grievance) =>
        GrievanceRefusals.Allow(roles.CanWorkOnCase, GrievanceRefusals.CaseWork);

    protected override Task<Result<GrievanceCaseDetailDto>> RunAsync(OpenedCase opened, StartGrievanceInvestigationCommand command, CancellationToken cancellationToken) =>
        SaveTransitionAsync(opened, opened.Case.StartInvestigation(Steps.Actor(opened.Roles.WorkRole), Steps.UtcNow, command.Comment), opened.Roles.WorkRole, cancellationToken);
}

/// <summary>To AwaitingEmployeeResponse with a question to the submitter (management Edit or an assigned investigator with Edit).</summary>
internal sealed class RequestGrievanceInfoHandler(IValidator<RequestGrievanceInfoCommand> validator, GrievanceCaseSteps steps)
    : GrievanceCommandHandler<RequestGrievanceInfoCommand>(validator, steps)
{
    protected override Result Authorize(GrievanceCaseRoles roles, GrievanceCase grievance) =>
        GrievanceRefusals.Allow(roles.CanWorkOnCase, GrievanceRefusals.CaseWork);

    protected override Task<Result<GrievanceCaseDetailDto>> RunAsync(OpenedCase opened, RequestGrievanceInfoCommand command, CancellationToken cancellationToken) =>
        SaveTransitionAsync(opened, opened.Case.RequestInfo(command.Message, Steps.Actor(opened.Roles.WorkRole), Steps.UtcNow), opened.Roles.WorkRole, cancellationToken);
}

/// <summary>
/// To ResolutionProposed: by the Lead investigator while UnderInvestigation, by management Edit while UnderReview. The reviewers are told.
/// In any other status either may try and gets the domain's 409.
/// </summary>
internal sealed class ProposeGrievanceResolutionHandler(IValidator<ProposeGrievanceResolutionCommand> validator, GrievanceCaseSteps steps)
    : GrievanceCommandHandler<ProposeGrievanceResolutionCommand>(validator, steps)
{
    protected override Result Authorize(GrievanceCaseRoles roles, GrievanceCase grievance) => grievance.Status switch
    {
        GrievanceStatus.UnderInvestigation => GrievanceRefusals.Allow(roles.IsLead, GrievanceRefusals.LeadOnly),
        GrievanceStatus.UnderReview => GrievanceRefusals.Allow(roles.ManagementEdit, GrievanceRefusals.ManagementOnly),
        _ => GrievanceRefusals.Allow(roles.IsLead || roles.ManagementEdit, GrievanceRefusals.CaseWork),
    };

    protected override Task<Result<GrievanceCaseDetailDto>> RunAsync(OpenedCase opened, ProposeGrievanceResolutionCommand command, CancellationToken cancellationToken)
    {
        var role = opened.Case.Status == GrievanceStatus.UnderInvestigation ? GrievanceActorRole.Investigator : GrievanceActorRole.Management;
        return SaveTransitionAsync(
            opened,
            opened.Case.ProposeResolution(command.Summary, Steps.Actor(role), Steps.UtcNow),
            role,
            cancellationToken,
            effects => effects with { ReviewersHear = "a resolution was proposed" });
    }
}

/// <summary>Shared by approve and reject: the pending proposal of the case (tracked), or a 409 when there is none.</summary>
internal static class PendingProposal
{
    public static async Task<Result<GrievanceResolutionProposal>> OfAsync(IRepository<GrievanceResolutionProposal> proposals, int caseId, CancellationToken cancellationToken)
    {
        var pending = await proposals.ListForChangeAsync(proposal => proposal.CaseId == caseId && proposal.Decision == ResolutionDecision.Pending, cancellationToken);
        return pending.Count == 0
            ? Result.Failure<GrievanceResolutionProposal>(AppError.Conflict("This case has no resolution waiting for a decision."))
            : Result.Success(pending[0]);
    }
}

/// <summary>ResolutionProposed to Resolved (<c>grievance-review:Approve</c>, never the proposer: 403 from the domain).</summary>
internal sealed class ApproveGrievanceResolutionHandler(
    IValidator<ApproveGrievanceResolutionCommand> validator,
    GrievanceCaseSteps steps,
    IRepository<GrievanceResolutionProposal> proposals) : GrievanceCommandHandler<ApproveGrievanceResolutionCommand>(validator, steps)
{
    protected override Result Authorize(GrievanceCaseRoles roles, GrievanceCase grievance) =>
        GrievanceRefusals.Decider(roles);

    protected override async Task<Result<GrievanceCaseDetailDto>> RunAsync(OpenedCase opened, ApproveGrievanceResolutionCommand command, CancellationToken cancellationToken)
    {
        var open = opened.Case.Status == GrievanceStatus.ResolutionProposed
            ? await PendingProposal.OfAsync(proposals, opened.Case.Id, cancellationToken)
            : Result.Failure<GrievanceResolutionProposal>(AppError.Conflict($"The case is {GrievanceText.Status.ToText(opened.Case.Status)}; only a proposed resolution can be decided."));
        if (open.IsFailure)
        {
            return Result.Failure<GrievanceCaseDetailDto>(open.Error);
        }

        var step = opened.Case.ApproveResolution(open.Value, command.VisibleToSubmitter, command.Comment, Steps.Actor(GrievanceActorRole.Reviewer), Steps.UtcNow);
        return await SaveTransitionAsync(opened, step, GrievanceActorRole.Reviewer, cancellationToken);
    }
}

/// <summary>ResolutionProposed back to UnderInvestigation (active Lead) or UnderReview, with a reason (<c>grievance-review:Approve</c>, never the proposer).</summary>
internal sealed class RejectGrievanceResolutionHandler(
    IValidator<RejectGrievanceResolutionCommand> validator,
    GrievanceCaseSteps steps,
    IRepository<GrievanceResolutionProposal> proposals,
    IRepository<GrievanceAssignment> assignments) : GrievanceCommandHandler<RejectGrievanceResolutionCommand>(validator, steps)
{
    protected override Result Authorize(GrievanceCaseRoles roles, GrievanceCase grievance) =>
        GrievanceRefusals.Decider(roles);

    protected override async Task<Result<GrievanceCaseDetailDto>> RunAsync(OpenedCase opened, RejectGrievanceResolutionCommand command, CancellationToken cancellationToken)
    {
        var id = opened.Case.Id;
        var open = opened.Case.Status == GrievanceStatus.ResolutionProposed
            ? await PendingProposal.OfAsync(proposals, id, cancellationToken)
            : Result.Failure<GrievanceResolutionProposal>(AppError.Conflict($"The case is {GrievanceText.Status.ToText(opened.Case.Status)}; only a proposed resolution can be decided."));
        if (open.IsFailure)
        {
            return Result.Failure<GrievanceCaseDetailDto>(open.Error);
        }

        var hasLead = await assignments.AnyAsync(
            assignment => assignment.CaseId == id && assignment.Role == GrievanceAssignmentRole.Lead && assignment.UnassignedAtUtc == null,
            cancellationToken);
        var step = opened.Case.RejectResolution(open.Value, command.Reason, hasLead, Steps.Actor(GrievanceActorRole.Reviewer), Steps.UtcNow);
        return await SaveTransitionAsync(opened, step, GrievanceActorRole.Reviewer, cancellationToken);
    }
}

/// <summary>Submitted, Acknowledged or UnderReview to Rejected with a reason (management Edit). Sets the retention date.</summary>
internal sealed class RejectGrievanceHandler(IValidator<RejectGrievanceCommand> validator, GrievanceCaseSteps steps, GrievanceSettings settings)
    : GrievanceCommandHandler<RejectGrievanceCommand>(validator, steps)
{
    protected override Result Authorize(GrievanceCaseRoles roles, GrievanceCase grievance) =>
        GrievanceRefusals.Allow(roles.ManagementEdit, GrievanceRefusals.ManagementOnly);

    protected override Task<Result<GrievanceCaseDetailDto>> RunAsync(OpenedCase opened, RejectGrievanceCommand command, CancellationToken cancellationToken)
    {
        var step = opened.Case.Reject(command.Reason, Steps.Actor(GrievanceActorRole.Management), Steps.UtcNow);
        if (step.IsSuccess)
        {
            opened.Case.ApplyRetention(Steps.Today, settings.RetentionYears);
        }

        return SaveTransitionAsync(opened, step, GrievanceActorRole.Management, cancellationToken);
    }
}

/// <summary>Resolved to Closed (management Edit). Sets the retention date (today plus <c>Grievance:RetentionYears</c>).</summary>
internal sealed class CloseGrievanceHandler(IValidator<CloseGrievanceCommand> validator, GrievanceCaseSteps steps, GrievanceSettings settings)
    : GrievanceCommandHandler<CloseGrievanceCommand>(validator, steps)
{
    protected override Result Authorize(GrievanceCaseRoles roles, GrievanceCase grievance) =>
        GrievanceRefusals.Allow(roles.ManagementEdit, GrievanceRefusals.ManagementOnly);

    protected override Task<Result<GrievanceCaseDetailDto>> RunAsync(OpenedCase opened, CloseGrievanceCommand command, CancellationToken cancellationToken)
    {
        var step = opened.Case.Close(Steps.Actor(GrievanceActorRole.Management), Steps.UtcNow, command.Comment);
        if (step.IsSuccess)
        {
            opened.Case.ApplyRetention(Steps.Today, settings.RetentionYears);
        }

        return SaveTransitionAsync(opened, step, GrievanceActorRole.Management, cancellationToken);
    }
}

/// <summary>
/// Resolved, Rejected or Closed to Reopened with a reason (management Edit; from Closed also <c>grievance-review:Approve</c>). Every active
/// assignment ends in the same step (Unassign entries with reason "Case reopened", not shown to the submitter).
/// </summary>
internal sealed class ReopenGrievanceHandler(
    IValidator<ReopenGrievanceCommand> validator,
    GrievanceCaseSteps steps,
    IRepository<GrievanceAssignment> assignments) : GrievanceCommandHandler<ReopenGrievanceCommand>(validator, steps)
{
    protected override Result Authorize(GrievanceCaseRoles roles, GrievanceCase grievance)
    {
        var management = GrievanceRefusals.Allow(roles.ManagementEdit, GrievanceRefusals.ManagementOnly);
        return management.IsFailure || grievance.Status != GrievanceStatus.Closed
            ? management
            : GrievanceRefusals.Allow(roles.ReviewApprove, GrievanceRefusals.ReopenClosed);
    }

    /// <summary>Reopening ends every active assignment (tracked here, saved with the step), so HR assigns a Lead again from Reopened.</summary>
    protected override async Task<Result<GrievanceCaseDetailDto>> RunAsync(OpenedCase opened, ReopenGrievanceCommand command, CancellationToken cancellationToken)
    {
        var id = opened.Case.Id;
        var active = await assignments.ListForChangeAsync(assignment => assignment.CaseId == id && assignment.UnassignedAtUtc == null, cancellationToken);
        return await SaveTransitionAsync(
            opened,
            opened.Case.Reopen(command.Reason, active, Steps.Actor(GrievanceActorRole.Management), Steps.UtcNow),
            GrievanceActorRole.Management,
            cancellationToken,
            effects => effects with { AuditDetail = effects.AuditDetail + string.Create(CultureInfo.InvariantCulture, $";unassigned:{active.Count}") });
    }
}

/// <summary>Flags an open case as escalated (management Edit or an assigned investigator with Edit). An immediate escalation alerts the reviewers.</summary>
internal sealed class EscalateGrievanceHandler(IValidator<EscalateGrievanceCommand> validator, GrievanceCaseSteps steps)
    : GrievanceCommandHandler<EscalateGrievanceCommand>(validator, steps)
{
    protected override Result Authorize(GrievanceCaseRoles roles, GrievanceCase grievance) =>
        GrievanceRefusals.Allow(roles.CanWorkOnCase, GrievanceRefusals.CaseWork);

    protected override Task<Result<GrievanceCaseDetailDto>> RunAsync(OpenedCase opened, EscalateGrievanceCommand command, CancellationToken cancellationToken) =>
        SaveTransitionAsync(
            opened,
            opened.Case.Escalate(command.Reason, Steps.Actor(opened.Roles.WorkRole), Steps.UtcNow, command.IsImmediate),
            opened.Roles.WorkRole,
            cancellationToken);
}

/// <summary>Archives a Closed or Rejected case that is not on legal hold (management Delete). Nothing changes it afterwards.</summary>
internal sealed class ArchiveGrievanceHandler(IValidator<ArchiveGrievanceCommand> validator, GrievanceCaseSteps steps)
    : GrievanceCommandHandler<ArchiveGrievanceCommand>(validator, steps)
{
    protected override Result Authorize(GrievanceCaseRoles roles, GrievanceCase grievance) =>
        GrievanceRefusals.Allow(roles.ManagementDelete, GrievanceRefusals.ArchiveOnly);

    protected override Task<Result<GrievanceCaseDetailDto>> RunAsync(OpenedCase opened, ArchiveGrievanceCommand command, CancellationToken cancellationToken) =>
        SaveTransitionAsync(opened, opened.Case.Archive(Steps.Actor(GrievanceActorRole.Management), Steps.UtcNow, command.Comment), GrievanceActorRole.Management, cancellationToken);
}

/// <summary>
/// HR's triage (management Edit): category, priority, target date, the employee the case is about, the involved department and the legal
/// hold. On an open case every field may change, and raising the priority to Critical escalates the case at once (like at
/// acknowledgement) and alerts the reviewers. On a case that is no longer open (resolved, closed, rejected or archived) only the legal
/// hold may change; any other difference is a 409. The access log records the changed fields as codes and ids (never free text).
/// </summary>
internal sealed class TriageGrievanceHandler(
    IValidator<TriageGrievanceCommand> validator,
    GrievanceCaseSteps steps,
    GrievanceReader reader,
    IRepository<GrievanceCategory> categories,
    IRepository<Employee> employees,
    IRepository<Department> departments,
    IRepository<GrievanceAssignment> assignments) : GrievanceCommandHandler<TriageGrievanceCommand>(validator, steps)
{
    public const string OnlyLegalHold = "This case is no longer open: only its legal hold can change.";

    protected override Result Authorize(GrievanceCaseRoles roles, GrievanceCase grievance) =>
        GrievanceRefusals.Allow(roles.ManagementEdit, GrievanceRefusals.ManagementOnly);

    protected override async Task<Result<GrievanceCaseDetailDto>> RunAsync(OpenedCase opened, TriageGrievanceCommand command, CancellationToken cancellationToken)
    {
        var grievance = opened.Case;
        var priority = command.Priority is null ? (GrievancePriority?)null : GrievanceText.Priority.Parse(command.Priority);
        var before = (grievance.CategoryId, grievance.Priority, grievance.TargetResolutionDate, grievance.SubjectEmployeeId, grievance.InvolvedDepartmentId, grievance.LegalHold);
        var changes = new List<string>();
        void Note<T>(string field, T old, T now)
        {
            if (!EqualityComparer<T>.Default.Equals(old, now))
            {
                changes.Add(string.Create(CultureInfo.InvariantCulture, $"{field}:{Code(old)}>{Code(now)}"));
            }
        }

        Note("categoryId", before.CategoryId, command.CategoryId);
        Note("priority", before.Priority, priority);
        Note("targetResolutionDate", before.TargetResolutionDate, command.TargetResolutionDate);
        Note("subjectEmployeeId", before.SubjectEmployeeId, command.SubjectEmployeeId);
        Note("involvedDepartmentId", before.InvolvedDepartmentId, command.InvolvedDepartmentId);
        var fieldsChange = changes.Count > 0;
        var legalHold = command.LegalHold!.Value;
        Note("legalHold", before.LegalHold, legalHold);

        GrievanceStep? escalation = null;
        if (!GrievanceCase.IsActive(grievance.Status) || grievance.IsArchived)
        {
            if (fieldsChange)
            {
                return Result.Failure<GrievanceCaseDetailDto>(AppError.Conflict(OnlyLegalHold));
            }
        }
        else
        {
            var references = await CheckReferencesAsync(command, cancellationToken);
            if (references.IsFailure)
            {
                return Result.Failure<GrievanceCaseDetailDto>(references.Error);
            }

            // The employee a case is about may not be one of its active investigators: the same generic conflict-of-interest answer as
            // assigning the subject (and logged the same way), so it never says who is who.
            if (command.SubjectEmployeeId is { } subject
                && await assignments.AnyAsync(
                    assignment => assignment.CaseId == grievance.Id && assignment.InvestigatorEmployeeId == subject && assignment.UnassignedAtUtc == null,
                    cancellationToken))
            {
                var conflict = AppError.Validation(GrievanceAccess.ConflictOfInterest, "subjectEmployeeId", GrievanceAccess.ConflictOfInterest);
                await Steps.RefuseAsync(grievance.Id, opened.Roles, conflict, cancellationToken, "assign:conflict");
                return Result.Failure<GrievanceCaseDetailDto>(conflict);
            }

            var reclassified = grievance.Reclassify(references.Value);
            if (reclassified.IsFailure)
            {
                return Result.Failure<GrievanceCaseDetailDto>(reclassified.Error);
            }

            var namedSubmitter = (await reader.NamedSubmitterAsync(grievance, cancellationToken))?.EmployeeId;
            var triaged = grievance.Triage(priority, command.TargetResolutionDate, command.SubjectEmployeeId, command.InvolvedDepartmentId, namedSubmitter, Steps.Today);
            if (triaged.IsFailure)
            {
                return Result.Failure<GrievanceCaseDetailDto>(triaged.Error);
            }

            if (priority == GrievancePriority.Critical && before.Priority != GrievancePriority.Critical && !grievance.IsEscalated)
            {
                var escalated = grievance.Escalate(GrievanceCase.CriticalTriageEscalationReason, Steps.Actor(GrievanceActorRole.Management), Steps.UtcNow, isImmediate: true);
                if (escalated.IsFailure)
                {
                    return Result.Failure<GrievanceCaseDetailDto>(escalated.Error);
                }

                escalation = escalated.Value;
            }
        }

        var held = grievance.SetLegalHold(legalHold);
        if (held.IsFailure)
        {
            return Result.Failure<GrievanceCaseDetailDto>(held.Error);
        }

        var effects = new StepEffects(GrievanceAuditAction.Triage, changes.Count == 0 ? "no-change" : string.Join(';', changes), GrievanceActorRole.Management);
        return escalation is null
            ? await Steps.CommitAsync(opened, effects, cancellationToken, caseChanged: changes.Count > 0)
            : await Steps.SaveStepAsync(opened, escalation, effects, cancellationToken);
    }

    private async Task<Result<GrievanceCategory>> CheckReferencesAsync(TriageGrievanceCommand command, CancellationToken cancellationToken)
    {
        var violations = new Violations();
        var category = await categories.FindAsync(command.CategoryId, cancellationToken);
        violations.AddIf(category is null, "categoryId", "Choose an active category.");
        if (command.SubjectEmployeeId is { } subject)
        {
            violations.AddIf(await employees.FindAsync(subject, cancellationToken) is null, "subjectEmployeeId", "Choose an employee.");
        }

        if (command.InvolvedDepartmentId is { } department)
        {
            violations.AddIf(await departments.FindAsync(department, cancellationToken) is null, "involvedDepartmentId", "Choose a department.");
        }

        return violations.Any ? Result.Failure<GrievanceCategory>(violations.ToError()) : Result.Success(category!);
    }

    private static string Code<T>(T value) => value switch
    {
        null => "null",
        bool flag => flag ? "1" : "0",
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        GrievancePriority level => GrievanceText.Priority.ToText(level),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "null",
    };
}

/// <summary>
/// A Message from the case team to the submitter (management Edit or an assigned investigator with Edit), on a submitted case that is
/// not closed or archived. It moves the submitter's unread badge, so the case row (and its row version) changes.
/// </summary>
internal sealed class SendGrievanceCaseMessageHandler(
    IValidator<SendGrievanceCaseMessageCommand> validator,
    GrievanceCaseSteps steps,
    IRepository<GrievanceCommunication> messages) : GrievanceCommandHandler<SendGrievanceCaseMessageCommand>(validator, steps)
{
    protected override Result Authorize(GrievanceCaseRoles roles, GrievanceCase grievance) =>
        GrievanceRefusals.Allow(roles.CanWorkOnCase, GrievanceRefusals.CaseWork);

    protected override async Task<Result<GrievanceCaseDetailDto>> RunAsync(OpenedCase opened, SendGrievanceCaseMessageCommand command, CancellationToken cancellationToken)
    {
        var message = opened.Case.PostTeamMessage(command.Body, Steps.Actor(opened.Roles.WorkRole), Steps.UtcNow);
        if (message.IsFailure)
        {
            return Result.Failure<GrievanceCaseDetailDto>(message.Error);
        }

        messages.Add(message.Value);
        return await Steps.CommitAsync(opened, new StepEffects(GrievanceAuditAction.SendMessage, "to-submitter", opened.Roles.WorkRole) { TellSubmitter = true }, cancellationToken);
    }
}

/// <summary>
/// An investigation note (management Edit or an assigned investigator with Edit) on an open case. Append-only; the case row does not
/// change, so its row version is only checked for its format.
/// </summary>
internal sealed class AddGrievanceNoteHandler(
    IValidator<AddGrievanceNoteCommand> validator,
    GrievanceCaseSteps steps,
    IRepository<GrievanceInvestigationNote> notes) : GrievanceCommandHandler<AddGrievanceNoteCommand>(validator, steps)
{
    protected override bool ChecksRowVersion => false;

    protected override Result Authorize(GrievanceCaseRoles roles, GrievanceCase grievance) =>
        GrievanceRefusals.Allow(roles.CanWorkOnCase, GrievanceRefusals.CaseWork);

    protected override async Task<Result<GrievanceCaseDetailDto>> RunAsync(OpenedCase opened, AddGrievanceNoteCommand command, CancellationToken cancellationToken)
    {
        var open = opened.Case.EnsureInvestigationOpen();
        if (open.IsFailure)
        {
            return Result.Failure<GrievanceCaseDetailDto>(open.Error);
        }

        var kind = GrievanceText.NoteKind.Parse(command.Kind);
        var note = GrievanceInvestigationNote.Create(
            opened.Case.Id, kind, command.Body, command.InterviewDate, command.IntervieweeText, Steps.Actor(opened.Roles.WorkRole), Steps.Today, Steps.UtcNow);
        if (note.IsFailure)
        {
            return Result.Failure<GrievanceCaseDetailDto>(note.Error);
        }

        notes.Add(note.Value);
        return await Steps.CommitAsync(
            opened,
            new StepEffects(GrievanceAuditAction.AddNote, "kind:" + GrievanceText.NoteKind.ToText(kind), opened.Roles.WorkRole),
            cancellationToken,
            caseChanged: false);
    }
}
