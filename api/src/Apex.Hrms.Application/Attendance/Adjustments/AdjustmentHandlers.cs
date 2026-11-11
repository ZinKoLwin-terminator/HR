using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Activity;
using Apex.Hrms.Application.Attendance.Approvals;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Attendance;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using FluentValidation;

namespace Apex.Hrms.Application.Attendance.Adjustments;

/// <summary>
/// Files an adjustment request. The employee must exist and already have an attendance record on the date (the table
/// links the two); the request may not be for a future date, must ask for something, must keep the check-out after the
/// check-in (unless the shift is a night shift), and must be the only pending request for that employee and date.
/// </summary>
internal sealed class AdjustmentCreateHandler(
    IValidator<AdjustmentCreateRequest> validator,
    IRepository<Employee> employees,
    IRepository<AttendanceAdjustment> adjustments,
    AttendanceLookup lookup,
    IClock clock,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork,
    AdjustmentMapping mapper) : ICreateHandler<AdjustmentCreateRequest, AdjustmentDto>
{
    public async Task<Result<AdjustmentDto>> HandleAsync(AdjustmentCreateRequest request, CancellationToken cancellationToken)
    {
        var valid = await validator.CheckAsync(request, cancellationToken);
        if (valid.IsFailure)
        {
            return Result.Failure<AdjustmentDto>(valid.Error);
        }

        var details = request.ToDetails();
        if (await employees.FindAsync(details.EmployeeId, cancellationToken) is null)
        {
            return Result.Failure<AdjustmentDto>(AppError.Validation(
                "The selected employee does not exist.", "employeeId", "Choose an employee."));
        }

        // A future day has no record, so say what is wrong with the date before saying there is no record.
        if (details.Date > clock.Today)
        {
            return Result.Failure<AdjustmentDto>(AppError.Validation(
                "The date cannot be in the future.", "date", "Choose today or an earlier date."));
        }

        var record = await lookup.RecordOfAsync(details.EmployeeId, details.Date, cancellationToken);
        if (record is null)
        {
            return Result.Failure<AdjustmentDto>(AppError.Validation(
                "There is no attendance record for this employee on that date.",
                "date",
                "No attendance record exists for this date."));
        }

        var shift = await lookup.ShiftForAsync(record.ShiftId, cancellationToken);
        var created = AttendanceAdjustment.Create(details, record, shift?.IsNightShift ?? false, clock.Today);
        if (created.IsFailure)
        {
            return Result.Failure<AdjustmentDto>(created.Error);
        }

        var date = details.Date;
        var employeeId = details.EmployeeId;
        if (await adjustments.AnyAsync(
            a => a.EmployeeId == employeeId && a.Date == date && a.Status == ApprovalStatus.Pending,
            cancellationToken))
        {
            return Result.Failure<AdjustmentDto>(AppError.Validation(
                "A pending request already exists for this employee and date.",
                "date",
                "A pending request already exists for this date."));
        }

        adjustments.Add(created.Value);
        // The label holds the id the database assigns, so the text is worked out once the row is inserted.
        var adjustment = created.Value;
        activity.Record(mapper.ActivityCategory, () => ActivityMessages.Created(mapper.Label, mapper.Describe(adjustment)));
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure
            ? Result.Failure<AdjustmentDto>(saved.Error)
            : Result.Success(mapper.ToDto(created.Value));
    }
}

/// <summary>
/// Approves an adjustment and applies it to the attendance record in the same save: the requested times replace the
/// stored ones, then minutes and status are derived again. The remarks of the record say which request changed it.
/// </summary>
internal sealed class ApproveAdjustmentHandler(
    EntityLoader<AttendanceAdjustment> loader,
    AttendanceLookup lookup,
    ICurrentUser user,
    IClock clock,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork,
    AdjustmentMapping mapper) : ICommandHandler<ApproveAdjustmentCommand, AdjustmentDto>
{
    public async Task<Result<AdjustmentDto>> HandleAsync(ApproveAdjustmentCommand command, CancellationToken cancellationToken)
    {
        var loaded = await loader.LoadForChangeAsync(command.Id, command.RowVersion, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<AdjustmentDto>(loaded.Error);
        }

        var adjustment = loaded.Value;
        var actor = AttendanceActors.ActorOf(user);
        if (actor.IsFailure)
        {
            return Result.Failure<AdjustmentDto>(actor.Error);
        }

        var record = await lookup.RecordOfAsync(adjustment.EmployeeId, adjustment.Date, cancellationToken);
        if (record is null)
        {
            return Result.Failure<AdjustmentDto>(AppError.Conflict("The attendance record of this request no longer exists."));
        }

        var shift = await lookup.ShiftForAsync(record.ShiftId, cancellationToken);
        if (shift is null)
        {
            return Result.Failure<AdjustmentDto>(AppError.Conflict("No shift exists to work out the attendance with."));
        }

        // The record may have changed since the request was made (a later punch): check the result still makes sense.
        var checkIn = adjustment.RequestedCheckIn ?? record.CheckIn;
        var checkOut = adjustment.RequestedCheckOut ?? record.CheckOut;
        if (checkIn is { } start && checkOut is { } end && !shift.IsNightShift && end <= start)
        {
            return Result.Failure<AdjustmentDto>(AppError.Conflict(
                "The requested times no longer fit the record: the check-out must be after the check-in."));
        }

        var decided = adjustment.Decide(ApprovalDecision.Approve, actor.Value, command.Remarks, clock.Today);
        if (decided.IsFailure)
        {
            return Result.Failure<AdjustmentDto>(decided.Error);
        }

        var applied = record.ApplyAdjustment(
            adjustment.RequestedCheckIn,
            adjustment.RequestedCheckOut,
            adjustment.RequestedStatus,
            shift.Rules,
            adjustment.Label);
        if (applied.IsFailure)
        {
            return Result.Failure<AdjustmentDto>(applied.Error);
        }

        activity.Record(
            mapper.ActivityCategory,
            ActivityMessages.AdjustmentDecided(adjustment.Label, "Approved", adjustment.Date));

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure
            ? Result.Failure<AdjustmentDto>(saved.Error)
            : Result.Success(mapper.ToDto(adjustment));
    }
}
