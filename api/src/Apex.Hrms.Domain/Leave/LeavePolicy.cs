using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;

namespace Apex.Hrms.Domain.Leave;

/// <summary>
/// The editable fields of a leave policy. A null job grade or employment type means "applies to all". Status changes
/// through Activate and Deactivate.
/// </summary>
public sealed record LeavePolicyDetails(
    string Name,
    int LeaveTypeId,
    int? JobGradeId,
    EmploymentType? EmploymentType,
    decimal DaysPerYear,
    LeaveAccrual Accrual,
    short MinServiceMonths)
{
    /// <summary>Trims the name. Idempotent.</summary>
    public LeavePolicyDetails Normalized() => this with { Name = TextNormalization.Clean(Name) };
}

/// <summary>
/// An entitlement rule (lve.LeavePolicy): the most specific active policy for an employee's grade and employment type
/// decides the yearly days (see <see cref="LeaveEntitlement"/>). It has no code, so it is not a <see cref="CodedRecord"/>.
/// </summary>
public sealed class LeavePolicy : AuditedEntity
{
    private LeavePolicy()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public int LeaveTypeId { get; private set; }

    public int? JobGradeId { get; private set; }

    public EmploymentType? EmploymentType { get; private set; }

    public decimal DaysPerYear { get; private set; }

    public LeaveAccrual Accrual { get; private set; }

    public short MinServiceMonths { get; private set; }

    public RecordStatus Status { get; private set; }

    public bool IsActive => Status == RecordStatus.Active;

    public static Result<LeavePolicy> Create(LeavePolicyDetails details, RecordStatus status)
    {
        var policy = new LeavePolicy();
        var result = policy.Update(details);
        if (result.IsFailure)
        {
            return Result.Failure<LeavePolicy>(result.Error);
        }

        policy.Status = status;
        return Result.Success(policy);
    }

    /// <summary>Checks the rules that need no other record, without changing anything.</summary>
    public static Result Validate(LeavePolicyDetails details)
    {
        var normalized = details.Normalized();
        return new Violations()
            .Require("name", "Policy name", normalized.Name)
            .AddIf(normalized.DaysPerYear < 0, "daysPerYear", "Must not be negative.")
            .AddIf(normalized.MinServiceMonths < 0, "minServiceMonths", "Must not be negative.")
            .ToResult();
    }

    /// <summary>Applies the details, or changes nothing when any rule is violated. Status is left untouched.</summary>
    public Result Update(LeavePolicyDetails details)
    {
        var valid = Validate(details);
        if (valid.IsFailure)
        {
            return valid;
        }

        var normalized = details.Normalized();
        Name = normalized.Name;
        LeaveTypeId = normalized.LeaveTypeId;
        JobGradeId = normalized.JobGradeId;
        EmploymentType = normalized.EmploymentType;
        DaysPerYear = normalized.DaysPerYear;
        Accrual = normalized.Accrual;
        MinServiceMonths = normalized.MinServiceMonths;
        return Result.Success();
    }

    /// <summary>Makes the policy active. Idempotent. The caller checks that no other active policy covers the same group.</summary>
    public Result Activate()
    {
        Status = RecordStatus.Active;
        return Result.Success();
    }

    /// <summary>Makes the policy inactive. Idempotent.</summary>
    public Result Deactivate()
    {
        Status = RecordStatus.Inactive;
        return Result.Success();
    }
}
