using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Employees;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Apex.Hrms.Application.Employees;

internal static class EmployeeServiceCollectionExtensions
{
    /// <summary>
    /// Registers the employee handlers piece by piece instead of with AddCrudResource: there is deliberately no
    /// "get all" handler (employees are only listed in pages of at most 200, so one call cannot dump the whole
    /// workforce), reads come in three shapes (list, detail, personal), and the status moves are commands of their own.
    /// </summary>
    public static IServiceCollection AddEmployees(this IServiceCollection services)
    {
        services.AddScoped<EmployeeMapping>();
        services.AddScoped<IEntityDescriptor<Employee>>(provider => provider.GetRequiredService<EmployeeMapping>());
        services.AddScoped<IDtoMapper<Employee, EmployeeDto>>(provider => provider.GetRequiredService<EmployeeMapping>());
        services.AddScoped<IDtoMapper<Employee, EmployeeListItemDto>>(provider => provider.GetRequiredService<EmployeeMapping>());
        services.AddScoped<IDtoMapper<Employee, EmployeePersonalDto>>(provider => provider.GetRequiredService<EmployeeMapping>());
        services.AddScoped<IQuerySpecification<Employee, EmployeeFilter>, EmployeeQuerySpecification>();
        services.TryAddScoped<EntityLoader<Employee>>();
        services.AddScoped<EmployeeRules>();
        services.AddScoped<EmployeeCodeGenerator>();

        // Reads: list rows, the employee without personal data, and the personal data on its own.
        services.AddScoped<IQueryHandler<EmployeeListItemDto, EmployeeFilter>, QueryHandler<Employee, EmployeeListItemDto, EmployeeFilter>>();
        services.AddScoped<IGetByIdHandler<EmployeeDto>, GetByIdHandler<Employee, EmployeeDto>>();
        services.AddScoped<IGetByIdHandler<EmployeePersonalDto>, GetByIdHandler<Employee, EmployeePersonalDto>>();
        services.AddScoped<IGetByIdHandler<BankAccountDto>, BankAccountGetHandler>();
        services.AddScoped<IEmployeeHistoryHandler, EmployeeHistoryHandler>();
        services.AddScoped<INextEmployeeCodeHandler, NextEmployeeCodeHandler>();

        // Writes.
        services.AddScoped<ICreateHandler<EmployeeCreateRequest, EmployeeDto>, EmployeeCreateHandler>();
        services.AddScoped<IUpdateHandler<EmployeeUpdateRequest, EmployeeDto>, EmployeeUpdateHandler>();
        services.AddScoped<IUpdateHandler<EmployeePersonalUpdateRequest, EmployeePersonalDto>, EmployeePersonalUpdateHandler>();
        services.AddScoped<IUpdateHandler<BankAccountUpdateRequest, BankAccountDto>, BankAccountUpdateHandler>();
        services.AddDelete<Employee, EmployeeDto>();

        // Commands: leaving, coming back, and moving between the current statuses.
        services.AddScoped<ICommandAction<Employee, ExitEmployeeCommand>, ExitEmployeeAction>();
        services.AddScoped<ICommandHandler<ExitEmployeeCommand, EmployeeDto>, ExitEmployeeHandler>();
        services.AddEntityCommand<Employee, ReinstateEmployeeCommand, EmployeeDto, ReinstateEmployeeAction>();
        services.AddEntityCommand<Employee, ChangeEmployeeStatusCommand, EmployeeDto, ChangeEmployeeStatusAction>();

        return services.AddEmployeeReferences();
    }
}
