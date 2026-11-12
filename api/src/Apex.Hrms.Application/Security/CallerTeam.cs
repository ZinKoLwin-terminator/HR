namespace Apex.Hrms.Application.Security;

/// <summary>
/// The employees who report directly to the signed-in caller (<c>hr.Employee.ManagerEmployeeId</c> is the caller's employee),
/// for the current request. Only direct reports count: not indirect reports and not the head of a department. The host fills
/// it next to <see cref="ICallerPermissions"/>; until then nobody reports to the caller, which is the safe side.
/// </summary>
public interface ICallerTeam
{
    IReadOnlyList<int> DirectReportIds { get; }
}

/// <summary>The per-request holder behind <see cref="ICallerTeam"/>.</summary>
public sealed class CallerTeam : ICallerTeam
{
    private IReadOnlyList<int> reports = [];

    /// <summary>True once the host has read the team for this request, so a request with several permission checks reads it once.</summary>
    public bool IsLoaded { get; private set; }

    public IReadOnlyList<int> DirectReportIds => reports;

    public void Set(IReadOnlyList<int> directReportIds)
    {
        reports = directReportIds;
        IsLoaded = true;
    }
}

/// <summary>Reads who reports directly to an employee. Implemented by the persistence layer.</summary>
public interface ITeamDirectory
{
    /// <summary>The ids of the employees whose manager is <paramref name="managerEmployeeId"/> (their direct reports, whatever their status).</summary>
    Task<IReadOnlyList<int>> DirectReportIdsAsync(int managerEmployeeId, CancellationToken cancellationToken);
}
