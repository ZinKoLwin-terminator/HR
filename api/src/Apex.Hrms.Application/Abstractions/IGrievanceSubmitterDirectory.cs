using Apex.Hrms.Domain.Grievances;

namespace Apex.Hrms.Application.Abstractions;

/// <summary>
/// Who submitted which grievance (grv.CaseSubmitter). The rows have no identity of their own (the key is the case id), so they are
/// reached through this port and never through <see cref="IReadModel"/>. The pseudonym of an anonymous submitter is worked out here
/// (<see cref="IGrievancePseudonyms"/>) and compared in the database only: the application never compares pseudonyms itself, never
/// returns one and never logs one.
/// </summary>
public interface IGrievanceSubmitterDirectory
{
    /// <summary>Every submitter row, not tracked; composable with the <see cref="IReadModel"/> queries of the same request.</summary>
    IQueryable<GrievanceCaseSubmitter> Query();

    /// <summary>
    /// The ids of the cases the employee submitted: named (EmployeeId = the employee) or anonymous (SubmitterPseudonym = the employee's
    /// pseudonym). Read by TWO separate statements (one takes only the employee id, the other only the pseudonym) and returned as a
    /// materialized list, so no SQL statement ever carries both: a trace capturing one statement's parameters cannot link an employee to
    /// their anonymous cases. Callers pass the ids on; they never compose this with a query that carries the employee id.
    /// </summary>
    Task<IReadOnlyList<int>> CaseIdsOfAsync(int employeeId, CancellationToken cancellationToken);

    /// <summary>The pseudonym of the employee (a new array), for a new anonymous draft or a draft that becomes anonymous.</summary>
    byte[] PseudonymOf(int employeeId);

    /// <summary>The submitter row of a case, tracked (a change is saved by the unit of work), or null.</summary>
    Task<GrievanceCaseSubmitter?> FindForChangeAsync(int caseId, CancellationToken cancellationToken);

    /// <summary>Stages a new submitter row (saved by the unit of work). The case must already have its id.</summary>
    void Add(GrievanceCaseSubmitter submitter);
}
