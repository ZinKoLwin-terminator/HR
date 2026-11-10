using System.Linq.Expressions;
using Apex.Hrms.Application.Organization;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Leave;

namespace Apex.Hrms.Application.Leave.LeavePolicies;

internal sealed class LeavePolicyQuerySpecification : IQuerySpecification<LeavePolicy, LeavePolicyFilter>
{
    public IReadOnlyDictionary<string, Expression<Func<LeavePolicy, object>>> SortKeys { get; } =
        new Dictionary<string, Expression<Func<LeavePolicy, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = p => p.Id,
            ["name"] = p => p.Name,
            ["leaveTypeId"] = p => p.LeaveTypeId,
            ["daysPerYear"] = p => p.DaysPerYear,
            ["minServiceMonths"] = p => p.MinServiceMonths,
            ["status"] = p => p.Status,
        };

    public Expression<Func<LeavePolicy, bool>> Search(string term) =>
        Predicates.ContainsAny<LeavePolicy>(term, p => p.Name);

    public Expression<Func<LeavePolicy, bool>> Filter(LeavePolicyFilter filter) => Predicates.Always<LeavePolicy>()
        .And(StatusFilter(filter.Status))
        .And(filter.LeaveTypeId is { } typeId ? p => p.LeaveTypeId == typeId : Predicates.Always<LeavePolicy>())
        .And(filter.JobGradeId is { } gradeId ? p => p.JobGradeId == gradeId : Predicates.Always<LeavePolicy>())
        .And(EmploymentTypeFilter(filter.EmploymentType))
        .And(AccrualFilter(filter.Accrual));

    private static Expression<Func<LeavePolicy, bool>> StatusFilter(string? text)
    {
        if (!FilterPredicates.IsSet(text))
        {
            return Predicates.Always<LeavePolicy>();
        }

        return RecordStatusText.TryParse(text, out var status)
            ? p => p.Status == status
            : Predicates.Never<LeavePolicy>();
    }

    private static Expression<Func<LeavePolicy, bool>> EmploymentTypeFilter(string? text)
    {
        if (!FilterPredicates.IsSet(text))
        {
            return Predicates.Always<LeavePolicy>();
        }

        return EmploymentTypeText.TryParse(text, out var type)
            ? p => p.EmploymentType == type
            : Predicates.Never<LeavePolicy>();
    }

    private static Expression<Func<LeavePolicy, bool>> AccrualFilter(string? text)
    {
        if (!FilterPredicates.IsSet(text))
        {
            return Predicates.Always<LeavePolicy>();
        }

        return LeaveAccrualText.TryParse(text, out var accrual)
            ? p => p.Accrual == accrual
            : Predicates.Never<LeavePolicy>();
    }
}
