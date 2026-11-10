using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;

namespace Apex.Hrms.Domain.Leave;

/// <summary>What the entitlement rules need to know about an employee.</summary>
public readonly record struct EntitlementProfile(int JobGradeId, EmploymentType EmploymentType, DateOnly JoiningDate);

/// <summary>
/// Yearly entitlement of a leave type for an employee (sample rules, not statutory).
/// <list type="number">
/// <item>Active policies of the type that match the employee's job grade and employment type are candidates; a null
/// policy field matches everyone.</item>
/// <item>The most specific candidate wins (grade match 2 points, employment type match 1 point, then more days).
/// Candidates whose minimum service is not reached are skipped.</item>
/// <item>With no candidate at all the type's default applies. If candidates exist but none is satisfied (too little
/// service) the entitlement is 0.</item>
/// <item>Monthly accrual grants <c>days x month / 12</c> of the as-of month, rounded to half days.</item>
/// </list>
/// </summary>
public static class LeaveEntitlement
{
    public static decimal Resolve(
        LeaveType type,
        IEnumerable<LeavePolicy> policies,
        EntitlementProfile employee,
        DateOnly asOf)
    {
        var candidates = policies
            .Where(policy =>
                policy.Status == RecordStatus.Active
                && policy.LeaveTypeId == type.Id
                && (policy.JobGradeId is null || policy.JobGradeId == employee.JobGradeId)
                && (policy.EmploymentType is null || policy.EmploymentType == employee.EmploymentType))
            .ToList();
        if (candidates.Count == 0)
        {
            return type.DefaultDaysPerYear;
        }

        var service = MonthsBetween(employee.JoiningDate, asOf);
        var chosen = candidates
            .Where(policy => service >= policy.MinServiceMonths)
            .OrderByDescending(Specificity)
            .ThenByDescending(policy => policy.DaysPerYear)
            .ThenBy(policy => policy.Id)
            .FirstOrDefault();
        if (chosen is null)
        {
            return 0;
        }

        return chosen.Accrual == LeaveAccrual.Monthly
            ? RoundToHalfDay(chosen.DaysPerYear * asOf.Month / 12m)
            : chosen.DaysPerYear;
    }

    /// <summary>Whole months between two dates (a started month does not count); never negative.</summary>
    public static int MonthsBetween(DateOnly from, DateOnly to)
    {
        var months = ((to.Year - from.Year) * 12) + (to.Month - from.Month);
        if (to.Day < from.Day)
        {
            months--;
        }

        return Math.Max(0, months);
    }

    private static int Specificity(LeavePolicy policy) =>
        (policy.JobGradeId is not null ? 2 : 0) + (policy.EmploymentType is not null ? 1 : 0);

    private static decimal RoundToHalfDay(decimal days) =>
        Math.Round(days * 2, MidpointRounding.AwayFromZero) / 2;
}
