using Apex.Hrms.Application.Settings;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Security;
using Apex.Hrms.Domain.Settings;

namespace Apex.Hrms.Application.Tests.Settings;

/// <summary>Roles, the permission matrix and the general settings, through the real handlers and validators.</summary>
public sealed class RoleAndSettingsHandlerTests : IDisposable
{
    private readonly SettingsHost settings = new();

    public void Dispose() => settings.Dispose();

    private async Task<RoleDto> GetRoleAsync(int id) =>
        ResultAssert.Succeeded(await settings.Handler<IGetByIdHandler<RoleDto>>().HandleAsync(id, default));

    private async Task<RoleDto> CreateRoleAsync(string name = "Trainer", string? description = "Runs training", string? status = null) =>
        ResultAssert.Succeeded(await settings.Handler<ICreateHandler<RoleCreateRequest, RoleDto>>()
            .HandleAsync(new RoleCreateRequest(name, description, status), default));

    private Task<Result<RoleDto>> UpdateRoleAsync(RoleDto role, string name, string? description = null) =>
        settings.Handler<IUpdateHandler<RoleUpdateRequest, RoleDto>>()
            .HandleAsync(role.Id, new RoleUpdateRequest(name, description ?? role.Description, SettingsHost.Version(role.RowVersion)), default);

    [Fact]
    public async Task Saving_the_grants_of_a_role_empties_the_cached_grants_of_that_role_only()
    {
        var (_, officer) = await AdminAndOfficerAsync();
        var cache = settings.Host.Get<Application.Security.RoleGrantCache>();
        var loads = 0;
        Task<IReadOnlySet<string>> Load(CancellationToken _)
        {
            loads++;
            return Task.FromResult<IReadOnlySet<string>>(new HashSet<string> { "dashboard:View" });
        }

        await cache.GetAsync(officer.Id, [], Load, default);
        await cache.GetAsync(officer.Id + 1000, [], Load, default);
        await cache.GetAsync(officer.Id, [], Load, default);
        Assert.Equal(2, loads);

        ResultAssert.Succeeded(await SaveGrantsAsync(officer, new() { ["dashboard"] = ["View"] }));

        await cache.GetAsync(officer.Id, [], Load, default);
        await cache.GetAsync(officer.Id + 1000, [], Load, default);
        Assert.Equal(3, loads);
    }

    // ---- Roles ----

    [Fact]
    public async Task A_role_is_created_as_a_custom_active_role_and_the_name_is_trimmed()
    {
        var role = await CreateRoleAsync("  Trainer  ");

        Assert.Equal("Trainer", role.Name);
        Assert.Equal("Runs training", role.Description);
        Assert.False(role.IsSystem);
        Assert.Equal("Active", role.Status);
        Assert.Equal("Role \"Trainer\" created", settings.Host.ActivityLog.LastMessage);
    }

    [Fact]
    public async Task Role_names_are_required_short_and_unique()
    {
        await CreateRoleAsync("Trainer");
        var create = settings.Handler<ICreateHandler<RoleCreateRequest, RoleDto>>();

        ResultAssert.FailedOnField(await create.HandleAsync(new RoleCreateRequest(" ", null, null), default), "name");
        ResultAssert.FailedOnField(await create.HandleAsync(new RoleCreateRequest(new string('x', 61), null, null), default), "name");
        ResultAssert.FailedOnField(await create.HandleAsync(new RoleCreateRequest("Trainer", null, null), default), "name", "Role name \"Trainer\" already exists.");
        ResultAssert.FailedOnField(await create.HandleAsync(new RoleCreateRequest("Other", null, "Locked"), default), "status");
    }

    [Fact]
    public async Task A_custom_role_can_be_renamed_and_its_description_changed()
    {
        var role = await CreateRoleAsync("Trainer");

        var updated = ResultAssert.Succeeded(await UpdateRoleAsync(role, "Lead Trainer", "Leads training"));

        Assert.Equal("Lead Trainer", updated.Name);
        Assert.Equal("Leads training", updated.Description);
    }

