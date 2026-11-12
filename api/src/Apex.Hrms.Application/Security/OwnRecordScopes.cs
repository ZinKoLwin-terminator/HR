using System.Linq.Expressions;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Attendance;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Leave;
using Apex.Hrms.Domain.Payroll;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.Application.Security;

// Row-level scoping of the per-employee data: see SelfService for the tiers. The scope is applied inside the database
// query by every read, update, delete and command, so a record outside it is simply "not found" (404).

internal sealed class LeaveApplicationScope(SelfService self) : IAccessScope<LeaveApplication>
{
    public Expression<Func<LeaveApplication, bool>> PredicateFor(ICurrentUser user) =>
        self.Rows<LeaveApplication>("leave", ids => application => ids.Contains(application.EmployeeId));
}

internal sealed class AttendanceRecordScope(SelfService self) : IAccessScope<AttendanceRecord>
{
    public Expression<Func<AttendanceRecord, bool>> PredicateFor(ICurrentUser user) =>
        self.Rows<AttendanceRecord>("attendance", ids => record => ids.Contains(record.EmployeeId));
}

internal sealed class AttendanceAdjustmentScope(SelfService self) : IAccessScope<AttendanceAdjustment>
{
    public Expression<Func<AttendanceAdjustment, bool>> PredicateFor(ICurrentUser user) =>
        self.Rows<AttendanceAdjustment>("attendance", ids => adjustment => ids.Contains(adjustment.EmployeeId));
}

internal sealed class OvertimeRecordScope(SelfService self) : IAccessScope<OvertimeRecord>
{
    public Expression<Func<OvertimeRecord, bool>> PredicateFor(ICurrentUser user) =>
        self.Rows<OvertimeRecord>("attendance", ids => overtime => ids.Contains(overtime.EmployeeId));
}

/// <summary>Employees: everyone for HR and payroll staff, otherwise the caller and their direct reports.</summary>
internal sealed class EmployeeAccessScope(SelfService self) : IAccessScope<Employee>
{
    public Expression<Func<Employee, bool>> PredicateFor(ICurrentUser user) =>
        self.EmployeeRows<Employee>(ids => employee => ids.Contains(employee.Id));
}

/// <summary>Employee documents follow the employee they belong to.</summary>
internal sealed class EmployeeDocumentScope(SelfService self) : IAccessScope<EmployeeDocument>
{
    public Expression<Func<EmployeeDocument, bool>> PredicateFor(ICurrentUser user) =>
        self.EmployeeRows<EmployeeDocument>(ids => document => ids.Contains(document.EmployeeId));
}

/// <summary>Payslips: payroll:Approve sees all, anyone else only their own.</summary>
internal sealed class PayslipScope(SelfService self) : IAccessScope<Payslip>
{
    public Expression<Func<Payslip, bool>> PredicateFor(ICurrentUser user) =>
        self.SalaryRows<Payslip>(ids => payslip => ids.Contains(payslip.EmployeeId));
}

/// <summary>Salary assignments carry the same figures as payslips and follow the same rule.</summary>
internal sealed class SalaryAssignmentScope(SelfService self) : IAccessScope<SalaryAssignment>
{
    public Expression<Func<SalaryAssignment, bool>> PredicateFor(ICurrentUser user) =>
        self.SalaryRows<SalaryAssignment>(ids => assignment => ids.Contains(assignment.EmployeeId));
}

internal static class OwnRecordScopeExtensions
{
    public static IServiceCollection AddOwnRecordScopes(this IServiceCollection services) => services
        .AddScoped<IAccessScope<LeaveApplication>, LeaveApplicationScope>()
        .AddScoped<IAccessScope<AttendanceRecord>, AttendanceRecordScope>()
        .AddScoped<IAccessScope<AttendanceAdjustment>, AttendanceAdjustmentScope>()
        .AddScoped<IAccessScope<OvertimeRecord>, OvertimeRecordScope>()
        .AddScoped<IAccessScope<Employee>, EmployeeAccessScope>()
        .AddScoped<IAccessScope<EmployeeDocument>, EmployeeDocumentScope>()
        .AddScoped<IAccessScope<Payslip>, PayslipScope>()
        .AddScoped<IAccessScope<SalaryAssignment>, SalaryAssignmentScope>();
}
