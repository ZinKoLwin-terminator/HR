using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Apex.Hrms.Application.Security;
using Apex.Hrms.Mobile.Application.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.MobileApi.Tests.Support;

/// <summary>
/// A Development host of the mobile API whose identity store, accounts, employee lookup and sessions are in memory, so the
/// whole sign-in flow runs without a database. One host per test: the fakes hold state.
/// </summary>
internal sealed class MobileHost : IDisposable
{
    private readonly WebApplicationFactory<Program> factory;

    public MobileHost(int permitsPerMinute = 1000)
    {
        Users = new InMemoryUserStore();
        Sessions = new InMemoryMobileSessionStore(Users);
        Employee = TestPeople.NewUser(TestPeople.Email);
        Users.Add(Employee);
        Accounts.Set(TestPeople.Account(Employee.Id));
        Lookup.Add(TestPeople.EmployeeId, Employee.Id, TestPeople.Profile);

        // An account without an employee (the built-in administrator): the web app only, never the mobile app.
        Admin = TestPeople.NewUser(TestPeople.AdminEmail);
        Users.Add(Admin);
        Accounts.Set(TestPeople.Account(Admin.Id) with { Id = TestPeople.AdminAccountId, EmployeeId = null, Email = TestPeople.AdminEmail });

        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("MobileAuth:RateLimit:PermitsPerMinute", permitsPerMinute.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<TimeProvider>(Clock);
                services.AddSingleton<IUserStore<IdentityUser>>(Users);
                services.AddSingleton<IAccountDirectory>(Accounts);
                services.AddSingleton<IMobileEmployeeDirectory>(Lookup);
                services.AddSingleton<IMobileSessionStore>(Sessions);
            });
        });
    }

    public MutableTimeProvider Clock { get; } = new();

    public InMemoryUserStore Users { get; }

    public FakeAccountDirectory Accounts { get; } = new();

    public FakeEmployeeDirectory Lookup { get; } = new();

    public InMemoryMobileSessionStore Sessions { get; }

    public IdentityUser Employee { get; }

    public IdentityUser Admin { get; }

    public IServiceProvider Services => factory.Services;

    public HttpClient CreateClient(string? accessToken = null)
    {
        var client = factory.CreateClient();
        if (accessToken is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return client;
    }

    /// <summary>Signs the test employee in on <paramref name="deviceId"/> and returns the <c>data</c> of the answer.</summary>
    public async Task<JsonElement> LoginAsync(string deviceId = "device-a", string userName = TestPeople.EmployeeCode, string password = TestPeople.Password)
    {
        using var client = CreateClient();
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { userName, password, deviceId, deviceName = "Test phone" });
        var body = await Json(response);
        Assert.True(response.IsSuccessStatusCode, body.ToString());
        return body.GetProperty("data");
    }

    public static async Task<JsonElement> Json(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>())!;

    public void Dispose() => factory.Dispose();
}