    [Fact]
    public async Task A_built_in_role_cannot_be_renamed_but_its_description_can_change()
    {
        var admin = settings.SeedRole(SettingsHost.AdministratorRole, builtIn: true);
        var dto = await GetRoleAsync(admin.Id);

        var renamed = await UpdateRoleAsync(dto, "Super Admin");
        var described = ResultAssert.Succeeded(await UpdateRoleAsync(dto, SettingsHost.AdministratorRole, "Everything"));

        var error = ResultAssert.FailedOnField(renamed, "name", "A built-in role cannot be renamed.");
        Assert.Equal("Built-in roles keep their name.", error.FieldErrors!["name"][0]);
        Assert.Equal("Everything", described.Description);
    }

    [Fact]
    public async Task A_built_in_role_cannot_be_deactivated_or_deleted()
    {
        var employee = settings.SeedRole("Employee", builtIn: true);
        var dto = await GetRoleAsync(employee.Id);

        var deactivated = await settings.Handler<ICommandHandler<DeactivateCommand, RoleDto>>()
            .HandleAsync(new DeactivateCommand(dto.Id, SettingsHost.Version(dto.RowVersion)), default);
        var deleted = await settings.Handler<IDeleteHandler<RoleDto>>().HandleAsync(dto.Id, SettingsHost.Version(dto.RowVersion), default);

        ResultAssert.Failed(deactivated, ErrorType.Conflict, "A built-in role cannot be deactivated.");
        ResultAssert.Failed(deleted, ErrorType.Conflict, "A built-in role cannot be deleted.");
    }

    [Fact]
    public async Task A_role_with_active_users_cannot_be_deactivated_and_one_without_can_be_activated_again()
    {
        var role = await CreateRoleAsync("Trainer");
        var seeded = settings.Host.Repository<AppRole>().Items.Single();
        settings.SeedAccount(seeded, employeeId: null, "Active Person", "active@example.test");
        settings.SeedAccount(seeded, employeeId: null, "Active Two", "active2@example.test");
        settings.SeedAccount(seeded, employeeId: null, "Inactive Person", "inactive@example.test", AccountStatus.Inactive);
        var deactivate = settings.Handler<ICommandHandler<DeactivateCommand, RoleDto>>();

        var refused = await deactivate.HandleAsync(new DeactivateCommand(role.Id, SettingsHost.Version(role.RowVersion)), default);

        ResultAssert.Failed(refused, ErrorType.Conflict, "2 active user(s) still have this role. Move them to another role first.");

        var empty = await CreateRoleAsync("Empty Role");
        var off = ResultAssert.Succeeded(await deactivate.HandleAsync(new DeactivateCommand(empty.Id, SettingsHost.Version(empty.RowVersion)), default));
        var on = ResultAssert.Succeeded(await settings.Handler<ICommandHandler<ActivateCommand, RoleDto>>()
            .HandleAsync(new ActivateCommand(empty.Id, SettingsHost.Version(off.RowVersion)), default));
        Assert.Equal("Inactive", off.Status);
        Assert.Equal("Active", on.Status);
    }

    [Fact]
    public async Task A_role_that_users_hold_cannot_be_deleted_and_the_message_names_the_users()
    {
        var role = await CreateRoleAsync("Trainer");
        var seeded = settings.Host.Repository<AppRole>().Items.Single();
        settings.SeedAccount(seeded, employeeId: null, "Inactive Person", "inactive@example.test", AccountStatus.Inactive);

        var result = await settings.Handler<IDeleteHandler<RoleDto>>().HandleAsync(role.Id, SettingsHost.Version(role.RowVersion), default);

        ResultAssert.Failed(result, ErrorType.Conflict, "\"Trainer\" cannot be deleted because it is used by 1 user(s). Deactivate it instead.");
    }

