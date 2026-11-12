using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Settings;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Security;

namespace Apex.Hrms.Application.Tests.Settings;

/// <summary>Creating and managing user accounts: the rules of the mock service plus the decisions about passwords and administrators.</summary>
public sealed class UserHandlerTests : IDisposable
{
    private const string Password = "a-long-enough-password";

    private readonly SettingsHost settings = new();

    public void Dispose() => settings.Dispose();

    private AppRole OfficerRole() => settings.SeedRole("HR Officer");

    // ---- Create ----

    [Fact]
    public async Task Create_makes_an_active_account_from_the_work_email_in_lower_case_and_names_only_the_display_name_in_the_activity()
    {
        var role = OfficerRole();
        var employee = await settings.SeedEmployeeAsync("Mia.Hartley@Example.Test");

        var user = ResultAssert.Succeeded(await settings.CreateUserAsync(employee.Id, role.Id));

        Assert.Equal("mia.hartley@example.test", user.Email);
        Assert.Equal("Mia Hartley", user.DisplayName);
        Assert.Equal("Active", user.Status);
        Assert.Equal(employee.Id, user.EmployeeId);
        Assert.Equal(role.Id, user.RoleId);
        Assert.Equal(["mia.hartley@example.test"], settings.Logins.CreatedEmails);
        var message = settings.Host.ActivityLog.LastMessage;
        Assert.Equal("User \"Mia Hartley\" created", message);
        Assert.Equal("system", settings.Host.ActivityLog.Entries[^1].Category);
        Assert.DoesNotContain("example.test", message, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Create_never_puts_the_password_in_the_response_or_in_any_activity_entry()
    {
        var role = OfficerRole();
        var employee = await settings.SeedEmployeeAsync();

        var user = ResultAssert.Succeeded(await settings.CreateUserAsync(employee.Id, role.Id, Password));

        Assert.DoesNotContain(Password, System.Text.Json.JsonSerializer.Serialize(user), StringComparison.Ordinal);
        Assert.All(settings.Host.ActivityLog.Messages, message => Assert.DoesNotContain(Password, message, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(9999)]
    public async Task Create_needs_an_existing_employee(int employeeId)
    {
        var role = OfficerRole();

        var result = await settings.CreateUserAsync(employeeId, role.Id);

        ResultAssert.FailedOnField(result, "employeeId", "Choose a current employee.");
        Assert.Empty(settings.Logins.CreatedEmails);
    }

    [Fact]
    public async Task Create_needs_a_current_employee_without_an_exit_date()
    {
        var role = OfficerRole();
        var employee = await settings.SeedEmployeeAsync();
        await settings.ExitAsync(employee);

        var result = await settings.CreateUserAsync(employee.Id, role.Id);

        ResultAssert.FailedOnField(result, "employeeId", "Choose a current employee.");
        Assert.Empty(settings.Logins.CreatedEmails);
    }

    [Fact]
    public async Task One_employee_gets_one_user_account()
    {
        var role = OfficerRole();
        var employee = await settings.SeedEmployeeAsync();
        ResultAssert.Succeeded(await settings.CreateUserAsync(employee.Id, role.Id));

        var second = await settings.CreateUserAsync(employee.Id, role.Id);

        var error = ResultAssert.FailedOnField(second, "employeeId", "This employee already has a user account.");
        Assert.Equal("A user already exists for this employee.", error.FieldErrors!["employeeId"][0]);
        Assert.Single(settings.Logins.CreatedEmails);
    }

    [Fact]
    public async Task The_role_must_exist_and_be_active()
    {
        var inactive = settings.SeedRole("Retired Role", active: false);
        var employee = await settings.SeedEmployeeAsync();

        ResultAssert.FailedOnField(await settings.CreateUserAsync(employee.Id, inactive.Id), "roleId", "Choose an active role.");
        ResultAssert.FailedOnField(await settings.CreateUserAsync(employee.Id, 9999), "roleId", "Choose an active role.");
        Assert.Empty(settings.Logins.CreatedEmails);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("short-11-ch")]
    public async Task The_password_needs_at_least_twelve_characters_and_nothing_is_created_without_one(string? password)
    {
        var role = OfficerRole();
        var employee = await settings.SeedEmployeeAsync();

        var result = await settings.CreateUserAsync(employee.Id, role.Id, password);

        var error = ResultAssert.FailedOnField(result, "initialPassword");
        Assert.Equal("Use at least 12 characters.", error.FieldErrors!["initialPassword"][0]);
        Assert.Empty(settings.Logins.CreatedEmails);
        Assert.Empty(settings.Host.Repository<AppAccount>().Items);
    }

    [Fact]
    public async Task A_password_of_exactly_twelve_characters_is_accepted()
    {
        var role = OfficerRole();
        var employee = await settings.SeedEmployeeAsync();

        ResultAssert.Succeeded(await settings.CreateUserAsync(employee.Id, role.Id, "123456789012"));

        Assert.Equal(12, settings.Logins.LastPasswordLength);
    }

    [Theory]
    [InlineData("Locked")]
    [InlineData("Banana")]
    [InlineData("active")]
    public async Task A_new_account_starts_Active_or_Inactive_only(string status)
    {
        var role = OfficerRole();
        var employee = await settings.SeedEmployeeAsync();

        var result = await settings.CreateUserAsync(employee.Id, role.Id, status: status);

        ResultAssert.FailedOnField(result, "status", "Status must be Active or Inactive.");
    }

    [Fact]
    public async Task A_new_account_can_start_Inactive()
    {
        var role = OfficerRole();
        var employee = await settings.SeedEmployeeAsync();

        var user = ResultAssert.Succeeded(await settings.CreateUserAsync(employee.Id, role.Id, status: "Inactive"));

        Assert.Equal("Inactive", user.Status);
    }

    [Fact]
    public async Task A_work_email_already_used_by_another_account_is_refused_before_a_login_is_created()
    {
        var role = OfficerRole();
        settings.SeedAccount(role, employeeId: null, "Someone", "person1@example.test");
        var employee = await settings.SeedEmployeeAsync("person1@example.test");

        var result = await settings.CreateUserAsync(employee.Id, role.Id);

        ResultAssert.FailedOnField(result, "employeeId", "An account with this email address already exists.");
        Assert.Empty(settings.Logins.CreatedEmails);
    }

    [Fact]
    public async Task The_login_and_the_account_are_created_in_one_transaction_that_is_committed_only_when_the_account_is_saved()
    {
        var role = OfficerRole();
        var employee = await settings.SeedEmployeeAsync();
        var commitsBefore = settings.Host.UnitOfWork.Commits;

        ResultAssert.Succeeded(await settings.CreateUserAsync(employee.Id, role.Id));

        Assert.Equal(commitsBefore + 1, settings.Host.UnitOfWork.Commits);
        Assert.Null(settings.Host.UnitOfWork.TransactionLocks[^1]);
    }

    [Fact]
    public async Task When_saving_the_account_fails_nothing_is_committed_and_no_compensating_delete_runs()
    {
        var role = OfficerRole();
        var employee = await settings.SeedEmployeeAsync();
        var commitsBefore = settings.Host.UnitOfWork.Commits;
        settings.Host.UnitOfWork.FailNextSaveWith = AppError.Conflict("A record with the same unique value already exists.");

        var result = await settings.CreateUserAsync(employee.Id, role.Id);

        // The identity user was written inside the transaction, which is rolled back by the database (the integration test
        // proves that no auth.User row is left); the handler does not try to repair anything by hand.
        ResultAssert.Failed(result, ErrorType.Conflict);
        Assert.Equal(commitsBefore, settings.Host.UnitOfWork.Commits);
        Assert.Empty(settings.Logins.Deleted);
    }

    [Fact]
    public void The_create_request_never_prints_the_initial_password()
    {
        var request = new UserCreateRequest(7, 3, "Active", "a-secret-initial-password");

        var text = request.ToString();

        Assert.DoesNotContain("a-secret-initial-password", text, StringComparison.Ordinal);
        Assert.DoesNotContain("InitialPassword", text, StringComparison.Ordinal);
        Assert.Contains("EmployeeId = 7", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Demoting_deactivating_or_deleting_an_administrator_takes_the_administrator_lock_in_a_transaction()
    {
        var admin = settings.SeedRole(SettingsHost.AdministratorRole, builtIn: true);
        var officer = OfficerRole();
        var one = settings.SeedAccount(admin, employeeId: null, "Admin One", "admin1@example.test");
        var two = settings.SeedAccount(admin, employeeId: null, "Admin Two", "admin2@example.test");
        var three = settings.SeedAccount(admin, employeeId: null, "Admin Three", "admin3@example.test");
        var unitOfWork = settings.Host.UnitOfWork;

        ResultAssert.Succeeded(await settings.Handler<IUpdateHandler<UserUpdateRequest, UserDto>>()
            .HandleAsync(one.Id, new UserUpdateRequest(officer.Id, SettingsHost.Version(one.RowVersion)), default));
        ResultAssert.Succeeded(await settings.Handler<ICommandHandler<DeactivateCommand, UserDto>>()
            .HandleAsync(new DeactivateCommand(two.Id, SettingsHost.Version(two.RowVersion)), default));
        // Admin Three is now the last one: the refused delete still held the lock and was never committed.
        var commits = unitOfWork.Commits;
        ResultAssert.Failed(
            await settings.Handler<IDeleteHandler<UserDto>>().HandleAsync(three.Id, SettingsHost.Version(three.RowVersion), default),
            ErrorType.Conflict,
            "There must always be one active HR Administrator.");

        Assert.Equal(commits, unitOfWork.Commits);
        Assert.Equal(3, unitOfWork.TransactionLocks.Count(name => name == Application.Security.SecurityPolicy.AdministratorLockName));
    }

    // ---- Update (role) ----

    [Fact]
    public async Task The_role_of_a_user_can_be_changed_and_the_row_version_moves()
    {
        var officer = OfficerRole();
        var manager = settings.SeedRole("Department Manager");
        var employee = await settings.SeedEmployeeAsync();
        var user = ResultAssert.Succeeded(await settings.CreateUserAsync(employee.Id, officer.Id));

        var updated = ResultAssert.Succeeded(await settings.Handler<IUpdateHandler<UserUpdateRequest, UserDto>>()
            .HandleAsync(user.Id, new UserUpdateRequest(manager.Id, SettingsHost.Version(user.RowVersion)), default));

        Assert.Equal(manager.Id, updated.RoleId);
        Assert.NotEqual(user.RowVersion, updated.RowVersion);
        Assert.Equal("User \"Mia Hartley\" updated", settings.Host.ActivityLog.LastMessage);
    }

    [Fact]
    public async Task A_user_cannot_get_an_inactive_role_and_a_stale_row_version_is_a_conflict()
    {
        var officer = OfficerRole();
        var retired = settings.SeedRole("Retired Role", active: false);
        var employee = await settings.SeedEmployeeAsync();
        var user = ResultAssert.Succeeded(await settings.CreateUserAsync(employee.Id, officer.Id));
        var update = settings.Handler<IUpdateHandler<UserUpdateRequest, UserDto>>();

        ResultAssert.FailedOnField(
            await update.HandleAsync(user.Id, new UserUpdateRequest(retired.Id, SettingsHost.Version(user.RowVersion)), default),
            "roleId",
            "Choose an active role.");
        ResultAssert.Failed(
            await update.HandleAsync(user.Id, new UserUpdateRequest(officer.Id, SettingsHost.Version([9, 9, 9, 9, 9, 9, 9, 9])), default),
            ErrorType.Conflict);
        ResultAssert.FailedOnField(await update.HandleAsync(user.Id, new UserUpdateRequest(officer.Id, null), default), "rowVersion");
    }

    [Fact]
    public async Task An_administrator_cannot_change_their_own_role()
    {
        var admin = settings.SeedRole(SettingsHost.AdministratorRole, builtIn: true);
        var officer = OfficerRole();
        var me = settings.SeedAccount(admin, employeeId: null, "Admin One", "admin1@example.test");
        settings.SeedAccount(admin, employeeId: null, "Admin Two", "admin2@example.test");
        settings.Host.User.UserId = me.Id;

        var result = await settings.Handler<IUpdateHandler<UserUpdateRequest, UserDto>>()
            .HandleAsync(me.Id, new UserUpdateRequest(officer.Id, SettingsHost.Version(me.RowVersion)), default);

        ResultAssert.Failed(result, ErrorType.Conflict, "You cannot change your own role or deactivate your own account.");
    }

    [Fact]
    public async Task The_last_active_administrator_cannot_be_demoted_but_another_one_can_once_a_second_exists()
    {
        var admin = settings.SeedRole(SettingsHost.AdministratorRole, builtIn: true);
        var officer = OfficerRole();
        var only = settings.SeedAccount(admin, employeeId: null, "Admin One", "admin1@example.test");
        settings.Host.User.UserId = 999;
        var update = settings.Handler<IUpdateHandler<UserUpdateRequest, UserDto>>();

        ResultAssert.Failed(
            await update.HandleAsync(only.Id, new UserUpdateRequest(officer.Id, SettingsHost.Version(only.RowVersion)), default),
            ErrorType.Conflict,
            "There must always be one active HR Administrator.");

        var second = settings.SeedAccount(admin, employeeId: null, "Admin Two", "admin2@example.test");
        ResultAssert.Succeeded(
            await update.HandleAsync(only.Id, new UserUpdateRequest(officer.Id, SettingsHost.Version(only.RowVersion)), default));
        Assert.Equal(officer.Id, only.RoleId);

        // Now the second one is the last.
        ResultAssert.Failed(
            await update.HandleAsync(second.Id, new UserUpdateRequest(officer.Id, SettingsHost.Version(second.RowVersion)), default),
            ErrorType.Conflict);
    }

    [Fact]
    public async Task An_administrator_account_without_a_login_does_not_count_as_one_who_can_sign_in()
    {
        var admin = settings.SeedRole(SettingsHost.AdministratorRole, builtIn: true);
        var officer = OfficerRole();
        var real = settings.SeedAccount(admin, employeeId: null, "Admin One", "admin1@example.test");
        settings.SeedAccount(admin, employeeId: null, "Unlinked", "admin2@example.test", linked: false);
        settings.Host.User.UserId = 999;

        var result = await settings.Handler<IUpdateHandler<UserUpdateRequest, UserDto>>()
            .HandleAsync(real.Id, new UserUpdateRequest(officer.Id, SettingsHost.Version(real.RowVersion)), default);

        ResultAssert.Failed(result, ErrorType.Conflict, "There must always be one active HR Administrator.");
    }

    // ---- Status commands ----

    private async Task<(UserDto Admin, UserDto Other)> TwoAdminsAsync()
    {
        var admin = settings.SeedRole(SettingsHost.AdministratorRole, builtIn: true);
        var first = settings.SeedAccount(admin, employeeId: null, "Admin One", "admin1@example.test");
        var second = settings.SeedAccount(admin, employeeId: null, "Admin Two", "admin2@example.test");
        settings.Host.User.UserId = first.Id;
        var get = settings.Handler<IGetByIdHandler<UserDto>>();
        return (ResultAssert.Succeeded(await get.HandleAsync(first.Id, default)), ResultAssert.Succeeded(await get.HandleAsync(second.Id, default)));
    }

    [Fact]
    public async Task An_administrator_cannot_deactivate_their_own_account()
    {
        var (me, _) = await TwoAdminsAsync();

        var result = await settings.Handler<ICommandHandler<DeactivateCommand, UserDto>>()
            .HandleAsync(new DeactivateCommand(me.Id, SettingsHost.Version(me.RowVersion)), default);

        ResultAssert.Failed(result, ErrorType.Conflict, "You cannot change your own role or deactivate your own account.");
    }

    [Fact]
    public async Task The_last_active_administrator_cannot_be_deactivated()
    {
        var (_, other) = await TwoAdminsAsync();
        var deactivate = settings.Handler<ICommandHandler<DeactivateCommand, UserDto>>();
        settings.Host.User.UserId = 999;

        var first = ResultAssert.Succeeded(await deactivate.HandleAsync(new DeactivateCommand(other.Id, SettingsHost.Version(other.RowVersion)), default));
        Assert.Equal("Inactive", first.Status);

        // Admin One is now the only active administrator.
        var remaining = settings.Host.Repository<AppAccount>().Items.Single(account => account.Status == AccountStatus.Active);
        ResultAssert.Failed(
            await deactivate.HandleAsync(new DeactivateCommand(remaining.Id, SettingsHost.Version(remaining.RowVersion)), default),
            ErrorType.Conflict,
            "There must always be one active HR Administrator.");
    }

    [Fact]
    public async Task Another_user_can_be_deactivated_and_activated_again()
    {
        var officer = OfficerRole();
        var employee = await settings.SeedEmployeeAsync();
        var user = ResultAssert.Succeeded(await settings.CreateUserAsync(employee.Id, officer.Id));

        var off = ResultAssert.Succeeded(await settings.Handler<ICommandHandler<DeactivateCommand, UserDto>>()
            .HandleAsync(new DeactivateCommand(user.Id, SettingsHost.Version(user.RowVersion)), default));
        var on = ResultAssert.Succeeded(await settings.Handler<ICommandHandler<ActivateCommand, UserDto>>()
            .HandleAsync(new ActivateCommand(user.Id, SettingsHost.Version(off.RowVersion)), default));

        Assert.Equal("Inactive", off.Status);
        Assert.Equal("Active", on.Status);
    }

    [Fact]
    public async Task A_locked_account_must_be_unlocked_before_it_is_activated_and_unlock_clears_the_lockout()
    {
        var officer = OfficerRole();
        var employee = await settings.SeedEmployeeAsync();
        var seeded = settings.SeedAccount(officer, employee.Id, "Locked Person", "locked@example.test", AccountStatus.Locked);
        var shown = ResultAssert.Succeeded(await settings.Handler<IGetByIdHandler<UserDto>>().HandleAsync(seeded.Id, default));
        Assert.Equal("Locked", shown.Status);

        ResultAssert.Failed(
            await settings.Handler<ICommandHandler<ActivateCommand, UserDto>>()
                .HandleAsync(new ActivateCommand(seeded.Id, SettingsHost.Version(shown.RowVersion)), default),
            ErrorType.Conflict,
            "This account is locked. Unlock it first.");

        var unlocked = ResultAssert.Succeeded(await settings.Handler<ICommandHandler<UnlockUserCommand, UserDto>>()
            .HandleAsync(new UnlockUserCommand(seeded.Id, SettingsHost.Version(shown.RowVersion)), default));

        Assert.Equal("Active", unlocked.Status);
        Assert.Equal([seeded.ExternalSubject!], settings.Logins.Unlocked);
    }

    [Fact]
    public async Task A_sign_in_lockout_shows_as_Locked_and_unlock_lifts_it()
    {
        var officer = OfficerRole();
        var employee = await settings.SeedEmployeeAsync();
        var user = ResultAssert.Succeeded(await settings.CreateUserAsync(employee.Id, officer.Id));
        var subject = settings.Host.Repository<AppAccount>().Items.Single().ExternalSubject!;
        settings.Logins.LockedOut.Add(subject);

        var locked = ResultAssert.Succeeded(await settings.Handler<IGetByIdHandler<UserDto>>().HandleAsync(user.Id, default));
        var page = ResultAssert.Succeeded(await settings.Handler<IQueryHandler<UserDto, UserFilter>>()
            .HandleAsync(new PageRequest<UserFilter>(0, 10, null, null, new UserFilter()), default));
        var unlocked = ResultAssert.Succeeded(await settings.Handler<ICommandHandler<UnlockUserCommand, UserDto>>()
            .HandleAsync(new UnlockUserCommand(user.Id, SettingsHost.Version(locked.RowVersion)), default));

        Assert.Equal("Locked", locked.Status);
        Assert.Equal("Locked", page.Items.Single().Status);
        Assert.Equal("Active", unlocked.Status);
        Assert.Empty(settings.Logins.LockedOut);
    }

    // ---- Delete ----

    [Fact]
    public async Task An_administrator_cannot_delete_their_own_account()
    {
        var (me, _) = await TwoAdminsAsync();

        var result = await settings.Handler<IDeleteHandler<UserDto>>().HandleAsync(me.Id, SettingsHost.Version(me.RowVersion), default);

        ResultAssert.Failed(result, ErrorType.Conflict, "You cannot delete your own account.");
    }

    [Fact]
    public async Task The_last_active_administrator_cannot_be_deleted()
    {
        var admin = settings.SeedRole(SettingsHost.AdministratorRole, builtIn: true);
        var only = settings.SeedAccount(admin, employeeId: null, "Admin One", "admin1@example.test");
        settings.Host.User.UserId = 999;

        var result = await settings.Handler<IDeleteHandler<UserDto>>().HandleAsync(only.Id, SettingsHost.Version(only.RowVersion), default);

        ResultAssert.Failed(result, ErrorType.Conflict, "There must always be one active HR Administrator.");
    }

    [Fact]
    public async Task Deleting_another_user_removes_the_account_and_its_login()
    {
        var officer = OfficerRole();
        var employee = await settings.SeedEmployeeAsync();
        var user = ResultAssert.Succeeded(await settings.CreateUserAsync(employee.Id, officer.Id));

        var result = await settings.Handler<IDeleteHandler<UserDto>>().HandleAsync(user.Id, SettingsHost.Version(user.RowVersion), default);

        Assert.True(result.IsSuccess);
        Assert.Empty(settings.Host.Repository<AppAccount>().Items);
        Assert.Equal(["sub-1"], settings.Logins.Deleted);
        Assert.Equal("User \"Mia Hartley\" deleted", settings.Host.ActivityLog.LastMessage);
    }

    [Fact]
    public async Task An_employee_who_has_a_user_account_cannot_be_deleted()
    {
        var officer = OfficerRole();
        var employee = await settings.SeedEmployeeAsync();
        ResultAssert.Succeeded(await settings.CreateUserAsync(employee.Id, officer.Id));

        var result = await settings.Handler<IDeleteHandler<EmployeeDto>>().HandleAsync(employee.Id, SettingsHost.Version(employee.RowVersion), default);

        var error = ResultAssert.Failed(result, ErrorType.Conflict);
        Assert.Contains("1 user account(s)", error.Message, StringComparison.Ordinal);
    }

    // ---- Password reset by an administrator ----

    [Fact]
    public async Task A_password_reset_without_email_configured_is_unavailable_and_sends_nothing()
    {
        var officer = OfficerRole();
        var employee = await settings.SeedEmployeeAsync();
        var user = ResultAssert.Succeeded(await settings.CreateUserAsync(employee.Id, officer.Id));
        settings.Email.Configured = false;

        var result = await settings.Handler<IUserPasswordResetHandler>().HandleAsync(user.Id, default);

        var error = ResultAssert.Failed(result, ErrorType.Unavailable);
        Assert.Contains("not configured", error.Message, StringComparison.Ordinal);
        Assert.Empty(settings.Logins.ResetMailsFor);
    }

    [Fact]
    public async Task A_password_reset_mails_the_user_and_records_a_note_without_the_address()
    {
        var officer = OfficerRole();
        var employee = await settings.SeedEmployeeAsync();
        var user = ResultAssert.Succeeded(await settings.CreateUserAsync(employee.Id, officer.Id));

        var result = await settings.Handler<IUserPasswordResetHandler>().HandleAsync(user.Id, default);

        Assert.True(result.IsSuccess);
        Assert.Single(settings.Logins.ResetMailsFor);
        Assert.Equal("Password reset email sent for user \"Mia Hartley\"", settings.Host.ActivityLog.LastMessage);
        Assert.DoesNotContain("@", settings.Host.ActivityLog.LastMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_password_reset_for_an_inactive_user_or_a_missing_one_is_refused()
    {
        var officer = OfficerRole();
        var employee = await settings.SeedEmployeeAsync();
        var user = ResultAssert.Succeeded(await settings.CreateUserAsync(employee.Id, officer.Id, status: "Inactive"));

        ResultAssert.Failed(await settings.Handler<IUserPasswordResetHandler>().HandleAsync(user.Id, default), ErrorType.Conflict);
        ResultAssert.Failed(await settings.Handler<IUserPasswordResetHandler>().HandleAsync(9999, default), ErrorType.NotFound);
        Assert.Empty(settings.Logins.ResetMailsFor);
    }


    [Fact]
    public async Task A_password_reset_mails_exactly_the_target_user_and_changes_nothing_else()
    {
        var officer = OfficerRole();
        var target = settings.SeedAccount(officer, employeeId: null, "Mia Hartley", "mia@example.test");
        var other = settings.SeedAccount(officer, employeeId: null, "Olive Officer", "olive@example.test");

        var result = await settings.Handler<IUserPasswordResetHandler>().HandleAsync(target.Id, default);

        Assert.True(result.IsSuccess);
        Assert.Equal(["seed-mia@example.test"], settings.Logins.ResetMailsFor);
        Assert.Empty(settings.Logins.SessionsInvalidated);
        Assert.Empty(settings.Logins.Unlocked);
        Assert.Equal(AccountStatus.Active, target.Status);
        Assert.Equal(AccountStatus.Active, other.Status);
    }

    [Fact]
    public async Task A_password_reset_for_a_locked_account_asks_to_activate_it_first()
    {
        var officer = OfficerRole();
        var locked = settings.SeedAccount(officer, employeeId: null, "Mia Hartley", "mia@example.test", AccountStatus.Locked);
        var inactive = settings.SeedAccount(officer, employeeId: null, "Olive Officer", "olive@example.test", AccountStatus.Inactive);

        ResultAssert.Failed(await settings.Handler<IUserPasswordResetHandler>().HandleAsync(locked.Id, default), ErrorType.Conflict, "Activate the account before sending a password reset.");
        ResultAssert.Failed(await settings.Handler<IUserPasswordResetHandler>().HandleAsync(inactive.Id, default), ErrorType.Conflict, "Activate the account before sending a password reset.");
        Assert.Empty(settings.Logins.ResetMailsFor);
    }

    [Fact]
    public async Task A_password_reset_for_the_account_of_a_leaver_is_a_conflict()
    {
        var officer = OfficerRole();
        var employeeId = await settings.MarkLeftAsync();
        var account = settings.SeedAccount(officer, employeeId, "Mia Hartley", "mia@example.test");

        var result = await settings.Handler<IUserPasswordResetHandler>().HandleAsync(account.Id, default);

        ResultAssert.Failed(result, ErrorType.Conflict, "This user's employee has left the company, so no password reset can be sent.");
        Assert.Empty(settings.Logins.ResetMailsFor);
    }

    [Fact]
    public async Task A_password_reset_for_an_account_without_a_login_is_a_conflict()
    {
        var officer = OfficerRole();
        var account = settings.SeedAccount(officer, employeeId: null, "Mia Hartley", "mia@example.test", linked: false);

        var result = await settings.Handler<IUserPasswordResetHandler>().HandleAsync(account.Id, default);

        ResultAssert.Failed(result, ErrorType.Conflict, "This account has no email address or sign-in to send a password reset to.");
        Assert.Empty(settings.Logins.ResetMailsFor);
    }

    [Fact]
    public async Task A_password_reset_for_an_account_with_an_inactive_role_is_a_conflict()
    {
        var role = settings.SeedRole("Retired Role", active: false);
        var account = settings.SeedAccount(role, employeeId: null, "Mia Hartley", "mia@example.test");

        var result = await settings.Handler<IUserPasswordResetHandler>().HandleAsync(account.Id, default);

        ResultAssert.Failed(result, ErrorType.Conflict, "The account's role is inactive. Activate the role before sending a password reset.");
    }
    [Fact]
    public async Task A_mail_server_failure_is_unavailable_and_does_not_leak_the_exception_text()
    {
        var officer = OfficerRole();
        var employee = await settings.SeedEmployeeAsync();
        var user = ResultAssert.Succeeded(await settings.CreateUserAsync(employee.Id, officer.Id));
        settings.Logins.SendFailure = new InvalidOperationException("smtp refused mia.hartley@example.test");

        var result = await settings.Handler<IUserPasswordResetHandler>().HandleAsync(user.Id, default);

        var error = ResultAssert.Failed(result, ErrorType.Unavailable);
        Assert.DoesNotContain("example.test", error.Message, StringComparison.Ordinal);
    }

    // ---- Reads ----

    [Fact]
    public async Task The_user_list_is_paged_searchable_and_filterable_by_role_and_status()
    {
        var officer = OfficerRole();
        var manager = settings.SeedRole("Department Manager");
        var first = await settings.SeedEmployeeAsync();
        var second = await settings.SeedEmployeeAsync();
        ResultAssert.Succeeded(await settings.CreateUserAsync(first.Id, officer.Id));
        ResultAssert.Succeeded(await settings.CreateUserAsync(second.Id, manager.Id, status: "Inactive"));
        var query = settings.Handler<IQueryHandler<UserDto, UserFilter>>();

        var byRole = ResultAssert.Succeeded(await query.HandleAsync(new PageRequest<UserFilter>(0, 10, null, null, new UserFilter(RoleId: manager.Id)), default));
        var byStatus = ResultAssert.Succeeded(await query.HandleAsync(new PageRequest<UserFilter>(0, 10, null, null, new UserFilter(Status: "Active")), default));
        var bySearch = ResultAssert.Succeeded(await query.HandleAsync(new PageRequest<UserFilter>(0, 10, null, "person1", new UserFilter()), default));
        var badSort = await query.HandleAsync(new PageRequest<UserFilter>(0, 10, new SortState("password", "asc"), null, new UserFilter()), default);

        Assert.Single(byRole.Items);
        Assert.Equal("Inactive", byRole.Items[0].Status);
        Assert.Single(byStatus.Items);
        Assert.Single(bySearch.Items);
        ResultAssert.FailedOnField(badSort, "sort");
    }
}
