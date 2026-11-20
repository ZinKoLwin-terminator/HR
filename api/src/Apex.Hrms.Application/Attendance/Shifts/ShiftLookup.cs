using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Attendance;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Attendance.Shifts;

/// <summary>
/// A shift as a name source (the shift of an employee's profile): times as "HH:mm" and work days 0 = Sunday ... 6 = Saturday,
/// the same formats as <see cref="ShiftDto"/>. No break or grace minutes, colour or row version.
/// </summary>
public sealed record ShiftLookupDto(
    int Id,
    string Code,
    string Name,
    TimeOnly StartTime,
    TimeOnly EndTime,
    IReadOnlyList<int> WorkDays,
    bool IsNightShift,
    string Status);

public interface IShiftLookupHandler
{
    /// <summary>Every shift, active and inactive, by name then id, reduced to <see cref="ShiftLookupDto"/>.</summary>
    Task<Result<IReadOnlyList<ShiftLookupDto>>> HandleAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Deliberately unscoped: the host lets any signed-in user call it, so it returns only non-personal reference data. One
/// untracked projection (the work days come with it, not one query per shift).
/// </summary>
internal sealed class ShiftLookupHandler(IReadModel read) : IShiftLookupHandler
{
    public async Task<Result<IReadOnlyList<ShiftLookupDto>>> HandleAsync(CancellationToken cancellationToken)
    {
        var rows = await read.ToListAsync(
            read.Query<Shift>()
                .OrderBy(shift => shift.Name).ThenBy(shift => shift.Id)
                .Select(shift => new
                {
                    shift.Id,
                    shift.Code,
                    shift.Name,
                    shift.StartTime,
                    shift.EndTime,
                    WorkDays = shift.WorkDays.Select(day => day.DayOfWeek).ToList(),
                    shift.IsNightShift,
                    shift.Status,
                }),
            cancellationToken);

        // Work days in ascending order and the status as its enum name, as ShiftMapping writes them; converted after the query.
        IReadOnlyList<ShiftLookupDto> shifts =
        [
            .. rows.Select(row => new ShiftLookupDto(
                row.Id,
                row.Code,
                row.Name,
                row.StartTime,
                row.EndTime,
                [.. row.WorkDays.Select(day => (int)day).Order()],
                row.IsNightShift,
                row.Status.ToString())),
        ];
        return Result.Success(shifts);
    }
}
