namespace Apex.Hrms.Application.Abstractions;

/// <summary>
/// The numeric part of the next grievance case number, <c>GRV-yyyy-nnnnn</c> (database sequence grv.CaseNoSeq). Each call consumes a
/// number, so gaps are normal; the number does not restart each year. The application skips numbers that are already in use.
/// </summary>
public interface IGrievanceCaseNumberSequence
{
    Task<int> NextAsync(CancellationToken cancellationToken);
}
