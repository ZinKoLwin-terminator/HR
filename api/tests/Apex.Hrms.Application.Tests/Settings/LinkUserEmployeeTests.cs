using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Security;
using Apex.Hrms.Application.Settings;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Security;

namespace Apex.Hrms.Application.Tests.Settings;

/// <summary>
/// <c>POST /api/users/{id}/link-employee</c>: an account without an employee (an administrator created after the seed linked the
/// others) is linked to the current employee whose work email is the account's email. Never a linked account, never an employee
/// the caller picks, never a leaver or an employee who already has an account; the link ends the sessions of the account.
/// </summary>
public sealed class LinkUserEmployeeTests : IDisposable
{
    private const string Email = "mia.hartley@example.test";

    private readonly SettingsHost settings = new();

    public void Dispose() => settings.Dispose();

    private AppRole AdministratorRole() => settings.SeedRole(SettingsHost.AdministratorRole, builtIn: true);

    /// <summary>An account without an employee, as the bootstrap creates it.</summary>
    private AppAccount SeedUnlinked(string email = Email) =>
        settings.SeedAccount(AdministratorRole(), employeeId: null, "Bootstrap Admin", email);

    private Task<Result<UserDto>> LinkAsync(int id, string? rowVersion) =>
        settings.Handler<ICommandHandler<LinkUserEmployeeCommand, UserDto>>().HandleAsync(new LinkUserEmployeeCommand(id, rowVersion), default);

    private Task<Result<UserDto>> LinkAsync(AppAccount account) => LinkAsync(account.Id, SettingsHost.Version(account.RowVersion));

    private Employee EmployeeEntity(int id) => settings.Host.Repository<Employee>().Items.Single(employee => employee.Id == id);

    private void SetWorkEmail(int employeeId, string workEmail) =>
        typeof(Employee).GetProperty(nameof(Employee.WorkEmail))!.SetValue(EmployeeEntity(employeeId), workEmail);

