using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Leave;

/// <summary>
/// The fields of a leave application the requester controls. The number of days is not one of them: it is worked out
/// from the dates (see <see cref="LeaveApplication.DaysFor"/>).
/// </summary>
public sealed record LeaveApplicationDetails(
    int EmployeeId,
    int LeaveTypeId,
    DateOnly StartDate,
    DateOnly EndDate,
    bool HalfDay,
    string Reason,
    string? DocumentName)
{
    /// <summary>Trims text and turns a blank document name into null. Idempotent.</summary>
    public LeaveApplicationDetails Normalized() => this with
    {
        Reason = TextNormalization.Clean(Reason),
        DocumentName = TextNormalization.CleanOptional(DocumentName),
    };
}

/// <summary>Who acts on an application: the signed-in account, the employee linked to it (null for the built-in administrator) and the name to keep.</summary>
public sealed record LeaveActor(int? UserId, int? EmployeeId, string Name);

public enum LeaveDecision
{
    Approve,
    Reject,
}

/// <summary>
/// A request for leave (lve.LeaveApplication). It moves through Draft, Submitted and then Approved, Rejected or
/// Cancelled; every move is a method here that refuses what the transition table does not allow:
/// <list type="bullet">
/// <item>Draft to Submitted (<see cref="Submit"/>)</item>
/// <item>Submitted to Approved or Rejected (<see cref="Decide"/>); nobody decides their own request</item>
/// <item>Submitted, or Approved leave that has not started, to Cancelled (<see cref="Cancel"/>)</item>
/// </list>
/// Rules that need other records (overlap, balance, eligibility) live in the Application layer.
/// </summary>
public sealed class LeaveApplication : AuditedEntity
{
    public const string DefaultApproveRemarks = "Approved.";
    public const string DefaultCancelRemarks = "Cancelled.";
    public const decimal HalfDayDays = 0.5m;

    private LeaveApplication()
    {
    }

    public string ApplicationNo { get; private set; } = string.Empty;

    public int EmployeeId { get; private set; }

