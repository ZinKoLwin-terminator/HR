namespace Apex.Hrms.Application.Abstractions;

/// <summary>
/// The numeric part of the next suggested employee code (database sequence hr.EmployeeCodeSeq). Each call consumes a
/// number, so gaps are normal. The application skips codes that are already in use.
/// </summary>
public interface IEmployeeCodeSequence
{
    Task<int> NextAsync(CancellationToken cancellationToken);
}
