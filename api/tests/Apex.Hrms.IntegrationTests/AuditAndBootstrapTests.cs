using System.Net;
using System.Net.Http.Json;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Organization.Companies;
using Apex.Hrms.Application.Security;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Infrastructure;
using Apex.Hrms.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.IntegrationTests;

/// <summary>
/// The audit rule at the handler level (no request, a fake caller) and the bootstrap command's data path against the
/// real database.
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
[Trait("Category", "Integration")]
public sealed class AuditAndBootstrapTests : DatabaseTest
{
    [IntegrationFact]
    public async Task A_write_by_the_fake_signed_in_user_is_stamped_with_that_users_id()
    {
        var company = await SeedCompanyAsync();

        var stamped = await Sql.ScalarAsync<int>("SELECT CreatedByUserId FROM org.Company WHERE Id = @id", ("@id", company.Id));
        Assert.Equal(TestAdministrator.AccountId, stamped);
    }

    [IntegrationFact]
    public async Task A_write_without_a_signed_in_user_is_an_unauthorized_result_and_writes_nothing()
    {
        Customize(services => services.AddScoped<ICurrentUser>(_ => new TestCurrentUser(null, "anonymous")));

        var result = await Create(Requests.Company());

        Failed(result, ErrorType.Unauthorized);
        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM org.Company WHERE Code = @code", ("@code", Code("A"))));
    }

    [IntegrationFact]
    public async Task The_system_identity_may_write_without_a_user_and_the_user_columns_stay_null()
    {
        Customize(services => services.UseSystemIdentity());

        var company = Succeeded(await Create(Requests.Company()));

        Assert.Equal(1, await Sql.ScalarAsync<int>(
            "SELECT COUNT(*) FROM org.Company WHERE Id = @id AND CreatedByUserId IS NULL AND ModifiedByUserId IS NULL", ("@id", company.Id)));
    }

    [IntegrationFact]
    public async Task Bootstrap_refuses_while_an_active_linked_administrator_exists()
    {
        // The fixture's administrator is one.
        Customize(services => services.UseSystemIdentity());

        var result = await Bootstrap(TestAccounts.EmailFor(Tag, "boot-refused"), TestAccounts.NewPassword());

        Assert.Equal(AdministratorBootstrap.AlreadyExistsMessage, Failed(result, ErrorType.Conflict).Message);
        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM auth.[User] WHERE Email = @email", ("@email", TestAccounts.EmailFor(Tag, "boot-refused"))));
    }

    [IntegrationFact]
    public async Task Bootstrap_creates_the_identity_user_and_the_administrator_account_in_one_go_and_the_password_signs_in()
    {
        Customize(services => services.UseSystemIdentity());
        var email = TestAccounts.EmailFor(Tag, "boot");
        var password = TestAccounts.NewPassword();

        // The test database must hold no other active linked administrator, or the command would rightly refuse.
        var others = await Sql.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM sec.AppUser a JOIN sec.Role r ON r.Id = a.RoleId
            WHERE r.Name = @role AND a.Status = 'Active' AND a.ExternalSubject IS NOT NULL AND a.Id <> @admin
            """,
            ("@role", SecurityPolicy.AdministratorRoleName),
            ("@admin", TestAdministrator.AccountId));
        Assert.True(others == 0, $"HR_IntegrationTests holds {others} other active HR Administrator account(s); remove them from that throwaway database.");

        // Take the fixture's administrator out of the way for the duration of the command only.
        await Sql.ExecuteAsync("UPDATE sec.AppUser SET Status = 'Inactive' WHERE Id = @id", ("@id", TestAdministrator.AccountId));
        Result result;
        try
        {
            result = await Bootstrap(email, password);
        }
        finally
        {
            await Sql.ExecuteAsync("UPDATE sec.AppUser SET Status = 'Active' WHERE Id = @id", ("@id", TestAdministrator.AccountId));
        }

        Assert.True(result.IsSuccess, result.IsFailure ? $"{result.Error.Type}: {result.Error.Message}" : null);

        var hash = await Sql.ScalarAsync<string>("SELECT PasswordHash FROM auth.[User] WHERE Email = @email", ("@email", email));
        Assert.False(string.IsNullOrEmpty(hash));
        Assert.DoesNotContain(password, hash, StringComparison.Ordinal);
        Assert.Equal(1, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM auth.[User] WHERE Email = @email AND LockoutEnabled = 1 AND AccessFailedCount = 0", ("@email", email)));
        Assert.Equal(1, await Sql.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM sec.AppUser a
            JOIN sec.Role r ON r.Id = a.RoleId
            JOIN auth.[User] u ON u.Id = a.ExternalSubject
            WHERE a.Email = @email AND a.EmployeeId IS NULL AND a.Status = 'Active' AND r.Name = @role
              AND a.CreatedByUserId IS NULL AND u.Email = @email
            """,
            ("@email", email),
            ("@role", SecurityPolicy.AdministratorRoleName)));

        // The bootstrapped password works through the real sign-in, and a second run refuses.
        using var factory = new ApiFactory();
        using var client = factory.CreateBrowserClient(out _);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password, rememberMe = false });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Failed(await Bootstrap(TestAccounts.EmailFor(Tag, "boot-second"), TestAccounts.NewPassword()), ErrorType.Conflict);
    }

    [IntegrationFact]
    public async Task Bootstrap_rejects_a_password_shorter_than_twelve_characters()
    {
        Customize(services => services.UseSystemIdentity());

        var result = await Bootstrap(TestAccounts.EmailFor(Tag, "boot-short"), "short");

        Assert.True(Failed(result, ErrorType.Validation).FieldErrors!.ContainsKey("password"));
    }

    private Task<Result> Bootstrap(string email, string password) =>
        InScopeAsync(services => services.GetRequiredService<AdministratorBootstrap>().RunAsync(email, "TST- Bootstrapped", password, default));
}
