using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Settings;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Security;

namespace Apex.Hrms.Application.Tests.Settings;

/// <summary>Security re-review fixes M2, L2 and L4: who counts as an administrator, who may record an exit, and sessions ending.</summary>
public sealed class AdministratorAndLeaverRuleTests : IDisposable
{
    private readonly SettingsHost settings = new();

    public void Dispose() => settings.Dispose();

    private Task<Result<EmployeeDto>> ExitAsync(EmployeeDto employee) =>
        settings.Handler<ICommandHandler<ExitEmployeeCommand, EmployeeDto>>().HandleAsync(
            new ExitEmployeeCommand(employee.Id, SettingsHost.Version(employee.RowVersion), new DateOnly(2026, 11, 1), "Resigned"),
            default);

    [Fact]
    public async Task An_administrator_whose_employee_has_left_is_not_counted_so_the_last_working_one_cannot_be_deactivated()
    {
        var admin = settings.SeedRole(SettingsHost.AdministratorRole, builtIn: true);
        var working = settings.SeedAccount(admin, employeeId: null, "Admin One", "admin1@example.test");
        var leaver = settings.SeedAccount(admin, await settings.MarkLeftAsync(), "Admin Gone", "gone@example.test");
        settings.Host.User.UserId = 999;

        var result = await settings.Handler<ICommandHandler<DeactivateCommand, UserDto>>()
            .HandleAsync(new DeactivateCommand(working.Id, SettingsHost.Version(working.RowVersion)), default);

        ResultAssert.Failed(result, ErrorType.Conflict, "There must always be one active HR Administrator.");
        Assert.Equal(AccountStatus.Active, leaver.Status);
    }

    [Fact]
    public async Task Deactivating_a_left_administrator_is_allowed_because_it_removes_nobody_who_can_sign_in()
    {
        var admin = settings.SeedRole(SettingsHost.AdministratorRole, builtIn: true);
        settings.SeedAccount(admin, employeeId: null, "Admin One", "admin1@example.test");
        var leaver = settings.SeedAccount(admin, await settings.MarkLeftAsync(), "Admin Gone", "gone@example.test");
        settings.Host.User.UserId = 999;

        var result = await settings.Handler<ICommandHandler<DeactivateCommand, UserDto>>()
            .HandleAsync(new DeactivateCommand(leaver.Id, SettingsHost.Version(leaver.RowVersion)), default);

        ResultAssert.Succeeded(result);
    }

    [Fact]
    public async Task Activating_the_account_of_a_leaver_is_refused_with_a_conflict()
    {
        var officer = settings.SeedRole("HR Officer");
        var leaver = settings.SeedAccount(officer, await settings.MarkLeftAsync(), "Mia Hartley", "mia@example.test", AccountStatus.Inactive);

        var result = await settings.Handler<ICommandHandler<ActivateCommand, UserDto>>()
            .HandleAsync(new ActivateCommand(leaver.Id, SettingsHost.Version(leaver.RowVersion)), default);

        ResultAssert.Failed(result, ErrorType.Conflict, "This user's employee has left the company, so the account cannot be activated.");
        Assert.Equal(AccountStatus.Inactive, leaver.Status);
    }

    [Fact]
    public async Task A_caller_who_is_not_an_administrator_cannot_record_the_exit_of_an_administrator()
    {
        var admin = settings.SeedRole(SettingsHost.AdministratorRole, builtIn: true);
        var officer = settings.SeedRole("HR Officer");
        var target = await settings.SeedEmployeeAsync();
        settings.SeedAccount(admin, target.Id, "Mia Hartley", "mia@example.test");
        settings.SeedAccount(admin, employeeId: null, "Admin Two", "admin2@example.test");
        settings.Host.User.UserId = settings.SeedAccount(officer, employeeId: null, "Olive Officer", "olive@example.test").Id;

        var result = await ExitAsync(target);

        ResultAssert.Failed(result, ErrorType.Forbidden, "Only an HR Administrator can record the exit of an HR Administrator.");
        Assert.Empty(settings.Logins.SessionsInvalidated);
        Assert.Equal(AccountStatus.Active, settings.Host.Repository<AppAccount>().Items.First(a => a.EmployeeId == target.Id).Status);
    }

    [Fact]
    public async Task An_administrator_can_record_the_exit_of_another_administrator()
    {
        var admin = settings.SeedRole(SettingsHost.AdministratorRole, builtIn: true);
        var target = await settings.SeedEmployeeAsync();
        settings.SeedAccount(admin, target.Id, "Mia Hartley", "mia@example.test");
        settings.Host.User.UserId = settings.SeedAccount(admin, employeeId: null, "Admin Two", "admin2@example.test").Id;

        ResultAssert.Succeeded(await ExitAsync(target));
    }

    [Fact]
    public async Task An_employee_cannot_record_their_own_exit()
    {
        var officer = settings.SeedRole("HR Officer");
        var employee = await settings.SeedEmployeeAsync();
        settings.SeedAccount(officer, employee.Id, "Mia Hartley", "mia@example.test");
        settings.Host.User.EmployeeId = employee.Id;

        var result = await ExitAsync(employee);

        ResultAssert.Failed(result, ErrorType.Conflict, "You cannot record your own exit.");
        Assert.Empty(settings.Logins.SessionsInvalidated);
        Assert.Null(settings.Host.Repository<Apex.Hrms.Domain.Employees.Employee>().Items.Single().ExitDate);
    }

    [Fact]
    public async Task Deactivating_a_user_rotates_the_security_stamp_so_the_open_session_ends()
    {
        var officer = settings.SeedRole("HR Officer");
        var employee = await settings.SeedEmployeeAsync();
        var user = ResultAssert.Succeeded(await settings.CreateUserAsync(employee.Id, officer.Id));
        var subject = settings.Host.Repository<AppAccount>().Items.Single().ExternalSubject!;
        settings.Host.User.UserId = 999;

        ResultAssert.Succeeded(await settings.Handler<ICommandHandler<DeactivateCommand, UserDto>>()
            .HandleAsync(new DeactivateCommand(user.Id, SettingsHost.Version(user.RowVersion)), default));

        Assert.Equal([subject], settings.Logins.SessionsInvalidated);
    }

    [Fact]
    public async Task Deleting_a_user_also_rotates_the_security_stamp()
    {
        var officer = settings.SeedRole("HR Officer");
        var employee = await settings.SeedEmployeeAsync();
        var user = ResultAssert.Succeeded(await settings.CreateUserAsync(employee.Id, officer.Id));
        var subject = settings.Host.Repository<AppAccount>().Items.Single().ExternalSubject!;
        settings.Host.User.UserId = 999;

        Assert.True((await settings.Handler<IDeleteHandler<UserDto>>().HandleAsync(user.Id, SettingsHost.Version(user.RowVersion), default)).IsSuccess);

        Assert.Equal([subject], settings.Logins.SessionsInvalidated);
        Assert.Equal([subject], settings.Logins.Deleted);
    }
}
