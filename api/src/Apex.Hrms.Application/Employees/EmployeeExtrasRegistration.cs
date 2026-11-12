using Apex.Hrms.Application.Employees.Documents;
using Apex.Hrms.Application.Employees.Salary;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Employees;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.Application.Employees;

internal static class EmployeeExtrasRegistration
{
    /// <summary>
    /// Employee documents (metadata CRUD, paged reads only: documents are personal data, so there is no "get all") and the
    /// read-only salary summary.
    /// </summary>
    public static IServiceCollection AddEmployeeExtras(this IServiceCollection services)
    {
        services
            .AddResource<EmployeeDocument, EmployeeDocumentSaveDetails, EmployeeDocumentDto, EmployeeDocumentFilter, EmployeeDocumentMapping, EmployeeDocumentQuerySpecification>()
            .AddCreate<EmployeeDocument, EmployeeDocumentSaveDetails, EmployeeDocumentCreateRequest, EmployeeDocumentDto, EmployeeDocumentSaveRules>()
            .AddUpdate<EmployeeDocument, EmployeeDocumentSaveDetails, EmployeeDocumentUpdateRequest, EmployeeDocumentDto, EmployeeDocumentSaveRules>()
            .AddDelete<EmployeeDocument, EmployeeDocumentDto>();
        services.AddScoped<IGetByIdHandler<EmployeeSalarySummaryDto>, EmployeeSalarySummaryHandler>();
        return services;
    }
}
