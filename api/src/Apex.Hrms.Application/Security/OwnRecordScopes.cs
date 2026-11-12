using System.Linq.Expressions;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Attendance;
using Apex.Hrms.Domain.Leave;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.Application.Security;

// Row-level scoping of the per-employee records of leave and attendance: see SelfService. The scope is applied inside the
// database query by every read, update, delete and command, so another employee's record is simply "not found" (404).

internal sealed class LeaveApplicationScope(SelfService self) : IAccessScope<LeaveApplication>
{
    public Expression<Func<LeaveApplication, bool>> PredicateFor(ICurrentUser user) =>
        self.Rows<LeaveApplication>("leave", own => application => application.EmployeeId == own);
}

internal sealed class AttendanceRecordScope(SelfService self) : IAccessScope<AttendanceRecord>
{
    public Expression<Func<AttendanceRecord, bool>> PredicateFor(ICurrentUser user) =>
        self.Rows<AttendanceRecord>("attendance", own => record => record.EmployeeId == own);
}

internal sealed class AttendanceAdjustmentScope(SelfService self) : IAccessScope<AttendanceAdjustment>
{
    public Expression<Func<AttendanceAdjustment, bool>> PredicateFor(ICurrentUser user) =>
        self.Rows<AttendanceAdjustment>("attendance", own => adjustment => adjustment.EmployeeId == own);
}

internal sealed class OvertimeRecordScope(SelfService self) : IAccessScope<OvertimeRecord>
{
    public Expression<Func<OvertimeRecord, bool>> PredicateFor(ICurrentUser user) =>
        self.Rows<OvertimeRecord>("attendance", own => overtime => overtime.EmployeeId == own);
}

internal static class OwnRecordScopeExtensions
{
    public static IServiceCollection AddOwnRecordScopes(this IServiceCollection services) => services
        .AddScoped<IAccessScope<LeaveApplication>, LeaveApplicationScope>()
        .AddScoped<IAccessScope<AttendanceRecord>, AttendanceRecordScope>()
        .AddScoped<IAccessScope<AttendanceAdjustment>, AttendanceAdjustmentScope>()
        .AddScoped<IAccessScope<OvertimeRecord>, OvertimeRecordScope>();
}
