using System.Net;
using System.Net.Http.Json;
using System.Text;
using Apex.Hrms.Domain.Security;
using Apex.Hrms.Mobile.Application.Abstractions;
using Apex.Hrms.Mobile.Application.Auth;
using Apex.Hrms.MobileApi.Tests.Support;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Apex.Hrms.MobileApi.Tests;

/// <summary>POST /api/v1/auth/login: the contract of hr_employee_app/docs/api_contracts.md §2 and the refusal rules.</summary>
public sealed class MobileLoginTests
{
    private const string LoginPath = "/api/v1/auth/login";

    [Fact]
    public async Task An_employee_signs_in_with_the_employee_code_and_gets_both_tokens_and_the_user_in_the_envelope()
    {
        using var host = new MobileHost();
        using var client = host.CreateClient();

        using var response = await client.PostAsJsonAsync(LoginPath, new
        {
            userName = " tst-00421 ",
            password = TestPeople.Password,
            deviceId = "device-a",
            deviceName = "Pixel 8 / Android 15",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var body = await MobileHost.Json(response);
        Assert.True(body.GetProperty("success").GetBoolean());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));

        var data = body.GetProperty("data");
        Assert.Equal("Bearer", data.GetProperty("tokenType").GetString());
        Assert.False(string.IsNullOrEmpty(data.GetProperty("accessToken").GetString()));
        Assert.False(string.IsNullOrEmpty(data.GetProperty("refreshToken").GetString()));
        Assert.EndsWith("Z", data.GetProperty("accessTokenExpiresAt").GetString(), StringComparison.Ordinal);
        Assert.True(data.GetProperty("refreshTokenExpiresAt").GetDateTime() > data.GetProperty("accessTokenExpiresAt").GetDateTime());

        var user = data.GetProperty("user");
        Assert.Equal("7", user.GetProperty("userId").GetString());
        Assert.Equal(TestPeople.EmployeeCode, user.GetProperty("employeeId").GetString());
        Assert.Equal("TST- Aye Chan Moe", user.GetProperty("fullName").GetString());
        Assert.Equal(TestPeople.Email, user.GetProperty("email").GetString());
        Assert.Equal("Finance", user.GetProperty("department").GetString());
        Assert.Equal("Senior Accountant", user.GetProperty("designation").GetString());
        Assert.False(user.GetProperty("mustChangePassword").GetBoolean());

        Assert.Equal(1, host.Sessions.Count(host.Employee.Id));
        Assert.Equal(1, host.Accounts.SignIns);
    }

    [Fact]
    public async Task The_access_token_names_the_session_and_the_account_but_carries_no_permissions_or_email()
    {
        using var host = new MobileHost();
        var data = await host.LoginAsync(deviceId: "device-a");

        var token = new JsonWebToken(data.GetProperty("accessToken").GetString());

        Assert.Equal(host.Employee.Id, token.Subject);
        Assert.Equal("device-a", token.GetClaim(MobileClaimTypes.SessionId).Value);
        Assert.Equal("7", token.GetClaim(MobileClaimTypes.AccountId).Value);
        Assert.Equal("42", token.GetClaim(MobileClaimTypes.EmployeeId).Value);
        Assert.Equal("HS256", token.Alg);
        Assert.DoesNotContain(token.Claims, claim => claim.Type is "email" or "permissions" or "role");
        Assert.True(token.ValidTo - token.IssuedAt <= TimeSpan.FromMinutes(15));
    }

    [Fact]
    public async Task An_employee_can_also_sign_in_with_the_work_email()
    {
        using var host = new MobileHost();

        var data = await host.LoginAsync(userName: "Aye.Chan@Example.test");

        Assert.Equal(TestPeople.EmployeeCode, data.GetProperty("user").GetProperty("employeeId").GetString());
    }

