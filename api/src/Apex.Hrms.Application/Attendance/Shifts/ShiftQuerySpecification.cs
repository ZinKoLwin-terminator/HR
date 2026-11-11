using System.Linq.Expressions;
using Apex.Hrms.Application.Organization;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Attendance;

namespace Apex.Hrms.Application.Attendance.Shifts;

internal sealed class ShiftQuerySpecification : IQuerySpecification<Shift, ShiftFilter>
{
    public IReadOnlyDictionary<string, Expression<Func<Shift, object>>> SortKeys { get; } =
        new Dictionary<string, Expression<Func<Shift, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = s => s.Id,
            ["code"] = s => s.Code,
            ["name"] = s => s.Name,
            ["startTime"] = s => s.StartTime,
            ["endTime"] = s => s.EndTime,
            ["status"] = s => s.Status,
        };

    public Expression<Func<Shift, bool>> Search(string term) =>
        Predicates.ContainsAny<Shift>(term, s => s.Code, s => s.Name);

    public Expression<Func<Shift, bool>> Filter(ShiftFilter filter) =>
        FilterPredicates.Status<Shift>(filter.Status);
}
