using Apex.Hrms.Application.Attendance.Adjustments;
using Apex.Hrms.Application.Attendance.Approvals;
using Apex.Hrms.Application.Attendance.Holidays;
using Apex.Hrms.Application.Attendance.Overtime;
using Apex.Hrms.Application.Attendance.Records;
using Apex.Hrms.Application.Attendance.Shifts;
using Apex.Hrms.Application.Attendance.Views;
using Apex.Hrms.Application.References;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Attendance;
using Apex.Hrms.Domain.Employees;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Apex.Hrms.Application.Attendance;

internal static class AttendanceServiceCollectionExtensions
{
    public static IServiceCollection AddAttendance(this IServiceCollection services)
    {
        services.AddScoped<AttendanceLookup>();
        services.AddScoped<IAttendanceOverviewHandler, AttendanceOverviewHandler>();
        services.AddScoped<IWorkScheduleHandler, WorkScheduleHandler>();

        return services
            .AddShifts()
            .AddHolidays()
            .AddAttendanceRecords()
            .AddAdjustments()
            .AddOvertime()
            .AddAttendanceReferences();
    }

    private static IServiceCollection AddShifts(this IServiceCollection services) => services
        .AddCrudResource<Shift, ShiftDetails, ShiftCreateRequest, ShiftUpdateRequest, ShiftDto, ShiftFilter, ShiftMapping, ShiftSaveRules, ShiftQuerySpecification>();

    /// <summary>Holidays have no status, so they are registered piece by piece: reads, create, update and delete.</summary>
    private static IServiceCollection AddHolidays(this IServiceCollection services) => services
        .AddResource<Holiday, HolidayDetails, HolidayDto, HolidayFilter, HolidayMapping, HolidayQuerySpecification>()
        .AddCreate<Holiday, HolidayDetails, HolidayCreateRequest, HolidayDto, HolidaySaveRules>()
        .AddUpdate<Holiday, HolidayDetails, HolidayUpdateRequest, HolidayDto, HolidaySaveRules>()
        .AddDelete<Holiday, HolidayDto>();

    /// <summary>Records are read, and written only by punches (and by approved adjustments).</summary>
    private static IServiceCollection AddAttendanceRecords(this IServiceCollection services)
    {
        services.AddScoped<AttendanceRecordMapping>();
        services.AddScoped<IEntityDescriptor<AttendanceRecord>>(provider => provider.GetRequiredService<AttendanceRecordMapping>());
        services.AddScoped<IDtoMapper<AttendanceRecord, AttendanceRecordDto>>(provider => provider.GetRequiredService<AttendanceRecordMapping>());
        services.TryAddScoped<EntityLoader<AttendanceRecord>>();
        services.AddScoped<AttendanceListItemBuilder>();
        services.AddScoped<IQueryHandler<AttendanceListItemDto, AttendanceFilter>, AttendanceRecordQueryHandler>();
        services.AddScoped<IGetByIdHandler<AttendanceListItemDto>, AttendanceRecordGetHandler>();
        services.AddScoped<IPunchHandler, PunchHandler>();
        return services;
    }

    private static IServiceCollection AddAdjustments(this IServiceCollection services)
    {
        services.AddScoped<AdjustmentMapping>();
        services.AddScoped<IEntityDescriptor<AttendanceAdjustment>>(provider => provider.GetRequiredService<AdjustmentMapping>());
        services.AddScoped<IDtoMapper<AttendanceAdjustment, AdjustmentDto>>(provider => provider.GetRequiredService<AdjustmentMapping>());
        services.AddScoped<IQuerySpecification<AttendanceAdjustment, AdjustmentFilter>, AdjustmentQuerySpecification>();
        services.TryAddScoped<EntityLoader<AttendanceAdjustment>>();

        services.AddScoped<IGetByIdHandler<AdjustmentDto>, GetByIdHandler<AttendanceAdjustment, AdjustmentDto>>();
        services.AddScoped<IQueryHandler<AdjustmentDto, AdjustmentFilter>, QueryHandler<AttendanceAdjustment, AdjustmentDto, AdjustmentFilter>>();
        services.AddScoped<ICreateHandler<AdjustmentCreateRequest, AdjustmentDto>, AdjustmentCreateHandler>();
        services.AddScoped<IDeleteHandler<AdjustmentDto>, ApprovalDeleteHandler<AttendanceAdjustment, AdjustmentDto>>();
        services.AddScoped<ICommandHandler<ApproveAdjustmentCommand, AdjustmentDto>, ApproveAdjustmentHandler>();
        return services.AddDecisionCommand<AttendanceAdjustment, RejectAdjustmentCommand, AdjustmentDto>();
    }

