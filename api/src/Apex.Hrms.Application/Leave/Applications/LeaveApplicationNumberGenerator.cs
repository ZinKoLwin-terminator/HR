using System.Globalization;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Leave;

namespace Apex.Hrms.Application.Leave.Applications;

/// <summary>
/// Makes application numbers such as <c>LV-2026-0007</c>: the year of the application date and the next number of the
/// database sequence, skipping numbers that are already in use (the sequence can lag behind imported data).
/// </summary>
internal sealed class LeaveApplicationNumberGenerator(
    ILeaveApplicationNumberSequence sequence,
    IRepository<LeaveApplication> applications)
{
    private const int MaxAttempts = 1000;

    public async Task<Result<string>> NextFreeAsync(DateOnly today, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var number = await sequence.NextAsync(cancellationToken);
            var applicationNo = Format(today.Year, number);
            if (!await applications.AnyAsync(application => application.ApplicationNo == applicationNo, cancellationToken))
            {
                return Result.Success(applicationNo);
            }
        }

        return Result.Failure<string>(AppError.Conflict("No unused application number could be generated. Try again."));
    }

    public static string Format(int year, int number) =>
        string.Create(CultureInfo.InvariantCulture, $"LV-{year:D4}-{number:D4}");
}
