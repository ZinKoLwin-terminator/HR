namespace Apex.Hrms.Application.Abstractions;

/// <summary>
/// Which employees hold a grant through an account that can sign in (account and role Active, the employee has not left). Used to
/// offer the people a module may assign work to (grievance investigators), never to decide what the caller may do.
/// </summary>
public interface IPermissionHolders
{
    /// <summary>The ids of those employees, composable with the <see cref="IReadModel"/> queries of the same request.</summary>
    IQueryable<int> EmployeesHolding(string moduleKey, string action);
}
