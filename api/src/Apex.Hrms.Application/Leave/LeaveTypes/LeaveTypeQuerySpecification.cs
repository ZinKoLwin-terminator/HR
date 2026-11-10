using System.Linq.Expressions;
using Apex.Hrms.Application.Organization;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Leave;

namespace Apex.Hrms.Application.Leave.LeaveTypes;

internal sealed class LeaveTypeQuerySpecification : IQuerySpecification<LeaveType, LeaveTypeFilter>
{
    public IReadOnlyDictionary<string, Expression<Func<LeaveType, object>>> SortKeys { get; } =
        new Dictionary<string, Expression<Func<LeaveType, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = t => t.Id,
            ["code"] = t => t.Code,
            ["name"] = t => t.Name,
            ["defaultDaysPerYear"] = t => t.DefaultDaysPerYear,
            ["maxConsecutiveDays"] = t => t.MaxConsecutiveDays,
            ["status"] = t => t.Status,
        };

    public Expression<Func<LeaveType, bool>> Search(string term) =>
        Predicates.ContainsAny<LeaveType>(term, t => t.Code, t => t.Name);

    public Expression<Func<LeaveType, bool>> Filter(LeaveTypeFilter filter) =>
        FilterPredicates.Status<LeaveType>(filter.Status);
}
