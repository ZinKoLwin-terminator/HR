using Apex.Hrms.Application.Payroll.Assignments;
using Apex.Hrms.Application.Payroll.Components;
using Apex.Hrms.Application.Payroll.Payslips;
using Apex.Hrms.Application.Payroll.Runs;
using Apex.Hrms.Application.Payroll.Structures;
using Apex.Hrms.Application.References;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Payroll;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Apex.Hrms.Application.Payroll;

internal static class PayrollServiceCollectionExtensions
{
    public static IServiceCollection AddPayroll(this IServiceCollection services)
    {
        services.AddScoped<PayrollProcessor>();

        return services
            .AddSalaryComponents()
            .AddSalaryStructures()
            .AddSalaryAssignments()
            .AddPayrollRuns()
            .AddPayslips()
            .AddPayrollReferences();
    }

    private static IServiceCollection AddSalaryComponents(this IServiceCollection services) => services
        .AddCrudResource<SalaryComponent, SalaryComponentDetails, SalaryComponentCreateRequest, SalaryComponentUpdateRequest, SalaryComponentDto, SalaryComponentFilter, SalaryComponentMapping, SalaryComponentRules, SalaryComponentQuerySpecification>();

    private static IServiceCollection AddSalaryStructures(this IServiceCollection services) => services
        .AddCrudResource<SalaryStructure, SalaryStructureDetails, SalaryStructureCreateRequest, SalaryStructureUpdateRequest, SalaryStructureDto, SalaryStructureFilter, SalaryStructureMapping, SalaryStructureRules, SalaryStructureQuerySpecification>();

    /// <summary>Assignments have no code and no status: reads are shared, create, update and delete have their own flows.</summary>
    private static IServiceCollection AddSalaryAssignments(this IServiceCollection services)
    {
        services.AddScoped<SalaryAssignmentMapping>();
        services.AddScoped<IEntityDescriptor<SalaryAssignment>>(provider => provider.GetRequiredService<SalaryAssignmentMapping>());
        services.AddScoped<IDtoMapper<SalaryAssignment, SalaryAssignmentDto>>(provider => provider.GetRequiredService<SalaryAssignmentMapping>());
        services.AddScoped<IQuerySpecification<SalaryAssignment, SalaryAssignmentFilter>, SalaryAssignmentQuerySpecification>();
        services.TryAddScoped<EntityLoader<SalaryAssignment>>();
        services.AddScoped<SalaryAssignmentRules>();

        // There is deliberately no "get all" handler: salaries are only listed in pages of a limited size.
        services.AddScoped<IGetByIdHandler<SalaryAssignmentDto>, GetByIdHandler<SalaryAssignment, SalaryAssignmentDto>>();
        services.AddScoped<IQueryHandler<SalaryAssignmentDto, SalaryAssignmentFilter>, QueryHandler<SalaryAssignment, SalaryAssignmentDto, SalaryAssignmentFilter>>();
        services.AddScoped<ICreateHandler<SalaryAssignmentCreateRequest, SalaryAssignmentDto>, SalaryAssignmentCreateHandler>();
        services.AddScoped<IUpdateHandler<SalaryAssignmentUpdateRequest, SalaryAssignmentDto>, SalaryAssignmentUpdateHandler>();
        services.AddScoped<IDeleteHandler<SalaryAssignmentDto>, SalaryAssignmentDeleteHandler>();
        return services;
    }

    /// <summary>Runs: reads, create, update and delete of drafts, and one command per workflow move.</summary>
    private static IServiceCollection AddPayrollRuns(this IServiceCollection services)
    {
        services
            .AddResource<PayrollRun, PayrollRunDetails, PayrollRunDto, PayrollRunFilter, PayrollRunMapping, PayrollRunQuerySpecification>()
            .AddCreate<PayrollRun, PayrollRunDetails, PayrollRunCreateRequest, PayrollRunDto, PayrollRunRules>()
            .AddUpdate<PayrollRun, PayrollRunDetails, PayrollRunUpdateRequest, PayrollRunDto, PayrollRunRules>();
        services.AddScoped<IDeleteHandler<PayrollRunDto>, PayrollRunDeleteHandler>();
        services.AddScoped<ICommandHandler<ProcessPayrollRunCommand, PayrollRunDto>, ProcessPayrollRunHandler>();
        services.AddScoped<ICommandHandler<ApprovePayrollRunCommand, PayrollRunDto>, ApprovePayrollRunHandler>();
        services.AddScoped<ICommandHandler<MarkPayrollRunPaidCommand, PayrollRunDto>, MarkPayrollRunPaidHandler>();
        services.AddScoped<ICommandHandler<ReturnPayrollRunToDraftCommand, PayrollRunDto>, ReturnPayrollRunToDraftHandler>();
        return services;
    }

    /// <summary>Payslips are read-only: they are made by processing a run and removed by returning it to draft.</summary>
    private static IServiceCollection AddPayslips(this IServiceCollection services)
    {
        services.AddScoped<PayslipMapping>();
        services.AddScoped<IDtoMapper<Payslip, PayslipDto>>(provider => provider.GetRequiredService<PayslipMapping>());
        services.AddScoped<IQuerySpecification<Payslip, PayslipFilter>, PayslipQuerySpecification>();
        services.AddScoped<IGetByIdHandler<PayslipDto>, PayslipGetHandler>();
        services.AddScoped<IMyPayslipsHandler, MyPayslipsHandler>();
        services.AddScoped<IQueryHandler<PayslipDto, PayslipFilter>, QueryHandler<Payslip, PayslipDto, PayslipFilter>>();
        return services;
    }

    /// <summary>What payroll records block: deleting a component, a structure or an employee that they point at.</summary>
    private static IServiceCollection AddPayrollReferences(this IServiceCollection services) => services
        .AddReference<SalaryComponent, SalaryStructureComponent>("salary structure(s)", ReferenceUse.Delete, ownerId => link => link.SalaryComponentId == ownerId)
        .AddReference<SalaryStructure, SalaryAssignment>("salary assignment(s)", ReferenceUse.Delete, ownerId => assignment => assignment.SalaryStructureId == ownerId)
        .AddReference<Employee, SalaryAssignment>("salary assignment(s)", ReferenceUse.Delete, ownerId => assignment => assignment.EmployeeId == ownerId)
        .AddReference<Employee, Payslip>("payslip(s)", ReferenceUse.Delete, ownerId => payslip => payslip.EmployeeId == ownerId);
}