    [Theory]
    [InlineData(TestPeople.EmployeeCode, "wrong password!!")]
    [InlineData("TST-99999", TestPeople.Password)]
    [InlineData("nobody@example.test", TestPeople.Password)]
    [InlineData(TestPeople.AdminEmail, TestPeople.Password)]
    public async Task A_wrong_password_an_unknown_user_and_an_account_without_an_employee_all_get_the_same_generic_401(string userName, string password)
    {
        using var host = new MobileHost();
        using var client = host.CreateClient();

        using var response = await client.PostAsJsonAsync(LoginPath, new { userName, password, deviceId = "device-a" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await MobileHost.Json(response);
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Equal(MobileAuthMessages.SignInFailed, body.GetProperty("message").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, body.GetProperty("data").ValueKind);
        Assert.Equal(0, host.Sessions.Count(host.Employee.Id) + host.Sessions.Count(host.Admin.Id));
    }

    [Theory]
    [InlineData(AccountStatus.Inactive)]
    [InlineData(AccountStatus.Locked)]
    public async Task An_inactive_or_locked_account_cannot_sign_in(AccountStatus status)
    {
        using var host = new MobileHost();
        host.Accounts.Set(TestPeople.Account(host.Employee.Id, status));
        using var client = host.CreateClient();

        using var response = await client.PostAsJsonAsync(LoginPath, new { userName = TestPeople.EmployeeCode, password = TestPeople.Password, deviceId = "device-a" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task An_employee_who_has_left_cannot_sign_in()
    {
        using var host = new MobileHost();
        host.Accounts.Set(TestPeople.Account(host.Employee.Id) with { EmployeeLeft = true });
        using var client = host.CreateClient();

        using var response = await client.PostAsJsonAsync(LoginPath, new { userName = TestPeople.EmployeeCode, password = TestPeople.Password, deviceId = "device-a" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_user_out_so_even_the_right_password_is_refused()
    {
        using var host = new MobileHost();
        using var client = host.CreateClient();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var wrong = await client.PostAsJsonAsync(LoginPath, new { userName = TestPeople.EmployeeCode, password = "wrong password!!", deviceId = "device-a" });
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }

        using var right = await client.PostAsJsonAsync(LoginPath, new { userName = TestPeople.EmployeeCode, password = TestPeople.Password, deviceId = "device-a" });

        Assert.Equal(HttpStatusCode.Unauthorized, right.StatusCode);
        Assert.NotNull(host.Employee.LockoutEnd);
    }

    [Fact]
    public async Task Missing_fields_are_a_400_with_an_error_per_field()
    {
        using var host = new MobileHost();
        using var client = host.CreateClient();

        using var response = await client.PostAsJsonAsync(LoginPath, new { userName = " ", password = "", deviceId = new string('x', 101) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await MobileHost.Json(response)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("userName", out _));
        Assert.True(errors.TryGetProperty("password", out _));
        Assert.True(errors.TryGetProperty("deviceId", out _));
    }

    [Fact]
    public async Task A_malformed_body_is_a_400_in_the_envelope_shape()
    {
        using var host = new MobileHost();
        using var client = host.CreateClient();

        using var response = await client.PostAsync(LoginPath, new StringContent("{ not json", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await MobileHost.Json(response);
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Equal("The request body or parameters are malformed.", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Signing_in_again_on_the_same_device_replaces_its_session()
    {
        using var host = new MobileHost();
        var first = await host.LoginAsync(deviceId: "device-a");
        await host.LoginAsync(deviceId: "device-a");

        Assert.Equal(1, host.Sessions.Count(host.Employee.Id));
        using var client = host.CreateClient();
        using var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = first.GetProperty("refreshToken").GetString(), deviceId = "device-a" });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task The_sign_in_endpoints_are_rate_limited_per_client_with_a_429_envelope()
    {
        using var host = new MobileHost(permitsPerMinute: 2);
        using var client = host.CreateClient();

        HttpResponseMessage? last = null;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            last?.Dispose();
            last = await client.PostAsJsonAsync(LoginPath, new { userName = TestPeople.EmployeeCode, password = "wrong password!!", deviceId = "device-a" });
        }

        using (last)
        {
            Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
            Assert.False((await MobileHost.Json(last)).GetProperty("success").GetBoolean());
        }
    }
}