    private static IServiceCollection AddOvertime(this IServiceCollection services)
    {
        services.AddScoped<OvertimeMapping>();
        services.AddScoped<IEntityDescriptor<OvertimeRecord>>(provider => provider.GetRequiredService<OvertimeMapping>());
        services.AddScoped<IDtoMapper<OvertimeRecord, OvertimeDto>>(provider => provider.GetRequiredService<OvertimeMapping>());
        services.AddScoped<IQuerySpecification<OvertimeRecord, OvertimeFilter>, OvertimeQuerySpecification>();
        services.TryAddScoped<EntityLoader<OvertimeRecord>>();

        services.AddScoped<IGetByIdHandler<OvertimeDto>, GetByIdHandler<OvertimeRecord, OvertimeDto>>();
        services.AddScoped<IQueryHandler<OvertimeDto, OvertimeFilter>, QueryHandler<OvertimeRecord, OvertimeDto, OvertimeFilter>>();
        services.AddScoped<ICreateHandler<OvertimeCreateRequest, OvertimeDto>, OvertimeCreateHandler>();
        services.AddScoped<IDeleteHandler<OvertimeDto>, ApprovalDeleteHandler<OvertimeRecord, OvertimeDto>>();
        return services
            .AddDecisionCommand<OvertimeRecord, ApproveOvertimeCommand, OvertimeDto>()
            .AddDecisionCommand<OvertimeRecord, RejectOvertimeCommand, OvertimeDto>();
    }

    /// <summary>An approve or reject command: the shared decision action and its activity wording.</summary>
    private static IServiceCollection AddDecisionCommand<TEntity, TCommand, TDto>(this IServiceCollection services)
        where TEntity : ApprovalRequest
        where TCommand : IDecisionCommand
    {
        services.AddScoped<ICommandActivity<TEntity, TCommand>, DecisionActivity<TEntity, TCommand>>();
        return services.AddEntityCommand<TEntity, TCommand, TDto, DecisionAction<TEntity, TCommand>>();
    }

    /// <summary>
    /// What attendance records block: deleting a shift (employees assigned to it, attendance records that used it) and
    /// deleting an employee (their records, adjustment requests and overtime entries).
    /// </summary>
    private static IServiceCollection AddAttendanceReferences(this IServiceCollection services) => services
        .AddReference<Shift, Employee>("assigned employee(s)", ReferenceUse.Delete, ownerId => employee => employee.ShiftId == ownerId)
        .AddReference<Shift, AttendanceRecord>("attendance record(s)", ReferenceUse.Delete, ownerId => record => record.ShiftId == ownerId)
        .AddReference<Employee, AttendanceRecord>("attendance record(s)", ReferenceUse.Delete, ownerId => record => record.EmployeeId == ownerId)
        .AddReference<Employee, AttendanceAdjustment>("attendance adjustment(s)", ReferenceUse.Delete, ownerId => adjustment => adjustment.EmployeeId == ownerId)
        .AddReference<Employee, OvertimeRecord>("overtime entry(ies)", ReferenceUse.Delete, ownerId => overtime => overtime.EmployeeId == ownerId);
}
