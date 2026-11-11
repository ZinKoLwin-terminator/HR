using System.Linq.Expressions;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Organization;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Attendance;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using FluentValidation;

namespace Apex.Hrms.Application.Attendance.Overtime;

/// <summary>Mirrors OvertimeRecord in attendance.models.ts. <c>Minutes</c> is worked out by the server from the two times.</summary>
public sealed record OvertimeDto(
    int Id,
    int EmployeeId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int Minutes,
    string Reason,
    string Status,
    DateOnly RequestedOn,
    string? DecidedBy,
    DateOnly? DecidedOn,
    string Remarks,
    byte[] RowVersion) : IIdentified;

/// <summary>POST body. The request starts Pending; the duration (start to end, 15 minutes to 12 hours) is derived.</summary>
public sealed record OvertimeCreateRequest(int EmployeeId, DateOnly Date, TimeOnly StartTime, TimeOnly EndTime, string Reason)
{
    public OvertimeDetails ToDetails() => new OvertimeDetails(EmployeeId, Date, StartTime, EndTime, Reason).Normalized();
}

public sealed record OvertimeFilter(int? EmployeeId = null, string? Status = null, DateOnly? DateFrom = null, DateOnly? DateTo = null);

internal sealed class OvertimeMapping :
    IEntityDescriptor<OvertimeRecord>,
    IDtoMapper<OvertimeRecord, OvertimeDto>
{
    public string Label => "Overtime entry";

    public string Describe(OvertimeRecord entity) => entity.Label;

    public OvertimeDto ToDto(OvertimeRecord entity) => new(
        entity.Id,
        entity.EmployeeId,
        entity.Date,
        entity.StartTime,
        entity.EndTime,
        entity.Minutes,
        entity.Reason,
        ApprovalStatusText.ToText(entity.Status),
        entity.RequestedOn,
        entity.DecidedByName,
        entity.DecidedOn,
        entity.Remarks ?? string.Empty,
        entity.RowVersion);
}

internal sealed class OvertimeQuerySpecification : IQuerySpecification<OvertimeRecord, OvertimeFilter>
{
    public IReadOnlyDictionary<string, Expression<Func<OvertimeRecord, object>>> SortKeys { get; } =
        new Dictionary<string, Expression<Func<OvertimeRecord, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = o => o.Id,
            ["employeeId"] = o => o.EmployeeId,
            ["date"] = o => o.Date,
            ["minutes"] = o => o.Minutes,
            ["status"] = o => o.Status,
            ["requestedOn"] = o => o.RequestedOn,
        };

    public Expression<Func<OvertimeRecord, bool>> Search(string term) =>
        Predicates.ContainsAny<OvertimeRecord>(term, o => o.Reason);

    public Expression<Func<OvertimeRecord, bool>> Filter(OvertimeFilter filter) => Predicates.Always<OvertimeRecord>()
        .And(filter.EmployeeId is { } employeeId ? o => o.EmployeeId == employeeId : Predicates.Always<OvertimeRecord>())
        .And(filter.DateFrom is { } from ? o => o.Date >= from : Predicates.Always<OvertimeRecord>())
        .And(filter.DateTo is { } to ? o => o.Date <= to : Predicates.Always<OvertimeRecord>())
        .And(StatusFilter(filter.Status));

    private static Expression<Func<OvertimeRecord, bool>> StatusFilter(string? text)
    {
        if (!FilterPredicates.IsSet(text))
        {
            return Predicates.Always<OvertimeRecord>();
        }

        return ApprovalStatusText.TryParse(text, out var status)
            ? o => o.Status == status
            : Predicates.Never<OvertimeRecord>();
    }
}

internal sealed class OvertimeCreateRequestValidator : RequestValidator<OvertimeCreateRequest>
{
    public OvertimeCreateRequestValidator() =>
        RuleForText(x => x.Reason, "Reason").RequiredText(OvertimeRecord.ReasonMaxLength);
}

/// <summary>
/// Files an overtime request: the employee exists, the date is not in the future, the duration is 15 minutes to 12
/// hours, and the employee has no overtime entry on that date yet.
/// </summary>
internal sealed class OvertimeCreateHandler(
    IValidator<OvertimeCreateRequest> validator,
    IRepository<Employee> employees,
    IRepository<OvertimeRecord> overtime,
    IClock clock,
    IUnitOfWork unitOfWork,
    OvertimeMapping mapper) : ICreateHandler<OvertimeCreateRequest, OvertimeDto>
{
    public async Task<Result<OvertimeDto>> HandleAsync(OvertimeCreateRequest request, CancellationToken cancellationToken)
    {
        var valid = await validator.CheckAsync(request, cancellationToken);
        if (valid.IsFailure)
        {
            return Result.Failure<OvertimeDto>(valid.Error);
        }

        var details = request.ToDetails();
        if (await employees.FindAsync(details.EmployeeId, cancellationToken) is null)
        {
            return Result.Failure<OvertimeDto>(AppError.Validation(
                "The selected employee does not exist.", "employeeId", "Choose an employee."));
        }

        var created = OvertimeRecord.Create(details, clock.Today);
        if (created.IsFailure)
        {
            return Result.Failure<OvertimeDto>(created.Error);
        }

        var employeeId = details.EmployeeId;
        var date = details.Date;
        if (await overtime.AnyAsync(o => o.EmployeeId == employeeId && o.Date == date, cancellationToken))
        {
            return Result.Failure<OvertimeDto>(AppError.Validation(
                "Overtime is already recorded for this employee on that date.",
                "date",
                "Already recorded for this date."));
        }

        overtime.Add(created.Value);
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure
            ? Result.Failure<OvertimeDto>(saved.Error)
            : Result.Success(mapper.ToDto(created.Value));
    }
}
