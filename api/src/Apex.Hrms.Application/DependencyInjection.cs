using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Activity;
using Apex.Hrms.Application.Attendance;
using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Excel;
using Apex.Hrms.Application.Leave;
using Apex.Hrms.Application.Organization;
using Apex.Hrms.Application.Recruitment;
using Apex.Hrms.Application.Security;
using Apex.Hrms.Application.Settings;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Apex.Hrms.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers validators, use-case handlers and the default (allow-all) access scope. The host must also provide
    /// <c>IRepository&lt;&gt;</c>, <c>IRecordCounter&lt;&gt;</c>, <c>IUnitOfWork</c>, <c>IClock</c>, <c>ICurrentUser</c>, <c>IActivityRecorder</c>, <c>IActivityLogStore</c>, <c>INotificationStore</c>, <c>IDashboardSource</c>, <c>IAccountLogins</c> and <c>IPermissionStore</c>.
    /// A host or module narrows a resource by registering its own closed <c>IAccessScope&lt;T&gt;</c>, which wins over the default.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);
        services.TryAddScoped(typeof(IAccessScope<>), typeof(AllowAllAccessScope<>));
        services.TryAddScoped<AdministratorBootstrap>();
        services.TryAddSingleton(provider => new RoleGrantCache(provider.GetService<TimeProvider>() ?? TimeProvider.System));
        services.TryAddSingleton<IRoleGrantInvalidator>(provider => provider.GetRequiredService<RoleGrantCache>());
        return services.AddOrganization().AddEmployees().AddLeave().AddAttendance().AddRecruitment().AddSettings().AddActivityAndDashboard().AddExcel();
    }
}
