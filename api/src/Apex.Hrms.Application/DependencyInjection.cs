using Apex.Hrms.Application.Organization;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers validators and use-case handlers. The host must also provide <c>IRepository&lt;&gt;</c>,
    /// <c>IUnitOfWork</c>, <c>IReferenceUsageReader</c>, <c>IClock</c> and <c>ICurrentUser</c>.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);
        return services.AddOrganization();
    }
}
