using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Security;
using Apex.Hrms.Application.Settings;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Security;

namespace Apex.Hrms.Application.Tests.Settings;

/// <summary>A leaver keeps no access: the exit command switches off the linked account and ends its sessions in one transaction.</summary>
public sealed class LeaverAccountTests : IDisposable
{
    private readonly SettingsHost settings = new();

    public void Dispose() => settings.Dispose();

    private Task<Result<EmployeeDto>> ExitAsync(EmployeeDto employee) =>
        settings.Handler<ICommandHandler<ExitEmployeeCommand, EmployeeDto>>().HandleAsync(
            new ExitEmployeeCommand(employee.Id, SettingsHost.Version(employee.RowVersion), new DateOnly(2026, 11, 1), "Resigned"),
            default);

    [Fact]
    public async Task Exit_deactivates_the_linked_account_and_changes_the_security_stamp_in_one_committed_transaction()
    {
        var role = settings.SeedRole("HR Officer");
        var employee = await settings.SeedEmployeeAsync();
        var user = ResultAssert.Succeeded(await settings.CreateUserAsync(employee.Id, role.Id));
        var subject = settings.Host.Repository<AppAccount>().Items.Single().ExternalSubject!;
        var commits = settings.Host.UnitOfWork.Commits;

        ResultAssert.Succeeded(await ExitAsync(employee));

        var account = settings.Host.Repository<AppAccount>().Items.Single();
        Assert.Equal(user.Id, account.Id);
        Assert.Equal(AccountStatus.Inactive, account.Status);
        Assert.Equal([subject], settings.Logins.SessionsInvalidated);
        Assert.Equal(commits + 1, settings.Host.UnitOfWork.Commits);
        Assert.Equal(SecurityPolicy.AdministratorLockName, settings.Host.UnitOfWork.TransactionLocks[^1]);
    }

    [Fact]
    public async Task Exit_of_an_employee_without_an_account_changes_no_login_and_opens_no_transaction()
    {
        var employee = await settings.SeedEmployeeAsync();
        var transactions = settings.Host.UnitOfWork.TransactionLocks.Count;

        ResultAssert.Succeeded(await ExitAsync(employee));

        Assert.Empty(settings.Logins.SessionsInvalidated);
        Assert.Equal(transactions, settings.Host.UnitOfWork.TransactionLocks.Count);
    }

    [Fact]
    public async Task Exit_of_an_employee_whose_account_is_already_inactive_leaves_the_login_alone()
    {
        var role = settings.SeedRole("HR Officer");
        var employee = await settings.SeedEmployeeAsync();
        settings.SeedAccount(role, employee.Id, "Mia Hartley", "mia@example.test", AccountStatus.Inactive);

        ResultAssert.Succeeded(await ExitAsync(employee));

        Assert.Empty(settings.Logins.SessionsInvalidated);
    }

    [Fact]
    public async Task Exit_of_the_last_active_administrator_is_refused_with_a_conflict_and_the_account_stays_active()
    {
        var admin = settings.SeedRole(SettingsHost.AdministratorRole, builtIn: true);
        var employee = await settings.SeedEmployeeAsync();
        settings.SeedAccount(admin, employee.Id, "Mia Hartley", "mia@example.test");
        var commits = settings.Host.UnitOfWork.Commits;

        var result = await ExitAsync(employee);

        ResultAssert.Failed(result, ErrorType.Conflict, "There must always be one active HR Administrator.");
        Assert.Equal(AccountStatus.Active, settings.Host.Repository<AppAccount>().Items.Single().Status);
        Assert.Empty(settings.Logins.SessionsInvalidated);
        Assert.Equal(commits, settings.Host.UnitOfWork.Commits);
    }

    [Fact]
    public async Task Exit_of_an_administrator_is_allowed_once_another_active_administrator_exists()
    {
        var admin = settings.SeedRole(SettingsHost.AdministratorRole, builtIn: true);
        var employee = await settings.SeedEmployeeAsync();
        settings.SeedAccount(admin, employee.Id, "Mia Hartley", "mia@example.test");
        settings.SeedAccount(admin, employeeId: null, "Admin Two", "admin2@example.test");

        ResultAssert.Succeeded(await ExitAsync(employee));

        Assert.Equal(1, settings.Host.Repository<AppAccount>().Items.Count(account => account.Status == AccountStatus.Active));
        Assert.Single(settings.Logins.SessionsInvalidated);
    }

    [Fact]
    public async Task Exit_with_a_stale_row_version_or_an_invalid_date_touches_no_account()
    {
        var role = settings.SeedRole("HR Officer");
        var employee = await settings.SeedEmployeeAsync();
        settings.SeedAccount(role, employee.Id, "Mia Hartley", "mia@example.test");

        var invalid = await settings.Handler<ICommandHandler<ExitEmployeeCommand, EmployeeDto>>().HandleAsync(
            new ExitEmployeeCommand(employee.Id, SettingsHost.Version(employee.RowVersion), null, "Resigned"),
            default);

        Assert.True(invalid.IsFailure);
        Assert.Equal(AccountStatus.Active, settings.Host.Repository<AppAccount>().Items.Single().Status);
        Assert.Empty(settings.Logins.SessionsInvalidated);
    }

    [Fact]
    public void An_account_whose_employee_has_left_cannot_sign_in_whatever_its_status()
    {
        var snapshot = new AccountSnapshot(1, 5, "TST- Person", "tst@example.test", "subject", AccountStatus.Active, 2, "HR Officer", true, EmployeeLeft: true, []);

        Assert.False(snapshot.CanSignIn);
        Assert.True((snapshot with { EmployeeLeft = false }).CanSignIn);
    }
}
