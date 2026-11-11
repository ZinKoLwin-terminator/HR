namespace Apex.Hrms.Application.Abstractions;

/// <summary>
/// The numeric part of the next job opening code, <c>JOB-yyyy-nnn</c> (database sequence rec.JobOpeningCodeSeq). Each
/// call consumes a number, so gaps are normal; the number does not restart each year. The application skips codes that
/// are already in use.
/// </summary>
public interface IJobOpeningCodeSequence
{
    Task<int> NextAsync(CancellationToken cancellationToken);
}
