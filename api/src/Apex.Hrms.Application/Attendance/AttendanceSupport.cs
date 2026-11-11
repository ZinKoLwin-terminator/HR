using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Attendance;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;

namespace Apex.Hrms.Application.Attendance;

/// <summary>Finds the shift an employee works, and the record of an employee's day.</summary>
internal sealed class AttendanceLookup(IRepository<Shift> shifts, IRepository<AttendanceRecord> records)
{
    /// <summary>
    /// The employee's own shift, or the first shift (lowest id) when the employee has none or it no longer exists, as the
    /// frontend's mock does. Null only when no shift exists at all.
    /// </summary>
    public async Task<Shift?> ShiftForAsync(int? shiftId, CancellationToken cancellationToken)
    {
        if (shiftId is { } id
            && await shifts.FindAsync(id, Predicates.Always<Shift>(), cancellationToken) is { } own)
        {
            return own;
        }

        var all = await shifts.ListAsync(Predicates.Always<Shift>(), cancellationToken);
        return all.Count == 0 ? null : all[0];
    }

    /// <summary>The record of the day, tracked so that it can be changed; null when the employee has none that day.</summary>
    public async Task<AttendanceRecord?> RecordOfAsync(int employeeId, DateOnly date, CancellationToken cancellationToken)
    {
        var found = await records.ListAsync(record => record.EmployeeId == employeeId && record.Date == date, cancellationToken);
        return found.Count == 0 ? null : await records.FindAsync(found[0].Id, cancellationToken);
    }
}

/// <summary>Who is acting, taken from the signed-in user.</summary>
internal static class AttendanceActors
{
    public static Result<ApprovalActor> ActorOf(ICurrentUser user) =>
        user is { IsAuthenticated: true, UserId: not null }
            ? Result.Success(new ApprovalActor(user.UserId, user.EmployeeId, user.DisplayName))
            : Result.Failure<ApprovalActor>(AppError.Unauthorized("Sign in to act on an attendance request."));

    public static string FullName(Employee employee) => $"{employee.FirstName} {employee.LastName}";
}