    [Fact]
    public async Task A_custom_role_nobody_holds_can_be_deleted()
    {
        var role = await CreateRoleAsync("Trainer");

        var result = await settings.Handler<IDeleteHandler<RoleDto>>().HandleAsync(role.Id, SettingsHost.Version(role.RowVersion), default);

        Assert.True(result.IsSuccess);
        Assert.Empty(settings.Host.Repository<AppRole>().Items);
        Assert.Equal("Role \"Trainer\" deleted", settings.Host.ActivityLog.LastMessage);
    }

    // ---- Permission matrix ----

    private async Task<(RoleDto Admin, RoleDto Officer)> AdminAndOfficerAsync()
    {
        var admin = settings.SeedRole(SettingsHost.AdministratorRole, builtIn: true);
        var officer = settings.SeedRole("HR Officer");
        return (await GetRoleAsync(admin.Id), await GetRoleAsync(officer.Id));
    }

    private Task<Result<RolePermissionsDto>> SaveGrantsAsync(RoleDto role, Dictionary<string, string[]?> grants, string? rowVersion = null) =>
        settings.Handler<IPermissionMatrixHandler>()
            .SaveAsync(role.Id, new RolePermissionsUpdateRequest(grants, rowVersion ?? SettingsHost.Version(role.RowVersion)), default);

    [Fact]
    public async Task The_matrix_lists_the_modules_with_their_actions_and_the_grants_of_every_role()
    {
        var (admin, officer) = await AdminAndOfficerAsync();
        ResultAssert.Succeeded(await SaveGrantsAsync(officer, new() { ["leave"] = ["View", "Create"] }));

        var matrix = ResultAssert.Succeeded(await settings.Handler<IPermissionMatrixHandler>().GetMatrixAsync(default));

        Assert.Equal(["dashboard", "employees", "leave", "settings"], matrix.Modules.Select(module => module.Key));
        Assert.Equal("View", matrix.Modules.Single(module => module.Key == "leave").Actions[0]);
        Assert.Equal([admin.Id, officer.Id], matrix.Roles.Select(role => role.RoleId));
        Assert.Equal(["View", "Create"], matrix.Roles.Single(role => role.RoleId == officer.Id).Grants["leave"]);
    }

    [Fact]
    public async Task Saving_grants_replaces_them_returns_a_new_row_version_and_records_the_change()
    {
        var (_, officer) = await AdminAndOfficerAsync();

        var saved = ResultAssert.Succeeded(await SaveGrantsAsync(officer, new()
        {
            ["employees"] = ["Edit", "View", "View"],
            ["leave"] = ["View"],
            ["dashboard"] = [],
        }));

        Assert.Equal(["View", "Edit"], saved.Grants["employees"]);
        Assert.Equal(["View"], saved.Grants["leave"]);
        Assert.DoesNotContain("dashboard", saved.Grants.Keys);
        Assert.NotEqual(officer.RowVersion, saved.RowVersion);
        Assert.Equal("Permissions of role \"HR Officer\" updated", settings.Host.ActivityLog.LastMessage);
        Assert.Equal(3, settings.Permissions.Grants.Count);
    }

    [Fact]
    public async Task A_grant_other_than_View_needs_View_of_the_same_module()
    {
        var (_, officer) = await AdminAndOfficerAsync();

        var result = await SaveGrantsAsync(officer, new() { ["leave"] = ["Create", "Edit"] });

        var error = ResultAssert.FailedOnField(result, "leave", "Leave: View is required to grant other permissions.");
        Assert.Equal("View is required.", error.FieldErrors!["leave"][0]);
        Assert.Empty(settings.Permissions.Grants);
    }

    [Fact]
    public async Task Unknown_modules_and_actions_that_do_not_exist_for_a_module_are_refused()
    {
        var (_, officer) = await AdminAndOfficerAsync();

        ResultAssert.FailedOnField(await SaveGrantsAsync(officer, new() { ["payroll"] = ["View"] }), "grants", "Unknown module \"payroll\".");
        ResultAssert.FailedOnField(
            await SaveGrantsAsync(officer, new() { ["dashboard"] = ["View", "Delete"] }),
            "dashboard",
            "Dashboard has no \"Delete\" permission.");
        ResultAssert.FailedOnField(await settings.Handler<IPermissionMatrixHandler>().SaveAsync(officer.Id, new RolePermissionsUpdateRequest(null, SettingsHost.Version(officer.RowVersion)), default), "grants");
    }

