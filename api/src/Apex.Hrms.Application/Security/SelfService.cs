using System.Linq.Expressions;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Security;

/// <summary>
/// Own-record rule of the HR modules: a caller who holds <c>Approve</c> in a module (HR, managers, administrators) sees and
/// acts for everyone; anyone else sees and acts only for their own employee. A caller with no linked employee and no Approve
/// sees nothing. Team scoping for managers is not implemented: an approver sees the whole module.
/// </summary>
internal sealed class SelfService(ICurrentUser user, ICallerPermissions permissions)
{
    public const string OwnOnlyMessage = "You can only act for your own employee record.";

    public bool SeesAll(string module) => permissions.Has(module, "Approve");

    /// <summary>True when the caller may see or act for <paramref name="employeeId"/>.</summary>
    public bool CanAccess(string module, int employeeId) =>
        SeesAll(module) || (user.EmployeeId is { } own && own == employeeId);

    /// <summary>A failure (Forbidden) when the caller may not act for the employee, like the punch rule.</summary>
    public Result EnsureCanActFor(string module, int employeeId) =>
        CanAccess(module, employeeId) ? Result.Success() : Result.Failure(AppError.Forbidden(OwnOnlyMessage));

    /// <summary>The rows whose employee the caller may see: all, their own, or none.</summary>
    public Expression<Func<T, bool>> Rows<T>(string module, Func<int, Expression<Func<T, bool>>> ownedBy) =>
        SeesAll(module)
            ? Predicates.Always<T>()
            : user.EmployeeId is { } own ? ownedBy(own) : Predicates.Never<T>();
}
