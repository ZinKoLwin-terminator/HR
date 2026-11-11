using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Attendance;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Attendance.Shifts;

/// <summary>Mirrors the Shift interface in attendance.models.ts. Times are "HH:mm"; work days are 0 = Sunday ... 6 = Saturday.</summary>
public sealed record ShiftDto(
    int Id,
    string Code,
    string Name,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int BreakMinutes,
    int GraceMinutes,
    IReadOnlyList<int> WorkDays,
    bool IsNightShift,
    string Color,
    string Status,
    byte[] RowVersion) : IIdentified;

internal interface IShiftFields
{
    string Code { get; }

    string Name { get; }

    TimeOnly StartTime { get; }

    TimeOnly EndTime { get; }

    int BreakMinutes { get; }

    int GraceMinutes { get; }

    IReadOnlyList<int>? WorkDays { get; }

    bool IsNightShift { get; }

    string Color { get; }
}

/// <summary>POST body. Status is the status the new shift starts with.</summary>
public sealed record ShiftCreateRequest(
    string Code,
    string Name,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int BreakMinutes,
    int GraceMinutes,
    IReadOnlyList<int>? WorkDays,
    bool IsNightShift,
    string Color,
    string Status) : IShiftFields, ICreateRequest<ShiftDetails>
{
    public ShiftDetails ToDetails() => ShiftDetailsFactory.From(this);

    public RecordStatus ParseStatus() => Enum.Parse<RecordStatus>(Status);
}

/// <summary>PUT body. It has no status (use activate and deactivate) and requires the row version.</summary>
public sealed record ShiftUpdateRequest(
    string Code,
    string Name,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int BreakMinutes,
    int GraceMinutes,
    IReadOnlyList<int>? WorkDays,
    bool IsNightShift,
    string Color,
    string? RowVersion) : IShiftFields, IDetailsRequest<ShiftDetails>, IVersionedRequest
{
    public ShiftDetails ToDetails() => ShiftDetailsFactory.From(this);
}

internal static class ShiftDetailsFactory
{
    public static ShiftDetails From(IShiftFields fields) => new ShiftDetails(
        fields.Code,
        fields.Name,
        fields.StartTime,
        fields.EndTime,
        fields.BreakMinutes,
        fields.GraceMinutes,
        fields.WorkDays ?? [],
        fields.IsNightShift,
        fields.Color).Normalized();
}

public sealed record ShiftFilter(string? Status = null);
