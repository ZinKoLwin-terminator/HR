using System.Linq.Expressions;
using Apex.Hrms.Application.Organization;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Leave;

namespace Apex.Hrms.Application.Leave.Applications;

internal sealed class LeaveApplicationQuerySpecification : IQuerySpecification<LeaveApplication, LeaveApplicationFilter>
{
    public IReadOnlyDictionary<string, Expression<Func<LeaveApplication, object>>> SortKeys { get; } =
        new Dictionary<string, Expression<Func<LeaveApplication, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = a => a.Id,
            ["applicationNo"] = a => a.ApplicationNo,
            ["employeeId"] = a => a.EmployeeId,
            ["leaveTypeId"] = a => a.LeaveTypeId,
            ["startDate"] = a => a.StartDate,
            ["endDate"] = a => a.EndDate,
            ["days"] = a => a.Days,
            ["status"] = a => a.Status,
            ["appliedOn"] = a => a.AppliedOn,
            ["actionedOn"] = a => a.ActionedOn ?? DateOnly.MinValue,
        };

    public Expression<Func<LeaveApplication, bool>> Search(string term) =>
        Predicates.ContainsAny<LeaveApplication>(term, a => a.ApplicationNo, a => a.Reason);

    public Expression<Func<LeaveApplication, bool>> Filter(LeaveApplicationFilter filter) => Predicates.Always<LeaveApplication>()
        .And(filter.EmployeeId is { } employeeId ? a => a.EmployeeId == employeeId : Predicates.Always<LeaveApplication>())
        .And(filter.LeaveTypeId is { } typeId ? a => a.LeaveTypeId == typeId : Predicates.Always<LeaveApplication>())
        .And(StatusFilter(filter.Status))
        .And(filter.DateFrom is { } from ? a => a.EndDate >= from : Predicates.Always<LeaveApplication>())
        .And(filter.DateTo is { } to ? a => a.StartDate <= to : Predicates.Always<LeaveApplication>());

    private static Expression<Func<LeaveApplication, bool>> StatusFilter(string? text)
    {
        if (!FilterPredicates.IsSet(text))
        {
            return Predicates.Always<LeaveApplication>();
        }

        return LeaveStatusText.TryParse(text, out var status)
            ? a => a.Status == status
            : Predicates.Never<LeaveApplication>();
    }
}