    [Fact]
    public async Task Linking_sets_the_employee_and_its_name_keeps_the_email_records_the_activity_and_ends_the_sessions_in_one_commit()
    {
        var employee = await settings.SeedEmployeeAsync(Email);
        var account = SeedUnlinked();
        var commits = settings.Host.UnitOfWork.Commits;
        var versionBefore = account.RowVersion.ToArray();

        var linked = ResultAssert.Succeeded(await LinkAsync(account));

        Assert.Equal(account.Id, linked.Id);
        Assert.Equal(employee.Id, linked.EmployeeId);
        Assert.Equal("Mia Hartley", linked.DisplayName);
        Assert.Equal(Email, linked.Email);
        Assert.Equal("Active", linked.Status);
        Assert.NotEqual(versionBefore, linked.RowVersion);
        var stored = settings.Host.Repository<AppAccount>().Items.Single(item => item.Id == account.Id);
        Assert.Equal(employee.Id, stored.EmployeeId);
        Assert.Equal("Mia Hartley", stored.DisplayName);

        Assert.Equal([account.ExternalSubject!], settings.Logins.SessionsInvalidated);
        Assert.Equal(commits + 1, settings.Host.UnitOfWork.Commits);
        Assert.Equal(SecurityPolicy.AdministratorLockName, settings.Host.UnitOfWork.TransactionLocks[^1]);

        var message = settings.Host.ActivityLog.LastMessage;
        Assert.Equal($"User #{account.Id} \"Mia Hartley\" linked to employee {employee.EmployeeCode}", message);
        Assert.Equal("system", settings.Host.ActivityLog.Entries[^1].Category);
        Assert.DoesNotContain("example.test", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_linked_account_is_a_409_and_its_employee_never_changes()
    {
        var first = await settings.SeedEmployeeAsync();
        await settings.SeedEmployeeAsync(Email);
        var account = settings.SeedAccount(AdministratorRole(), first.Id, "Mia Hartley", Email);

        var result = await LinkAsync(account);

        Assert.Equal("This account is already linked to an employee.", ResultAssert.Failed(result, ErrorType.Conflict).Message);
        Assert.Equal(first.Id, settings.Host.Repository<AppAccount>().Items.Single().EmployeeId);
        Assert.Empty(settings.Logins.SessionsInvalidated);
        Assert.DoesNotContain(settings.Host.ActivityLog.Messages, text => text.Contains("linked", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Without_an_employee_whose_work_email_matches_it_is_a_409_naming_the_account_email()
    {
        await settings.SeedEmployeeAsync("someone.else@example.test");
        var account = SeedUnlinked();

        var result = await LinkAsync(account);

        Assert.Equal($"No current employee has the work email {Email}.", ResultAssert.Failed(result, ErrorType.Conflict).Message);
        Assert.Null(settings.Host.Repository<AppAccount>().Items.Single().EmployeeId);
        Assert.Empty(settings.Logins.SessionsInvalidated);
    }

    [Fact]
    public async Task An_employee_with_an_exit_date_is_not_current_and_gets_the_same_409()
    {
        var employee = await settings.SeedEmployeeAsync(Email);
        typeof(Employee).GetProperty(nameof(Employee.ExitDate))!.SetValue(EmployeeEntity(employee.Id), new DateOnly(2026, 12, 31));
        var account = SeedUnlinked();

        var result = await LinkAsync(account);

        Assert.Equal($"No current employee has the work email {Email}.", ResultAssert.Failed(result, ErrorType.Conflict).Message);
        Assert.Null(settings.Host.Repository<AppAccount>().Items.Single().EmployeeId);
        Assert.Empty(settings.Logins.SessionsInvalidated);
    }

    [Theory]
    [InlineData("Mia.Hartley@Example.Test", "mia.hartley@example.test")]
    [InlineData("  mia.hartley@example.test  ", "mia.hartley@example.test")]
    [InlineData("mia.hartley@example.test", "MIA.HARTLEY@EXAMPLE.TEST")]
    public async Task The_work_email_matches_trimmed_and_whatever_its_case(string workEmail, string accountEmail)
    {
        var employee = await settings.SeedEmployeeAsync(Email);
        SetWorkEmail(employee.Id, workEmail);
        var account = SeedUnlinked(accountEmail);

        var linked = ResultAssert.Succeeded(await LinkAsync(account));

        Assert.Equal(employee.Id, linked.EmployeeId);
        Assert.Equal(accountEmail, linked.Email);
    }

    [Fact]
    public async Task An_employee_who_already_has_an_account_is_a_409_naming_the_employee()
    {
        var employee = await settings.SeedEmployeeAsync(Email);
        var role = settings.SeedRole("HR Officer");

        // The other account was created while the employee had another work email: one account per employee (UQ_AppUser_EmployeeId).
        settings.SeedAccount(role, employee.Id, "Mia Hartley", "mia.previous@example.test");
        var account = SeedUnlinked();

        var result = await LinkAsync(account);

        Assert.Equal("Mia Hartley already has a user account.", ResultAssert.Failed(result, ErrorType.Conflict).Message);
        Assert.Null(settings.Host.Repository<AppAccount>().Items.Single(item => item.Id == account.Id).EmployeeId);
        Assert.Empty(settings.Logins.SessionsInvalidated);
    }

    [Fact]
    public async Task An_administrator_may_link_their_own_account_and_their_own_sessions_end_too()
    {
        var employee = await settings.SeedEmployeeAsync(Email);
        var account = SeedUnlinked();
        settings.Host.User.UserId = account.Id;

        var linked = ResultAssert.Succeeded(await LinkAsync(account));

        Assert.Equal(employee.Id, linked.EmployeeId);
        Assert.Equal([account.ExternalSubject!], settings.Logins.SessionsInvalidated);
    }

    [Fact]
    public async Task An_unknown_account_is_404_and_a_stale_or_missing_row_version_is_refused()
    {
        await settings.SeedEmployeeAsync(Email);
        var account = SeedUnlinked();

        ResultAssert.Failed(await LinkAsync(9999, SettingsHost.Version(account.RowVersion)), ErrorType.NotFound);
        ResultAssert.FailedOnField(await LinkAsync(account.Id, null), "rowVersion");
        Assert.Null(settings.Host.Repository<AppAccount>().Items.Single().EmployeeId);

        // A stale version fails at the save, before the sessions are touched, and nothing is committed.
        var commits = settings.Host.UnitOfWork.Commits;
        ResultAssert.Failed(await LinkAsync(account.Id, SettingsHost.Version([9, 9, 9, 9, 9, 9, 9, 9])), ErrorType.Conflict);
        Assert.Equal(commits, settings.Host.UnitOfWork.Commits);
        Assert.Empty(settings.Logins.SessionsInvalidated);
    }

    // ---- The administrator lock: the rules are read after the lock is granted ----

    [Fact]
    public async Task While_another_administrator_change_holds_the_lock_the_link_is_a_409_and_nothing_changes()
    {
        await settings.SeedEmployeeAsync(Email);
        var account = SeedUnlinked();
        settings.Host.UnitOfWork.HeldElsewhere.Add(SecurityPolicy.AdministratorLockName);

        var result = await LinkAsync(account);

        Assert.Equal("Another change to the administrators is in progress. Try again.", ResultAssert.Failed(result, ErrorType.Conflict).Message);
        Assert.Null(settings.Host.Repository<AppAccount>().Items.Single().EmployeeId);
        Assert.Empty(settings.Logins.SessionsInvalidated);
    }

    [Fact]
    public async Task An_exit_committed_just_before_the_link_got_the_lock_is_seen_and_the_link_is_refused()
    {
        var employee = await settings.SeedEmployeeAsync(Email);
        var account = SeedUnlinked();
        settings.Host.UnitOfWork.OnBeginTransaction = _ =>
            typeof(Employee).GetProperty(nameof(Employee.ExitDate))!.SetValue(EmployeeEntity(employee.Id), new DateOnly(2026, 11, 1));

        var result = await LinkAsync(account);

        Assert.Equal($"No current employee has the work email {Email}.", ResultAssert.Failed(result, ErrorType.Conflict).Message);
        Assert.Null(settings.Host.Repository<AppAccount>().Items.Single().EmployeeId);
        Assert.Empty(settings.Logins.SessionsInvalidated);
    }

    [Fact]
    public async Task An_account_given_to_the_employee_just_before_the_link_got_the_lock_is_seen_and_the_link_is_refused()
    {
        var employee = await settings.SeedEmployeeAsync(Email);
        var account = SeedUnlinked();
        var role = settings.SeedRole("HR Officer");
        settings.Host.UnitOfWork.OnBeginTransaction = _ => settings.SeedAccount(role, employee.Id, "Mia Hartley", "mia.other@example.test");

        var result = await LinkAsync(account);

        Assert.Equal("Mia Hartley already has a user account.", ResultAssert.Failed(result, ErrorType.Conflict).Message);
        Assert.Null(settings.Host.Repository<AppAccount>().Items.Single(item => item.Id == account.Id).EmployeeId);
    }

    [Fact]
    public async Task An_account_linked_by_another_request_just_before_the_lock_is_refused_as_already_linked()
    {
        var other = await settings.SeedEmployeeAsync();
        await settings.SeedEmployeeAsync(Email);
        var account = SeedUnlinked();
        settings.Host.UnitOfWork.OnBeginTransaction = _ => Assert.True(account.LinkEmployee(other.Id, "Mia Hartley").IsSuccess);

        var result = await LinkAsync(account);

        Assert.Equal("This account is already linked to an employee.", ResultAssert.Failed(result, ErrorType.Conflict).Message);
        Assert.Equal(other.Id, settings.Host.Repository<AppAccount>().Items.Single().EmployeeId);
    }

    [Fact]
    public async Task When_the_sessions_cannot_be_ended_nothing_is_committed_and_the_link_and_its_activity_entry_roll_back()
    {
        await settings.SeedEmployeeAsync(Email);
        var account = SeedUnlinked();
        var unitOfWork = settings.Host.UnitOfWork;
        unitOfWork.RollBackUncommittedTransactions = true;
        settings.Logins.InvalidateFailure = new InvalidOperationException("The identity store refused the change: ConcurrencyFailure.");
        var commits = unitOfWork.Commits;
        var rollBacks = unitOfWork.RollBacks;
        var entries = settings.Host.ActivityLog.Entries.Count;

        await Assert.ThrowsAsync<InvalidOperationException>(() => LinkAsync(account));

        Assert.Equal(1, settings.Logins.InvalidateAttempts);
        Assert.Equal(commits, unitOfWork.Commits);
        Assert.Equal(rollBacks + 1, unitOfWork.RollBacks);
        var stored = settings.Host.Repository<AppAccount>().Items.Single();
        Assert.Null(stored.EmployeeId);
        Assert.Equal("Bootstrap Admin", stored.DisplayName);
        Assert.Equal(entries, settings.Host.ActivityLog.Entries.Count);
        Assert.DoesNotContain(settings.Host.ActivityLog.Messages, text => text.Contains("linked", StringComparison.Ordinal));
    }
}