    [Fact]
    public async Task The_HR_Administrator_always_keeps_every_grant()
    {
        var (admin, _) = await AdminAndOfficerAsync();
        var full = FakePermissionStore.Modules.ToDictionary(module => module.Key, module => (string[]?)[.. module.Actions]);
        var saved = ResultAssert.Succeeded(await SaveGrantsAsync(admin, full));

        // Removing one single action, or a whole module, is refused.
        var withoutOne = full.ToDictionary(pair => pair.Key, pair => pair.Value);
        withoutOne["leave"] = ["View", "Create", "Edit", "Delete"];
        var withoutModule = full.Where(pair => pair.Key != "settings").ToDictionary(pair => pair.Key, pair => pair.Value);
        var currentVersion = SettingsHost.Version(saved.RowVersion);

        var one = await SaveGrantsAsync(admin, withoutOne, currentVersion);
        var module = await SaveGrantsAsync(admin, withoutModule, currentVersion);

        ResultAssert.Failed(one, ErrorType.Conflict, "HR Administrator always has full access and cannot be changed.");
        ResultAssert.Failed(module, ErrorType.Conflict);
        Assert.Equal(FakePermissionStore.Modules.Sum(m => m.Actions.Count), settings.Permissions.Grants.Count(grant => grant.RoleId == admin.Id));
    }

    [Fact]
    public async Task Saving_grants_needs_the_current_row_version_and_an_existing_role()
    {
        var (_, officer) = await AdminAndOfficerAsync();
        var first = ResultAssert.Succeeded(await SaveGrantsAsync(officer, new() { ["leave"] = ["View"] }));

        ResultAssert.Failed(await SaveGrantsAsync(officer, new() { ["leave"] = ["View", "Edit"] }), ErrorType.Conflict);
        ResultAssert.FailedOnField(await SaveGrantsAsync(officer, new() { ["leave"] = ["View"] }, rowVersion: ""), "rowVersion");
        ResultAssert.Succeeded(await SaveGrantsAsync(officer, new() { ["leave"] = ["View", "Edit"] }, SettingsHost.Version(first.RowVersion)));
        ResultAssert.Failed(await settings.Handler<IPermissionMatrixHandler>().GetForRoleAsync(9999, default), ErrorType.NotFound);
    }

    [Fact]
    public async Task A_role_without_grants_returns_an_empty_grant_set()
    {
        var (_, officer) = await AdminAndOfficerAsync();

        var perRole = ResultAssert.Succeeded(await settings.Handler<IPermissionMatrixHandler>().GetForRoleAsync(officer.Id, default));

        Assert.Equal(officer.Id, perRole.Id);
        Assert.Empty(perRole.Grants);
    }

    // ---- General settings ----

    private Task<Result<GeneralSettingsDto>> SaveSettingsAsync(GeneralSettingsDto current, Func<GeneralSettingsUpdateRequest, GeneralSettingsUpdateRequest>? change = null)
    {
        var request = new GeneralSettingsUpdateRequest(
            current.OrganizationName,
            current.ContactEmail,
            current.Timezone,
            current.DateFormat,
            current.CurrencyCode,
            current.FiscalYearStartMonth,
            current.EmailNotifications,
            current.WeeklyDigest,
            SettingsHost.Version(current.RowVersion));
        return settings.Handler<IGeneralSettingsHandler>().SaveAsync(change is null ? request : change(request), default);
    }

    private async Task<GeneralSettingsDto> CurrentSettingsAsync()
    {
        settings.SeedGeneralSettings();
        return ResultAssert.Succeeded(await settings.Handler<IGeneralSettingsHandler>().GetAsync(default));
    }

