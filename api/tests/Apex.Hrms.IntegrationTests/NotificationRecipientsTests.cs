using Apex.Hrms.Application.Notifications;
using Apex.Hrms.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.IntegrationTests;

/// <summary>
/// Who a notification goes to, against SQL Server: the account of an employee, and the accounts holding a grant, only when they
/// can sign in, without the excluded employees. HR Administrator holds every grant; the Employee role never holds kpi-setup.
/// Fictional data only; every row carries the tag of its test.
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
[Trait("Category", "Integration")]
public sealed class NotificationRecipientsTests : DatabaseTest
{
    private Task<T> Recipients<T>(Func<INotificationRecipients, Task<T>> action) =>
        InScopeAsync(sp => action(sp.GetRequiredService<INotificationRecipients>()));

    [IntegrationFact]
    public async Task Holders_are_the_active_accounts_whose_role_has_the_grant_minus_the_excluded_employees()
    {
        var org = await SeedOrganizationAsync();
        var admin = await SeedEmployeeAsync(org, "1", "mia");
        var plain = await SeedEmployeeAsync(org, "2", "noah");
        var inactive = await SeedEmployeeAsync(org, "3", "ava");
        var excluded = await SeedEmployeeAsync(org, "4", "liam");
        var adminAccount = await TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, "admin"), employeeId: admin.Id);
        var plainAccount = await TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, "plain"), roleName: "Employee", employeeId: plain.Id);
        var inactiveAccount = await TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, "inactive"), status: "Inactive", employeeId: inactive.Id);
        var excludedAccount = await TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, "excluded"), employeeId: excluded.Id);

        var holders = await Recipients(store => store.UserIdsHoldingAsync("kpi-setup", "Delete", [excluded.Id], default));

        Assert.Contains(adminAccount.AccountId, holders);
        Assert.DoesNotContain(plainAccount.AccountId, holders);
        Assert.DoesNotContain(inactiveAccount.AccountId, holders);
        Assert.DoesNotContain(excludedAccount.AccountId, holders);
        Assert.Equal(holders.Distinct().Count(), holders.Count);
    }

    [IntegrationFact]
    public async Task The_account_of_an_employee_is_found_only_when_it_can_sign_in()
    {
        var org = await SeedOrganizationAsync();
        var active = await SeedEmployeeAsync(org, "1", "mia");
        var inactive = await SeedEmployeeAsync(org, "2", "noah");
        var without = await SeedEmployeeAsync(org, "3", "ava");
        var activeAccount = await TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, "active"), roleName: "Employee", employeeId: active.Id);
        await TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, "inactive"), roleName: "Employee", status: "Inactive", employeeId: inactive.Id);

        Assert.Equal(activeAccount.AccountId, await Recipients(store => store.UserIdOfEmployeeAsync(active.Id, default)));
        Assert.Null(await Recipients(store => store.UserIdOfEmployeeAsync(inactive.Id, default)));
        Assert.Null(await Recipients(store => store.UserIdOfEmployeeAsync(without.Id, default)));
    }
}
