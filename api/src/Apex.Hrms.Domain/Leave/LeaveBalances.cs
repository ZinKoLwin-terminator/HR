using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;

namespace Apex.Hrms.Domain.Leave;

/// <summary>One leave type's figures for one employee and year.</summary>
/// <param name="LeaveTypeId">The leave type.</param>
/// <param name="Code">The type's code.</param>
/// <param name="Name">The type's name.</param>
/// <param name="Color">The type's display colour.</param>
/// <param name="Entitled">Days granted for the year (after policy, accrual and service rules).</param>
/// <param name="Taken">Days of approved leave that starts in the year.</param>
/// <param name="Pending">Days of submitted leave, not yet decided.</param>
/// <param name="Available">Entitled minus taken minus pending, never below zero.</param>
public sealed record LeaveBalanceLine(
    int LeaveTypeId,
    string Code,
    string Name,
    string Color,
    decimal Entitled,
    decimal Taken,
    decimal Pending,
    decimal Available);

/// <summary>
/// Balances are never stored: they are worked out from the leave applications whenever they are asked for. Only active
/// types the employee is eligible for are returned. Leave counts in the year it starts.
/// </summary>
public static class LeaveBalances
{
    public static IReadOnlyList<LeaveBalanceLine> Compute(
        IEnumerable<LeaveType> types,
        IEnumerable<LeaveApplication> applications,
        Gender gender,
        int year,
        Func<LeaveType, decimal> entitlementFor)
    {
        var inYear = applications.Where(application => application.StartDate.Year == year).ToList();
        return
        [
            .. types
                .Where(type => type.Status == RecordStatus.Active && (type.EligibleGender is null || type.EligibleGender == gender))
                .Select(type =>
                {
                    var mine = inYear.Where(application => application.LeaveTypeId == type.Id).ToList();
                    var taken = Sum(mine, LeaveStatus.Approved);
                    var pending = Sum(mine, LeaveStatus.Submitted);
                    var entitled = entitlementFor(type);
                    return new LeaveBalanceLine(
                        type.Id,
                        type.Code,
                        type.Name,
                        type.Color,
                        entitled,
                        taken,
                        pending,
                        Math.Max(0, entitled - taken - pending));
                }),
        ];
    }

    private static decimal Sum(IEnumerable<LeaveApplication> applications, LeaveStatus status) =>
        applications.Where(application => application.Status == status).Sum(application => application.Days);
}