    [Fact]
    public async Task General_settings_are_read_and_saved_with_a_new_row_version_and_an_activity_entry()
    {
        var current = await CurrentSettingsAsync();

        var saved = ResultAssert.Succeeded(await SaveSettingsAsync(current, request => request with
        {
            OrganizationName = "  Apex Test Org  ",
            Timezone = "UTC",
            DateFormat = "dd/MM/yyyy",
            CurrencyCode = "USD",
            FiscalYearStartMonth = 4,
            WeeklyDigest = true,
        }));

        Assert.Equal("Apex Test Org", saved.OrganizationName);
        Assert.Equal("UTC", saved.Timezone);
        Assert.Equal(4, saved.FiscalYearStartMonth);
        Assert.True(saved.WeeklyDigest);
        Assert.NotEqual(current.RowVersion, saved.RowVersion);
        Assert.Equal("General settings updated", settings.Host.ActivityLog.LastMessage);
    }

    [Theory]
    [InlineData("organizationName", " ", "Organization name is required.")]
    [InlineData("contactEmail", "not-an-email", "Enter a valid email address.")]
    [InlineData("contactEmail", "a b@example.test", "Enter a valid email address.")]
    [InlineData("contactEmail", "a@example.test\n", "Enter a valid email address.")]
    [InlineData("timezone", "Mars/Base", "Choose a listed time zone.")]
    [InlineData("dateFormat", "d/m/y", "Choose a listed format.")]
    [InlineData("currencyCode", "mmk", "Use a 3-letter code such as MMK.")]
    [InlineData("currencyCode", "MMKK", "Use a 3-letter code such as MMK.")]
    [InlineData("fiscalYearStartMonth", "0", "Choose a month.")]
    [InlineData("fiscalYearStartMonth", "13", "Choose a month.")]
    public async Task General_settings_validation_ports_the_mock_rules_and_messages(string field, string value, string message)
    {
        var current = await CurrentSettingsAsync();

        var result = await SaveSettingsAsync(current, request => field switch
        {
            "organizationName" => request with { OrganizationName = value },
            "contactEmail" => request with { ContactEmail = value },
            "timezone" => request with { Timezone = value },
            "dateFormat" => request with { DateFormat = value },
            "currencyCode" => request with { CurrencyCode = value },
            _ => request with { FiscalYearStartMonth = int.Parse(value, System.Globalization.CultureInfo.InvariantCulture) },
        });

        var error = ResultAssert.FailedOnField(result, field);
        Assert.Equal(message, error.FieldErrors![field][0]);
        Assert.Equal("Please fix the highlighted settings.", error.Message);
    }

    [Fact]
    public async Task Every_listed_time_zone_and_date_format_is_accepted()
    {
        var current = await CurrentSettingsAsync();

        foreach (var zone in GeneralSettings.Timezones)
        {
            current = ResultAssert.Succeeded(await SaveSettingsAsync(current, request => request with { Timezone = zone }));
        }

        foreach (var format in GeneralSettings.DateFormats)
        {
            current = ResultAssert.Succeeded(await SaveSettingsAsync(current, request => request with { DateFormat = format }));
        }

        Assert.Equal("dd MMM yyyy", current.DateFormat);
    }

    [Fact]
    public async Task General_settings_need_the_current_row_version()
    {
        var current = await CurrentSettingsAsync();
        ResultAssert.Succeeded(await SaveSettingsAsync(current));

        var stale = await SaveSettingsAsync(current);
        var missing = await SaveSettingsAsync(current, request => request with { RowVersion = null });

        ResultAssert.Failed(stale, ErrorType.Conflict);
        ResultAssert.FailedOnField(missing, "rowVersion");
    }

    [Fact]
    public async Task General_settings_that_were_never_created_are_not_found()
    {
        var result = await settings.Handler<IGeneralSettingsHandler>().GetAsync(default);

        ResultAssert.Failed(result, ErrorType.NotFound);
    }
}