    public int LeaveTypeId { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    /// <summary>Working days requested; 0.5 for a half day.</summary>
    public decimal Days { get; private set; }

    public bool HalfDay { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    /// <summary>The name of the supporting document. Only the name is kept; no file is stored yet.</summary>
    public string? DocumentName { get; private set; }

    public LeaveStatus Status { get; private set; }

    /// <summary>The day the application was created, and again the day it was submitted.</summary>
    public DateOnly AppliedOn { get; private set; }

    /// <summary>The employee who approved or rejected; null when the built-in administrator decided.</summary>
    public int? ApproverEmployeeId { get; private set; }

    public int? ActionedByUserId { get; private set; }

    /// <summary>The display name of whoever approved, rejected or cancelled, as it was at that time.</summary>
    public string? ActionedByName { get; private set; }

    public DateOnly? ActionedOn { get; private set; }

    public string? ApproverRemarks { get; private set; }

    /// <summary>Open leave that still blocks other requests and counts against the balance: Submitted or Approved.</summary>
    public bool IsCommitted => Status is LeaveStatus.Submitted or LeaveStatus.Approved;

    /// <summary>The days a request is for: 0.5 for a half day, otherwise the working days between the dates.</summary>
    public static decimal DaysFor(LeaveApplicationDetails details, int workingDays) =>
        details.HalfDay ? HalfDayDays : workingDays;

    /// <summary>Checks the rules that need no other record, without changing anything.</summary>
    public static Result Validate(LeaveApplicationDetails details)
    {
        var normalized = details.Normalized();
        return new Violations()
            .Require("reason", "Reason", normalized.Reason)
            .AddIf(
                normalized.EndDate < normalized.StartDate,
                "endDate",
                "Must be on or after the start date.",
                "End date must be on or after the start date.")
            .AddIf(
                normalized.HalfDay && normalized.StartDate != normalized.EndDate,
                "endDate",
                "A half day covers a single date.",
                "A half day must start and end on the same date.")
            .ToResult();
    }

    /// <summary>Starts a new application as a Draft. <paramref name="days"/> comes from <see cref="DaysFor"/>.</summary>
    public static Result<LeaveApplication> Create(
        LeaveApplicationDetails details,
        decimal days,
        string applicationNo,
        DateOnly today)
    {
        var application = new LeaveApplication();
        var applied = application.Apply(details, days);
        if (applied.IsFailure)
        {
            return Result.Failure<LeaveApplication>(applied.Error);
        }

        application.ApplicationNo = applicationNo;
        application.Status = LeaveStatus.Draft;
        application.AppliedOn = today;
        return Result.Success(application);
    }

    /// <summary>Changes a draft. Anything that has been submitted is changed by cancelling it, not by editing it.</summary>
    public Result Update(LeaveApplicationDetails details, decimal days)
    {
        var allowed = EnsureCanUpdate();
        return allowed.IsFailure ? allowed : Apply(details, days);
    }

    /// <summary>The status rule of changing: only a draft.</summary>
    public Result EnsureCanUpdate() =>
        Status != LeaveStatus.Draft
            ? Result.Failure(AppError.Conflict("Only draft applications can be changed. Cancel a submitted one instead."))
            : Result.Success();

    /// <summary>Only a draft application can be deleted.</summary>
    public Result EnsureCanDelete() =>
        Status != LeaveStatus.Draft
            ? Result.Failure(AppError.Conflict("Only draft applications can be deleted. Cancel a submitted one instead."))
            : Result.Success();

    /// <summary>The status rule of submitting. The balance, overlap and document rules are the caller's (they need other records).</summary>
    public Result EnsureCanSubmit() =>
        Status != LeaveStatus.Draft
            ? Result.Failure(AppError.Conflict($"Only a draft can be submitted (this one is {LeaveStatusText.ToText(Status)})."))
            : Result.Success();

    /// <summary>Draft to Submitted. The application date becomes the day it was submitted.</summary>
    public Result Submit(DateOnly today)
    {
        var allowed = EnsureCanSubmit();
        if (allowed.IsFailure)
        {
            return allowed;
        }

        Status = LeaveStatus.Submitted;
        AppliedOn = today;
        return Result.Success();
    }

    /// <summary>
    /// Submitted to Approved or Rejected. A person never decides their own request; the built-in administrator is linked
    /// to no employee, so the rule cannot apply to it (the database also refuses an approver equal to the employee).
    /// Rejecting needs a reason; approving does not.
    /// </summary>
    public Result Decide(LeaveDecision decision, LeaveActor actor, string? remarks, DateOnly today)
    {
        if (Status != LeaveStatus.Submitted)
        {
            return Result.Failure(AppError.Conflict(
                $"Only submitted applications can be decided (this one is {LeaveStatusText.ToText(Status)})."));
        }

        if (actor.EmployeeId is { } employeeId && employeeId == EmployeeId)
        {
            return Result.Failure(AppError.Conflict("You cannot decide your own leave request."));
        }

        var cleaned = TextNormalization.CleanOptional(remarks);
        if (decision == LeaveDecision.Reject && cleaned is null)
        {
            return Result.Failure(AppError.Validation(
                "Give a reason for rejecting the request.",
                "remarks",
                "A reason is required."));
        }

        Status = decision == LeaveDecision.Approve ? LeaveStatus.Approved : LeaveStatus.Rejected;
        ApproverEmployeeId = actor.EmployeeId;
        RecordAction(actor, today, cleaned ?? DefaultApproveRemarks);
        return Result.Success();
    }

    /// <summary>Submitted leave, or approved leave that has not started, to Cancelled.</summary>
    public Result Cancel(LeaveActor actor, string? remarks, DateOnly today)
    {
        if (Status is not (LeaveStatus.Submitted or LeaveStatus.Approved))
        {
            return Result.Failure(AppError.Conflict(
                $"Only submitted or approved leave can be cancelled (this one is {LeaveStatusText.ToText(Status)})."));
        }

        if (Status == LeaveStatus.Approved && StartDate <= today)
        {
            return Result.Failure(AppError.Conflict("Leave that has already started cannot be cancelled."));
        }

        Status = LeaveStatus.Cancelled;
        RecordAction(actor, today, TextNormalization.CleanOptional(remarks) ?? DefaultCancelRemarks);
        return Result.Success();
    }

    /// <summary>True when the leave dates of the two applications share at least one day.</summary>
    public bool Overlaps(DateOnly startDate, DateOnly endDate) => StartDate <= endDate && EndDate >= startDate;

    private Result Apply(LeaveApplicationDetails details, decimal days)
    {
        var valid = Validate(details);
        if (valid.IsFailure)
        {
            return valid;
        }

        if (days <= 0)
        {
            return Result.Failure(AppError.Validation(
                "The selected dates contain no working days.",
                "endDate",
                "Choose dates that include a working day."));
        }

        var normalized = details.Normalized();
        EmployeeId = normalized.EmployeeId;
        LeaveTypeId = normalized.LeaveTypeId;
        StartDate = normalized.StartDate;
        EndDate = normalized.EndDate;
        HalfDay = normalized.HalfDay;
        Days = days;
        Reason = normalized.Reason;
        DocumentName = normalized.DocumentName;
        return Result.Success();
    }

    private void RecordAction(LeaveActor actor, DateOnly today, string remarks)
    {
        ActionedByUserId = actor.UserId;
        ActionedByName = actor.Name;
        ActionedOn = today;
        ApproverRemarks = remarks;
    }
}
