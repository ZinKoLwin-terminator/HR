using System.Linq.Expressions;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Security;

/// <summary>How many employees' records of a module the caller sees and acts for.</summary>
internal enum AccessTier
{
    /// <summary>Only their own employee record.</summary>
    Own,

    /// <summary>Their own record and the records of their direct reports.</summary>
    Team,

    /// <summary>Every employee.</summary>
    All,
}

/// <summary>
/// The row-level rules of the per-employee HR data, worked out from the caller's grants (<see cref="ICallerPermissions"/>) and
/// team (<see cref="ICallerTeam"/>). Every rule below is decided inside the database predicate by the access scopes.
/// <list type="bullet">
/// <item>Leave and attendance (per module): <c>Approve</c> and <c>employees:Edit</c> is <see cref="AccessTier.All"/> (HR staff);
/// <c>Approve</c> without <c>employees:Edit</c> is <see cref="AccessTier.Team"/> (managers); anyone else is <see cref="AccessTier.Own"/>.</item>
/// <item>Employees: <c>employees:Edit</c> or <c>payroll:View</c> sees every employee; anyone else sees themselves and their direct reports.</item>
/// <item>Salary figures (payslips, salary assignments, the salary summary): <c>payroll:Approve</c> sees everyone; anyone else only themselves.</item>
/// </list>
/// A caller with no linked employee sees only what their tier gives to everyone, and nothing otherwise.
/// </summary>
internal sealed class SelfService(ICurrentUser user, ICallerPermissions permissions, ICallerTeam team)
{
    public const string OwnOnlyMessage = "You can only act for your own employee record.";

    public const string TeamOnlyMessage = "You can only act for your own record and your direct reports.";

    public AccessTier TierOf(string module)
    {
        if (!permissions.Has(module, "Approve"))
        {
            return AccessTier.Own;
        }

        return permissions.Has("employees", "Edit") ? AccessTier.All : AccessTier.Team;
    }

    /// <summary>True when the caller may see or act for <paramref name="employeeId"/> in <paramref name="module"/> (leave or attendance).</summary>
    public bool CanAccess(string module, int employeeId) => TierOf(module) switch
    {
        AccessTier.All => true,
        AccessTier.Team => IsSelfOrReport(employeeId),
        _ => user.EmployeeId is { } own && own == employeeId,
    };

    /// <summary>A failure (Forbidden) when the caller may not act for the employee, like the punch rule.</summary>
    public Result EnsureCanActFor(string module, int employeeId) =>
        CanAccess(module, employeeId)
            ? Result.Success()
            : Result.Failure(AppError.Forbidden(TierOf(module) == AccessTier.Team ? TeamOnlyMessage : OwnOnlyMessage));

    /// <summary>
    /// The rows of a leave or attendance table the caller may see: all, their own and their team's, their own, or none.
    /// <paramref name="inSet"/> builds the predicate "the row's employee is one of these ids".
    /// </summary>
    public Expression<Func<T, bool>> Rows<T>(string module, Func<int[], Expression<Func<T, bool>>> inSet) =>
        TierOf(module) switch
        {
            AccessTier.All => Predicates.Always<T>(),
            AccessTier.Team => SetOrNever(SelfAndReports(), inSet),
            _ => SetOrNever(Own(), inSet),
        };

    /// <summary>True when the caller sees every employee of the employees module (HR staff and payroll staff).</summary>
    public bool SeesEveryEmployee => permissions.Has("employees", "Edit") || permissions.Has("payroll", "View");

    /// <summary>The employee rows (and rows owned by an employee, such as documents) the caller may see: all, or themselves and their direct reports.</summary>
    public Expression<Func<T, bool>> EmployeeRows<T>(Func<int[], Expression<Func<T, bool>>> inSet) =>
        SeesEveryEmployee ? Predicates.Always<T>() : SetOrNever(SelfAndReports(), inSet);

    /// <summary>True when the caller may see the salary figures of <paramref name="employeeId"/>: payroll:Approve, or their own.</summary>
    public bool CanSeeSalaryOf(int employeeId) =>
        permissions.Has("payroll", "Approve") || (user.EmployeeId is { } own && own == employeeId);

    /// <summary>The rows of salary data the caller may see: all with payroll:Approve, otherwise their own employee's.</summary>
    public Expression<Func<T, bool>> SalaryRows<T>(Func<int[], Expression<Func<T, bool>>> inSet) =>
        permissions.Has("payroll", "Approve") ? Predicates.Always<T>() : SetOrNever(Own(), inSet);

    private bool IsSelfOrReport(int employeeId) =>
        (user.EmployeeId is { } own && own == employeeId) || team.DirectReportIds.Contains(employeeId);

    private int[] Own() => user.EmployeeId is { } own ? [own] : [];

    private int[] SelfAndReports() => user.EmployeeId is { } own ? [own, .. team.DirectReportIds] : [];

    private static Expression<Func<T, bool>> SetOrNever<T>(int[] ids, Func<int[], Expression<Func<T, bool>>> inSet) =>
        ids.Length == 0 ? Predicates.Never<T>() : inSet(ids);
}
