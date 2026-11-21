using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Apex.Hrms.Mobile.Application.Auth;
using Apex.Hrms.MobileApi.Tests.Support;

namespace Apex.Hrms.MobileApi.Tests;

/// <summary>Refresh (rotation and reuse detection), me, logout and change-password, end to end through HTTP.</summary>
public sealed class MobileSessionTests
{
    private const string RefreshPath = "/api/v1/auth/refresh";
    private const string MePath = "/api/v1/auth/me";

    private static string Access(JsonElement data) => data.GetProperty("accessToken").GetString()!;

    private static string Refresh(JsonElement data) => data.GetProperty("refreshToken").GetString()!;

    private static async Task<HttpResponseMessage> RefreshAsync(MobileHost host, string refreshToken, string deviceId = "device-a")
    {
        using var client = host.CreateClient();
        return await client.PostAsJsonAsync(RefreshPath, new { refreshToken, deviceId });
    }

    private static async Task<HttpStatusCode> MeStatusAsync(MobileHost host, string accessToken)
    {
        using var client = host.CreateClient(accessToken);
        using var response = await client.GetAsync(MePath);
        return response.StatusCode;
    }

    [Fact]
    public async Task Me_answers_the_user_with_the_role_and_its_permissions()
    {
        using var host = new MobileHost();
        var login = await host.LoginAsync();
        using var client = host.CreateClient(Access(login));

        using var response = await client.GetAsync(MePath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await MobileHost.Json(response)).GetProperty("data");
        Assert.Equal(TestPeople.EmployeeCode, data.GetProperty("user").GetProperty("employeeId").GetString());
        Assert.Equal("Employee", data.GetProperty("roleName").GetString());
        Assert.Equal(["attendance:View", "leave:Create"], data.GetProperty("permissions").EnumerateArray().Select(item => item.GetString()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-token")]
    [InlineData("eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJ4In0.c2lnbmF0dXJl")]
    public async Task Without_a_valid_access_token_a_signed_in_endpoint_answers_401_in_the_envelope_shape(string? accessToken)
    {
        using var host = new MobileHost();
        using var client = host.CreateClient(accessToken);

        using var response = await client.GetAsync(MePath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.ToString());
        var body = await MobileHost.Json(response);
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Equal(MobileAuthMessages.SessionEnded, body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Refresh_rotates_the_refresh_token_and_issues_a_working_access_token()
    {
        using var host = new MobileHost();
        var login = await host.LoginAsync();

        using var response = await RefreshAsync(host, Refresh(login));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await MobileHost.Json(response)).GetProperty("data");
        Assert.NotEqual(Refresh(login), Refresh(data));
        Assert.Equal(HttpStatusCode.OK, await MeStatusAsync(host, Access(data)));
        Assert.Equal(TestPeople.EmployeeCode, data.GetProperty("user").GetProperty("employeeId").GetString());
    }

    [Fact]
    public async Task Presenting_a_rotated_refresh_token_again_ends_the_session_of_the_device()
    {
        using var host = new MobileHost();
        var login = await host.LoginAsync();
        using var rotated = await RefreshAsync(host, Refresh(login));
        var current = (await MobileHost.Json(rotated)).GetProperty("data");

        using var replay = await RefreshAsync(host, Refresh(login));

        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Equal(0, host.Sessions.Count(host.Employee.Id));
        using var afterReplay = await RefreshAsync(host, Refresh(current));
        Assert.Equal(HttpStatusCode.Unauthorized, afterReplay.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, await MeStatusAsync(host, Access(current)));
    }

    [Fact]
    public async Task A_guessed_refresh_token_is_refused_without_signing_the_device_out()
    {
        using var host = new MobileHost();
        var login = await host.LoginAsync();
        var guessed = Refresh(login).Split('.')[0] + ".AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

        using var refused = await RefreshAsync(host, guessed);
        using var genuine = await RefreshAsync(host, Refresh(login));

        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
        Assert.Equal(HttpStatusCode.OK, genuine.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-dot")]
    [InlineData("!!!.secret")]
    public async Task A_malformed_refresh_token_is_a_401(string refreshToken)
    {
        using var host = new MobileHost();

        using var response = await RefreshAsync(host, refreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_refresh_token_only_works_on_the_device_it_was_issued_to()
    {
        using var host = new MobileHost();
        var login = await host.LoginAsync(deviceId: "device-a");

        using var response = await RefreshAsync(host, Refresh(login), deviceId: "device-b");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task An_expired_refresh_token_is_refused_and_its_session_removed()
    {
        using var host = new MobileHost();
        var login = await host.LoginAsync();
        host.Clock.Advance(TimeSpan.FromDays(31));

        using var response = await RefreshAsync(host, Refresh(login));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, host.Sessions.Count(host.Employee.Id));
    }

    [Fact]
    public async Task A_deactivated_account_loses_its_access_token_and_cannot_refresh()
    {
        using var host = new MobileHost();
        var login = await host.LoginAsync();
        host.Accounts.Set(TestPeople.Account(host.Employee.Id, Domain.Security.AccountStatus.Inactive));

        Assert.Equal(HttpStatusCode.Unauthorized, await MeStatusAsync(host, Access(login)));
        using var refresh = await RefreshAsync(host, Refresh(login));
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Logout_ends_the_access_token_at_once_and_the_refresh_token_too()
    {
        using var host = new MobileHost();
        var login = await host.LoginAsync();
        using var client = host.CreateClient(Access(login));

        using var logout = await client.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = Refresh(login) });

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, await MeStatusAsync(host, Access(login)));
        using var refresh = await RefreshAsync(host, Refresh(login));
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Logout_on_one_device_leaves_the_other_devices_signed_in()
    {
        using var host = new MobileHost();
        var phone = await host.LoginAsync(deviceId: "device-a");
        var tablet = await host.LoginAsync(deviceId: "device-b");
        using var client = host.CreateClient(Access(phone));

        using var logout = await client.PostAsync("/api/v1/auth/logout", null);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.OK, await MeStatusAsync(host, Access(tablet)));
    }

    [Fact]
    public async Task A_wrong_current_password_is_a_400_on_that_field()
    {
        using var host = new MobileHost();
        var login = await host.LoginAsync();
        using var client = host.CreateClient(Access(login));

        using var response = await client.PostAsJsonAsync("/api/v1/auth/change-password", new { currentPassword = "wrong password!!", newPassword = "a brand new password" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await MobileHost.Json(response)).GetProperty("errors");
        Assert.Equal(MobileAuthMessages.WrongCurrentPassword, errors.GetProperty("currentPassword")[0].GetString());
        Assert.Equal(1, host.Employee.AccessFailedCount);
    }

    [Fact]
    public async Task A_short_new_password_is_refused_before_the_current_one_is_checked()
    {
        using var host = new MobileHost();
        var login = await host.LoginAsync();
        using var client = host.CreateClient(Access(login));

        using var response = await client.PostAsJsonAsync("/api/v1/auth/change-password", new { currentPassword = TestPeople.Password, newPassword = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await MobileHost.Json(response)).GetProperty("errors").TryGetProperty("newPassword", out _));
    }

    [Fact]
    public async Task Changing_the_password_keeps_this_device_signed_in_and_ends_every_other_device()
    {
        using var host = new MobileHost();
        var phone = await host.LoginAsync(deviceId: "device-a");
        var tablet = await host.LoginAsync(deviceId: "device-b");
        using var client = host.CreateClient(Access(phone));
        const string NewPassword = "a brand new password";

        using var change = await client.PostAsJsonAsync("/api/v1/auth/change-password", new { currentPassword = TestPeople.Password, newPassword = NewPassword });

        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);

        // This device: the access token and the refresh token still work.
        Assert.Equal(HttpStatusCode.OK, await MeStatusAsync(host, Access(phone)));
        using (var phoneRefresh = await RefreshAsync(host, Refresh(phone), "device-a"))
        {
            Assert.Equal(HttpStatusCode.OK, phoneRefresh.StatusCode);
        }

        // The other device is signed out.
        Assert.Equal(HttpStatusCode.Unauthorized, await MeStatusAsync(host, Access(tablet)));
        using (var tabletRefresh = await RefreshAsync(host, Refresh(tablet), "device-b"))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, tabletRefresh.StatusCode);
        }

        // Only the new password signs in.
        using var anonymous = host.CreateClient();
        using var old = await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { userName = TestPeople.EmployeeCode, password = TestPeople.Password, deviceId = "device-c" });
        Assert.Equal(HttpStatusCode.Unauthorized, old.StatusCode);
        await host.LoginAsync(deviceId: "device-c", password: NewPassword);
    }

    [Fact]
    public async Task A_password_reset_elsewhere_ends_the_mobile_sessions()
    {
        using var host = new MobileHost();
        var login = await host.LoginAsync();

        // The web API's reset changes the security stamp; that is all the mobile API sees of it.
        host.Employee.SecurityStamp = Guid.NewGuid().ToString("N");

        Assert.Equal(HttpStatusCode.Unauthorized, await MeStatusAsync(host, Access(login)));
        using var refresh = await RefreshAsync(host, Refresh(login));
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }
}
