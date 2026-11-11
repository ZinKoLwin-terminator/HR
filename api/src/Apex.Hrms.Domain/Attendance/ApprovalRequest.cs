using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Attendance;

/// <summary>Who decides a request: the signed-in account, the employee linked to it (null for the built-in administrator) and the name to keep.</summary>
public sealed record ApprovalActor(int? UserId, int? EmployeeId, string Name);

public enum ApprovalDecision
{
    Approve,
    Reject,
}

/// <summary>
/// What an attendance adjustment and an overtime request share: they start Pending and are decided once, by someone
/// other than the employee. Rejecting needs remarks; the name of the person who decided is kept as it was then.
/// </summary>
public abstract class ApprovalRequest : AuditedEntity
{
    public const int RemarksMaxLength = 250;

    public int EmployeeId { get; protected set; }

    public DateOnly Date { get; protected set; }

    public string Reason { get; protected set; } = string.Empty;

    public ApprovalStatus Status { get; protected set; }

    /// <summary>The day the request was made.</summary>
    public DateOnly RequestedOn { get; protected set; }

    public int? DecidedByUserId { get; private set; }

    public string? DecidedByName { get; private set; }

    public DateOnly? DecidedOn { get; private set; }

    public string? Remarks { get; private set; }

    /// <summary>What the request is called in messages and in the remarks of the record it changes, e.g. "ADJ-0004".</summary>
    public abstract string Label { get; }

    /// <summary>Only a pending request may be deleted.</summary>
    public Result EnsureCanDelete() =>
        Status != ApprovalStatus.Pending
            ? Result.Failure(AppError.Conflict($"Only pending requests can be deleted (this one is {ApprovalStatusText.ToText(Status).ToLowerInvariant()})."))
            : Result.Success();

    /// <summary>
    /// Pending to Approved or Rejected. Nobody decides their own request; the built-in administrator is linked to no
    /// employee, so the rule cannot apply to it. Rejecting needs remarks; approving does not.
    /// </summary>
    public Result Decide(ApprovalDecision decision, ApprovalActor actor, string? remarks, DateOnly today)
    {
        if (Status != ApprovalStatus.Pending)
        {
            return Result.Failure(AppError.Conflict(
                $"This request was already {ApprovalStatusText.ToText(Status).ToLowerInvariant()}."));
        }

        if (actor.EmployeeId is { } employeeId && employeeId == EmployeeId)
        {
            return Result.Failure(AppError.Conflict("You cannot decide your own request."));
        }

        var cleaned = TextNormalization.CleanOptional(remarks);
        if (decision == ApprovalDecision.Reject && cleaned is null)
        {
            return Result.Failure(AppError.Validation(
                "Give a reason for rejecting the request.",
                "remarks",
                "A reason is required."));
        }

        if (cleaned is { Length: > RemarksMaxLength })
        {
            return Result.Failure(AppError.Validation(
                $"Remarks must be at most {RemarksMaxLength} characters.",
                "remarks",
                $"Remarks must be at most {RemarksMaxLength} characters."));
        }

        Status = decision == ApprovalDecision.Approve ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
        DecidedByUserId = actor.UserId;
        DecidedByName = actor.Name;
        DecidedOn = today;
        Remarks = cleaned ?? (decision == ApprovalDecision.Approve ? "Approved." : "Rejected.");
        return Result.Success();
    }

    /// <summary>Checks the date and reason every request has.</summary>
    protected static Violations CheckCommon(Violations violations, DateOnly date, DateOnly today, string reason) =>
        violations
            .AddIf(date > today, "date", "Choose today or an earlier date.", "The date cannot be in the future.")
            .Require("reason", "Reason", reason);

    protected void StartPending(int employeeId, DateOnly date, string reason, DateOnly today)
    {
        EmployeeId = employeeId;
        Date = date;
        Reason = reason;
        Status = ApprovalStatus.Pending;
        RequestedOn = today;
    }
}
