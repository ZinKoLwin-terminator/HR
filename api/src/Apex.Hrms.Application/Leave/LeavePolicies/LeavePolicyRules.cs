using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Organization;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Leave;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Leave.LeavePolicies;

/// <summary>
/// Rules of a policy that need other records: a unique name, an existing leave type and job grade, and at most one
/// active policy for each leave type, grade and employment type (the database has a unique index for it too).
/// </summary>
internal sealed class LeavePolicyRules(
    IRepository<LeavePolicy> policies,
    IRepository<LeaveType> types,
    IRepository<JobGrade> grades) : ISaveRules<LeavePolicy, LeavePolicyDetails>
{
    public Task<Result> CheckCreateAsync(LeavePolicyDetails details, CancellationToken cancellationToken) =>
        CheckAsync(details, excludedId: 0, willBeActive: false, cancellationToken);

    /// <summary>The same checks for a new policy that starts with <paramref name="status"/>.</summary>
    public Task<Result> CheckCreateAsync(LeavePolicyDetails details, RecordStatus status, CancellationToken cancellationToken) =>
        CheckAsync(details, excludedId: 0, status == RecordStatus.Active, cancellationToken);

    public Task<Result> CheckUpdateAsync(LeavePolicy existing, LeavePolicyDetails details, CancellationToken cancellationToken) =>
        CheckAsync(details, existing.Id, existing.IsActive, cancellationToken);

    /// <summary>Activating a policy needs the group of its current values to be free.</summary>
    public Task<Result> CheckActivationAsync(LeavePolicy policy, CancellationToken cancellationToken) =>
        EnsureNoActiveClashAsync(
            new LeavePolicyDetails(
                policy.Name,
                policy.LeaveTypeId,
                policy.JobGradeId,
                policy.EmploymentType,
                policy.DaysPerYear,
                policy.Accrual,
                policy.MinServiceMonths),
            policy.Id,
            cancellationToken);

    private async Task<Result> CheckAsync(
        LeavePolicyDetails details,
        int excludedId,
        bool willBeActive,
        CancellationToken cancellationToken)
    {
        var name = details.Name;
        var unique = await HierarchyChecks.EnsureUniqueAsync(
            policies, p => p.Name == name && p.Id != excludedId, "Policy name", "name", name, cancellationToken);
        if (unique.IsFailure)
        {
            return unique;
        }

        var violations = new Violations();
        if (await types.FindAsync(details.LeaveTypeId, cancellationToken) is null)
        {
            violations.Add("leaveTypeId", "Choose an existing leave type.", $"Leave type #{details.LeaveTypeId} was not found.");
        }

        if (details.JobGradeId is { } gradeId && await grades.FindAsync(gradeId, cancellationToken) is null)
        {
            violations.Add("jobGradeId", "Choose an existing job grade.", $"Job grade #{gradeId} was not found.");
        }

        if (violations.Any)
        {
            return violations.ToResult();
        }

        return willBeActive ? await EnsureNoActiveClashAsync(details, excludedId, cancellationToken) : Result.Success();
    }

    private async Task<Result> EnsureNoActiveClashAsync(
        LeavePolicyDetails details,
        int excludedId,
        CancellationToken cancellationToken)
    {
        var typeId = details.LeaveTypeId;
        var gradeId = details.JobGradeId;
        var employmentType = details.EmploymentType;
        var clash = await policies.AnyAsync(
            p => p.Id != excludedId
                && p.Status == RecordStatus.Active
                && p.LeaveTypeId == typeId
                && p.JobGradeId == gradeId
                && p.EmploymentType == employmentType,
            cancellationToken);
        return clash
            ? Result.Failure(AppError.Validation(
                "An active policy already exists for this leave type, grade and employment type.",
                "leaveTypeId",
                "Another active policy covers the same group."))
            : Result.Success();
    }
}
