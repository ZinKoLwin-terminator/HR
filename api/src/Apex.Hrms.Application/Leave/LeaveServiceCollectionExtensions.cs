using Apex.Hrms.Application.Leave.Applications;
using Apex.Hrms.Application.Leave.Balances;
using Apex.Hrms.Application.Leave.Calendar;
using Apex.Hrms.Application.Leave.LeavePolicies;
using Apex.Hrms.Application.Leave.LeaveTypes;
using Apex.Hrms.Application.References;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Leave;
using Apex.Hrms.Domain.Organization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Apex.Hrms.Application.Leave;

internal static class LeaveServiceCollectionExtensions
{
    public static IServiceCollection AddLeave(this IServiceCollection services)
    {
        services.AddScoped<WorkingDayCalculator>();
        services.AddScoped<LeaveBalanceService>();
        services.AddScoped<LeaveApplicationRules>();
        services.AddScoped<LeaveApplicationNumberGenerator>();
        services.AddScoped<ILeaveBalanceHandler, LeaveBalanceHandler>();
        services.AddScoped<ILeaveCalendarHandler, LeaveCalendarHandler>();

        return services
            .AddLeaveTypes()
            .AddLeavePolicies()
            .AddLeaveApplications()
            .AddLeaveReferences();
    }

    private static IServiceCollection AddLeaveTypes(this IServiceCollection services) => services
        .AddCrudResource<LeaveType, LeaveTypeDetails, LeaveTypeCreateRequest, LeaveTypeUpdateRequest, LeaveTypeDto, LeaveTypeFilter, LeaveTypeMapping, LeaveTypeRules, LeaveTypeQuerySpecification>();

    /// <summary>
    /// Policies have no code, so they are registered piece by piece: the shared reads, update and delete, an own create
    /// and own activate (both check the "one active policy per group" rule) and the generic-style deactivate.
    /// </summary>
    private static IServiceCollection AddLeavePolicies(this IServiceCollection services)
    {
        services.AddResource<LeavePolicy, LeavePolicyDetails, LeavePolicyDto, LeavePolicyFilter, LeavePolicyMapping, LeavePolicyQuerySpecification>();
        services.AddScoped<LeavePolicyRules>();
        services.AddScoped<ISaveRules<LeavePolicy, LeavePolicyDetails>>(provider => provider.GetRequiredService<LeavePolicyRules>());
        services.AddScoped<ICreateHandler<LeavePolicyCreateRequest, LeavePolicyDto>, LeavePolicyCreateHandler>();
        services.AddUpdate<LeavePolicy, LeavePolicyDetails, LeavePolicyUpdateRequest, LeavePolicyDto, LeavePolicyRules>();
        services.AddDelete<LeavePolicy, LeavePolicyDto>();
        services.AddScoped<ICommandHandler<ActivateCommand, LeavePolicyDto>, LeavePolicyActivateHandler>();
        services.AddEntityCommand<LeavePolicy, DeactivateCommand, LeavePolicyDto, DeactivateLeavePolicyAction>();
        return services;
    }

    /// <summary>
    /// Applications are registered piece by piece: only the reads are shared; create, update and delete have their own
    /// flows and each workflow move is a command of its own.
    /// </summary>
    private static IServiceCollection AddLeaveApplications(this IServiceCollection services)
    {
        services.AddScoped<LeaveApplicationMapping>();
        services.AddScoped<IEntityDescriptor<LeaveApplication>>(provider => provider.GetRequiredService<LeaveApplicationMapping>());
        services.AddScoped<IDtoMapper<LeaveApplication, LeaveApplicationDto>>(provider => provider.GetRequiredService<LeaveApplicationMapping>());
        services.AddScoped<IQuerySpecification<LeaveApplication, LeaveApplicationFilter>, LeaveApplicationQuerySpecification>();
        services.TryAddScoped<EntityLoader<LeaveApplication>>();

        services.AddScoped<IGetByIdHandler<LeaveApplicationDto>, GetByIdHandler<LeaveApplication, LeaveApplicationDto>>();
        services.AddScoped<IQueryHandler<LeaveApplicationDto, LeaveApplicationFilter>, QueryHandler<LeaveApplication, LeaveApplicationDto, LeaveApplicationFilter>>();

        services.AddScoped<ICreateHandler<LeaveApplicationCreateRequest, LeaveApplicationDto>, LeaveApplicationCreateHandler>();
        services.AddScoped<IUpdateHandler<LeaveApplicationUpdateRequest, LeaveApplicationDto>, LeaveApplicationUpdateHandler>();
        services.AddScoped<IDeleteHandler<LeaveApplicationDto>, LeaveApplicationDeleteHandler>();

        return services
            .AddLeaveCommand<SubmitLeaveCommand, SubmitLeaveAction>()
            .AddLeaveCommand<ApproveLeaveCommand, ApproveLeaveAction>()
            .AddLeaveCommand<RejectLeaveCommand, RejectLeaveAction>()
            .AddLeaveCommand<CancelLeaveCommand, CancelLeaveAction>();
    }

    private static IServiceCollection AddLeaveCommand<TCommand, TAction>(this IServiceCollection services)
        where TCommand : IEntityCommand
        where TAction : class, ILeaveCommandAction<TCommand>
    {
        services.AddScoped<ILeaveCommandAction<TCommand>, TAction>();
        services.AddScoped<ICommandHandler<TCommand, LeaveApplicationDto>, LeaveCommandHandler<TCommand>>();
        return services;
    }

    /// <summary>What leave records block: deleting a leave type, an employee or a job grade that they point at.</summary>
    private static IServiceCollection AddLeaveReferences(this IServiceCollection services) => services
        .AddReference<LeaveType, LeaveApplication>("application(s)", ReferenceUse.Delete, ownerId => application => application.LeaveTypeId == ownerId)
        .AddReference<LeaveType, LeavePolicy>("policy(ies)", ReferenceUse.Delete, ownerId => policy => policy.LeaveTypeId == ownerId)
        .AddReference<JobGrade, LeavePolicy>("leave policy(ies)", ReferenceUse.Delete, ownerId => policy => policy.JobGradeId == ownerId)
        .AddReference<Employee, LeaveApplication>(
            "leave application(s)",
            ReferenceUse.Delete,
            ownerId => application => application.EmployeeId == ownerId || application.ApproverEmployeeId == ownerId);
}
