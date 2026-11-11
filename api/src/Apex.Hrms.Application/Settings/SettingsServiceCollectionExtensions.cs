using Apex.Hrms.Application.References;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Security;
using Apex.Hrms.Domain.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Apex.Hrms.Application.Settings;

internal static class SettingsServiceCollectionExtensions
{
    /// <summary>Roles, users, the permission matrix and the general settings.</summary>
    public static IServiceCollection AddSettings(this IServiceCollection services) => services
        .AddRoles()
        .AddUsers()
        .AddPermissionsAndGeneralSettings();

    /// <summary>Roles use the shared reads, create and update; activate is a plain command and deactivate and delete have their own rules.</summary>
    private static IServiceCollection AddRoles(this IServiceCollection services)
    {
        services
            .AddResource<AppRole, RoleDetails, RoleDto, RoleFilter, RoleMapping, RoleQuerySpecification>()
            .AddCreate<AppRole, RoleDetails, RoleCreateRequest, RoleDto, RoleRules>()
            .AddUpdate<AppRole, RoleDetails, RoleUpdateRequest, RoleDto, RoleRules>()
            .AddEntityCommand<AppRole, ActivateCommand, RoleDto, ActivateRoleAction>();
        services.AddScoped<ICommandHandler<DeactivateCommand, RoleDto>, DeactivateRoleHandler>();
        services.AddScoped<IDeleteHandler<RoleDto>, DeleteRoleHandler>();
        return services.AddReference<AppRole, AppAccount>("user(s)", ReferenceUse.Delete, roleId => account => account.RoleId == roleId);
    }

    /// <summary>
    /// Users are listed in pages only (no "get all": the list holds emails). Creating, changing the role, the status moves,
    /// deleting and the password reset mail each have their own flow.
    /// </summary>
    private static IServiceCollection AddUsers(this IServiceCollection services)
    {
        services.AddScoped<UserMapping>();
        services.AddScoped<IEntityDescriptor<AppAccount>>(provider => provider.GetRequiredService<UserMapping>());
        services.AddScoped<IQuerySpecification<AppAccount, UserFilter>, UserQuerySpecification>();
        services.TryAddScoped<EntityLoader<AppAccount>>();

        services.AddScoped<UserReadHandlers>();
        services.AddScoped<IQueryHandler<UserDto, UserFilter>>(provider => provider.GetRequiredService<UserReadHandlers>());
        services.AddScoped<IGetByIdHandler<UserDto>>(provider => provider.GetRequiredService<UserReadHandlers>());
        services.AddScoped<ICreateHandler<UserCreateRequest, UserDto>, CreateUserHandler>();
        services.AddScoped<IUpdateHandler<UserUpdateRequest, UserDto>, UpdateUserHandler>();
        services.AddScoped<IDeleteHandler<UserDto>, DeleteUserHandler>();
        services.AddScoped<ICommandHandler<ActivateCommand, UserDto>, ActivateUserHandler>();
        services.AddScoped<ICommandHandler<DeactivateCommand, UserDto>, DeactivateUserHandler>();
        services.AddScoped<ICommandHandler<UnlockUserCommand, UserDto>, UnlockUserHandler>();
        services.AddScoped<IUserPasswordResetHandler, UserPasswordResetHandler>();

        // An employee who has a user account cannot be deleted: the account has to go first.
        return services.AddReference<Employee, AppAccount>("user account(s)", ReferenceUse.Delete, employeeId => account => account.EmployeeId == employeeId);
    }

    private static IServiceCollection AddPermissionsAndGeneralSettings(this IServiceCollection services)
    {
        services.AddScoped<IPermissionMatrixHandler, PermissionMatrixHandler>();
        services.TryAddScoped<EntityLoader<GeneralSettings>>();
        services.AddScoped<IEntityDescriptor<GeneralSettings>, GeneralSettingsDescriptor>();
        services.AddScoped<IGeneralSettingsHandler, GeneralSettingsHandler>();
        return services;
    }
}

internal sealed class GeneralSettingsDescriptor : IEntityDescriptor<GeneralSettings>
{
    public string Label => "General settings";

    public string Describe(GeneralSettings entity) => entity.OrganizationName;
}
