using Apex.Hrms.Application.Employees;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Apex.Hrms.Application.Excel;

internal static class ExcelServiceCollectionExtensions
{
    /// <summary>Registers the Excel import and export services and the modules that take part. The host supplies <see cref="ExcelLimits"/> (a default is used otherwise).</summary>
    public static IServiceCollection AddExcel(this IServiceCollection services)
    {
        services.TryAddSingleton(new ExcelLimits());
        services.AddScoped<EmployeeExcelDependencies>();
        services.AddScoped<IExcelModule, EmployeeExcelModule>();
        services.AddScoped<IExcelCatalogService, ExcelCatalogService>();
        services.AddScoped<IExcelImportService, ExcelImportService>();
        services.AddScoped<IExcelExportService, ExcelExportService>();
        return services;
    }
}
