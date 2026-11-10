namespace Apex.Hrms.Application.Abstractions;

/// <summary>
/// The numeric part of the next leave application number, <c>LV-yyyy-nnnn</c> (database sequence
/// lve.LeaveApplicationNoSeq). Each call consumes a number, so gaps are normal; the number does not restart each year.
/// The application skips numbers that are already in use.
/// </summary>
public interface ILeaveApplicationNumberSequence
{
    Task<int> NextAsync(CancellationToken cancellationToken);
}
