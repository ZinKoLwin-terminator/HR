using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Security;
using Apex.Hrms.Domain.Security;
using Apex.Hrms.TestSupport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.Api.Tests;

/// <summary>
/// The administrator-only rule of the settings endpoints and the error contract of the user endpoints, without a database:
/// the account directory and the email sender are replaced by fakes, and every request that reaches a handler is rejected by
/// the handler before it would query anything.
/// </summary>
public sealed class SettingsAccessTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public SettingsAccessTests(WebApplicationFactory<Program> factory) => this.factory = factory;

    private HttpClient ClientFor(string roleName, AccountStatus status = AccountStatus.Active, bool roleActive = true, IEmailSender? email = null)
    {
        // Every role holds every grant here, so only the administrator requirement can refuse the caller.
        var account = AccessFakes.Account(roleName, status, roleActive);
        var host = SignedInHost.Customize(factory, account, AccessFakes.AllGrants).WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            if (email is not null)
            {
                services.AddSingleton(email);
            }
        }));
        return BrowserHandler.CreateClient(host.Server.CreateHandler(), out _);
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>())!;

    [Theory]
    [InlineData("GET", "/api/users/query")]
    [InlineData("GET", "/api/users/1")]
    [InlineData("POST", "/api/users")]
    [InlineData("PUT", "/api/users/1")]
    [InlineData("DELETE", "/api/users/1")]
    [InlineData("POST", "/api/users/1/activate")]
    [InlineData("POST", "/api/users/1/deactivate")]
    [InlineData("POST", "/api/users/1/unlock")]
    [InlineData("POST", "/api/users/1/password-reset")]
    [InlineData("GET", "/api/roles")]
    [InlineData("GET", "/api/roles/query")]
    [InlineData("POST", "/api/roles")]
    [InlineData("PUT", "/api/roles/1")]
    [InlineData("DELETE", "/api/roles/1")]
    [InlineData("POST", "/api/roles/1/activate")]
    [InlineData("POST", "/api/roles/1/deactivate")]
    [InlineData("GET", "/api/role-permissions")]
    [InlineData("GET", "/api/role-permissions/1")]
    [InlineData("PUT", "/api/role-permissions/1")]
    [InlineData("PUT", "/api/general-settings")]
    public async Task A_caller_who_is_not_an_administrator_gets_403_from_every_write_and_every_settings_read(string method, string url)
    {
        using var client = ClientFor("HR Officer");

        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url) { Content = JsonContent.Create(new { }) });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(AccountStatus.Inactive, true)]
    [InlineData(AccountStatus.Locked, true)]
    [InlineData(AccountStatus.Active, false)]
    public async Task An_administrator_account_that_cannot_sign_in_is_refused_too(AccountStatus status, bool roleActive)
    {
        using var client = ClientFor("HR Administrator", status, roleActive);

        var response = await client.PostAsJsonAsync("/api/users", new { employeeId = 1, roleId = 1, initialPassword = "twelve-chars-ok" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_administrator_passes_the_check_and_reaches_the_handler_which_applies_the_password_policy()
    {
        using var client = ClientFor("HR Administrator");

        var response = await client.PostAsJsonAsync("/api/users", new { employeeId = 1, roleId = 1, initialPassword = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal("Validation", body.GetProperty("code").GetString());
        Assert.Equal("Use at least 12 characters.", body.GetProperty("errors").GetProperty("initialPassword")[0].GetString());
        Assert.DoesNotContain("short", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Creating_a_user_without_a_body_or_with_an_unknown_status_is_a_validation_problem()
    {
        using var client = ClientFor("HR Administrator");

        var response = await client.PostAsJsonAsync("/api/users", new { employeeId = 0, roleId = 0, status = "Locked", initialPassword = "twelve-chars-ok" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await ReadAsync(response)).GetProperty("errors");
        Assert.Equal("Choose a current employee.", errors.GetProperty("employeeId")[0].GetString());
        Assert.Equal("Choose an active role.", errors.GetProperty("roleId")[0].GetString());
        Assert.Equal("Status must be Active or Inactive.", errors.GetProperty("status")[0].GetString());
    }

    [Fact]
    public async Task An_administrator_reset_without_email_configured_is_503_with_code_Unknown()
    {
        using var client = ClientFor("HR Administrator", email: new UnconfiguredEmail());

        var response = await client.PostAsync("/api/users/1/password-reset", content: null);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal("Unknown", body.GetProperty("code").GetString());
        Assert.Contains("not configured", body.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task General_settings_with_bad_values_return_the_mock_field_errors()
    {
        using var client = ClientFor("HR Administrator");

        var response = await client.PutAsJsonAsync(
            "/api/general-settings",
            new
            {
                organizationName = " ",
                contactEmail = "nope",
                timezone = "Mars/Base",
                dateFormat = "d/m/y",
                currencyCode = "mmk",
                fiscalYearStartMonth = 13,
                emailNotifications = true,
                weeklyDigest = false,
                rowVersion = "AAAAAAAAB9E=",
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal("Please fix the highlighted settings.", body.GetProperty("detail").GetString());
        var errors = body.GetProperty("errors");
        Assert.Equal("Organization name is required.", errors.GetProperty("organizationName")[0].GetString());
        Assert.Equal("Enter a valid email address.", errors.GetProperty("contactEmail")[0].GetString());
        Assert.Equal("Choose a listed time zone.", errors.GetProperty("timezone")[0].GetString());
        Assert.Equal("Choose a listed format.", errors.GetProperty("dateFormat")[0].GetString());
        Assert.Equal("Use a 3-letter code such as MMK.", errors.GetProperty("currencyCode")[0].GetString());
        Assert.Equal("Choose a month.", errors.GetProperty("fiscalYearStartMonth")[0].GetString());
    }

    private sealed class UnconfiguredEmail : IEmailSender
    {
        public void EnsureAvailable() => throw new EmailNotConfiguredException();

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken) => throw new EmailNotConfiguredException();
    }
}
