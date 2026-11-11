using System.Linq.Expressions;
using Apex.Hrms.Application.Organization;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Attendance;
using FluentValidation;

namespace Apex.Hrms.Application.Attendance.Adjustments;

/// <summary>
/// Mirrors AttendanceAdjustment in attendance.models.ts, without <c>recordId</c> (the record is the one of
/// <c>employeeId</c> and <c>date</c>). <c>DecidedBy</c> is the name of whoever decided, as it was then.
/// </summary>
public sealed record AdjustmentDto(
    int Id,
    int EmployeeId,
    DateOnly Date,
    TimeOnly? CurrentCheckIn,
    TimeOnly? CurrentCheckOut,
    TimeOnly? RequestedCheckIn,
    TimeOnly? RequestedCheckOut,
    string? RequestedStatus,
    string Reason,
    string Status,
    DateOnly RequestedOn,
    string? DecidedBy,
    DateOnly? DecidedOn,
    string Remarks,
    byte[] RowVersion) : IIdentified;

/// <summary>
/// POST body. The request starts Pending. The employee must already have an attendance record on <c>date</c> (records
/// come from punches). At least one of the times or the status is required; <c>requestedStatus</c> is one of the
/// attendance statuses ("Present", "Late", "Half Day" ...).
/// </summary>
public sealed record AdjustmentCreateRequest(
    int EmployeeId,
    DateOnly Date,
    TimeOnly? RequestedCheckIn,
    TimeOnly? RequestedCheckOut,
    string? RequestedStatus,
    string Reason)
{
    public AttendanceAdjustmentDetails ToDetails() => new AttendanceAdjustmentDetails(
        EmployeeId,
        Date,
        RequestedCheckIn,
        RequestedCheckOut,
        string.IsNullOrWhiteSpace(RequestedStatus) ? null : AttendanceStatusText.Parse(RequestedStatus.Trim()),
        Reason).Normalized();
}

public sealed record AdjustmentFilter(int? EmployeeId = null, string? Status = null, DateOnly? DateFrom = null, DateOnly? DateTo = null);

internal sealed class AdjustmentMapping :
    IEntityDescriptor<AttendanceAdjustment>,
    IDtoMapper<AttendanceAdjustment, AdjustmentDto>
{
    public string Label => "Adjustment request";

    public string Describe(AttendanceAdjustment entity) => entity.Label;

    public AdjustmentDto ToDto(AttendanceAdjustment entity) => new(
        entity.Id,
        entity.EmployeeId,
        entity.Date,
        entity.CurrentCheckIn,
        entity.CurrentCheckOut,
        entity.RequestedCheckIn,
        entity.RequestedCheckOut,
        entity.RequestedStatus is { } status ? AttendanceStatusText.ToText(status) : null,
        entity.Reason,
        ApprovalStatusText.ToText(entity.Status),
        entity.RequestedOn,
        entity.DecidedByName,
        entity.DecidedOn,
        entity.Remarks ?? string.Empty,
        entity.RowVersion);
}

internal sealed class AdjustmentQuerySpecification : IQuerySpecification<AttendanceAdjustment, AdjustmentFilter>
{
    public IReadOnlyDictionary<string, Expression<Func<AttendanceAdjustment, object>>> SortKeys { get; } =
        new Dictionary<string, Expression<Func<AttendanceAdjustment, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = a => a.Id,
            ["employeeId"] = a => a.EmployeeId,
            ["date"] = a => a.Date,
            ["status"] = a => a.Status,
            ["requestedOn"] = a => a.RequestedOn,
            ["decidedOn"] = a => a.DecidedOn ?? DateOnly.MinValue,
        };

    public Expression<Func<AttendanceAdjustment, bool>> Search(string term) =>
        Predicates.ContainsAny<AttendanceAdjustment>(term, a => a.Reason);

    public Expression<Func<AttendanceAdjustment, bool>> Filter(AdjustmentFilter filter) => Predicates.Always<AttendanceAdjustment>()
        .And(filter.EmployeeId is { } employeeId ? a => a.EmployeeId == employeeId : Predicates.Always<AttendanceAdjustment>())
        .And(filter.DateFrom is { } from ? a => a.Date >= from : Predicates.Always<AttendanceAdjustment>())
        .And(filter.DateTo is { } to ? a => a.Date <= to : Predicates.Always<AttendanceAdjustment>())
        .And(StatusFilter(filter.Status));

    private static Expression<Func<AttendanceAdjustment, bool>> StatusFilter(string? text)
    {
        if (!FilterPredicates.IsSet(text))
        {
            return Predicates.Always<AttendanceAdjustment>();
        }

        return ApprovalStatusText.TryParse(text, out var status)
            ? a => a.Status == status
            : Predicates.Never<AttendanceAdjustment>();
    }
}

internal sealed class AdjustmentCreateRequestValidator : RequestValidator<AdjustmentCreateRequest>
{
    public AdjustmentCreateRequestValidator()
    {
        RuleForText(x => x.Reason, "Reason").RequiredText(AttendanceAdjustment.ReasonMaxLength);
        RuleForText(x => x.RequestedStatus, "Requested status")
            .Must(status => string.IsNullOrWhiteSpace(status) || AttendanceStatusText.TryParse(status.Trim(), out _))
            .WithMessage($"Requested status must be {AttendanceStatusText.Map.OneOf}.");
    }
}
