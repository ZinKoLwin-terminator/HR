using System.Net;
using System.Net.Http.Json;
using Apex.Hrms.Application.Excel;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Security;
using Apex.Hrms.TestSupport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.Api.Tests;

/// <summary>
/// The permission check through the real pipeline (authentication, antiforgery, authorization, routing) without a database:
/// a signed-in caller whose role lacks a grant gets 403 from a sample route of every module, and a caller whose role holds
/// exactly that grant gets past authorization. To prove "past authorization" without a database, a test-only result handler
/// answers 204 for a request the policy let through (the endpoint code, which would query the database, does not run).
/// </summary>
public sealed class PermissionPolicyHttpTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly string[] ExportOnly = ["excel:Export", "employees:View"];

    private readonly WebApplicationFactory<Program> factory;

    public PermissionPolicyHttpTests(WebApplicationFactory<Program> factory) => this.factory = factory;

    public enum SignInBlock
    {
        Inactive,
        RoleInactive,
        EmployeeLeft,
    }

    /// <summary>One sample route per module and permission, with the grant its policy needs.</summary>
    public static TheoryData<string, string, string> Routes => new()
    {
        { "GET", "/api/companies/query", "organization:View" },
        { "POST", "/api/companies", "organization:Create" },
        { "GET", "/api/employees/query", "employees:View" },
        { "GET", "/api/employees/1/personal", "employees:ViewSensitive" },
        { "PUT", "/api/employees/1/personal", "employees:EditSensitive" },
        { "GET", "/api/employees/1/bank-account", "employees:ViewBank" },
        { "PUT", "/api/employees/1/bank-account", "employees:EditBank" },
        { "POST", "/api/employees/1/exit", "employees:Edit" },
        { "POST", "/api/leave-applications/1/approve", "leave:Approve" },
        { "POST", "/api/attendance-adjustments/1/approve", "attendance:Approve" },
        // Every employee holds attendance:Create to punch, so creating shifts and holidays needs Edit.
        { "POST", "/api/shifts", "attendance:Edit" },
        { "POST", "/api/holidays", "attendance:Edit" },
        { "POST", "/api/attendance/punches", "attendance:Create" },
        { "GET", "/api/job-openings/query", "recruitment:View" },
        { "GET", "/api/dashboard/summary", "dashboard:View" },
        { "GET", "/api/activity/query", "activity:View" },
        { "GET", "/api/excel/modules", "excel:View" },
        { "POST", "/api/excel/imports/employees/validate", "excel:Import" },
        { "POST", "/api/excel/exports/employees", "excel:Export" },
    };

    private HttpClient ClientWith(IEnumerable<string> grants, Action<IServiceCollection>? configure = null, bool markAuthorized = true)
    {
        // The grants travel in a request header, so one host serves every grant set (starting a host is slow). A test with an
        // extra service of its own gets a host of its own.
        WebApplicationFactory<Program> Build() => SignedInHost.Customize(factory, AccessFakes.Account("HR Officer"), grantsFromHeader: true).WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            if (markAuthorized)
            {
                services.AddSingleton<IAuthorizationMiddlewareResultHandler, AuthorizedMarker>();
            }

            configure?.Invoke(services);
        }));

        var host = configure is null ? SignedInHost.Cached(factory, $"grants-header|{markAuthorized}", _ => Build()) : Build();
        return SignedInHost.CreateClientWithGrants(host, grants);
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string url) =>
        client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url) { Content = JsonContent.Create(new { }) });

    private static Task<HttpResponseMessage> ExportAsync(HttpClient client, bool includeSensitive) =>
        client.PostAsJsonAsync("/api/excel/exports/employees", new { includeSensitive });

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task A_caller_whose_role_holds_no_grant_gets_403(string method, string url, string needs)
    {
        using var client = ClientWith([]);

        var response = await SendAsync(client, method, url);

        Assert.True(response.StatusCode == HttpStatusCode.Forbidden, $"{method} {url} ({needs}) answered {(int)response.StatusCode}.");
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task A_caller_whose_role_holds_everything_except_the_needed_grant_gets_403(string method, string url, string needs)
    {
        using var client = ClientWith(AccessFakes.AllGrants.Where(grant => grant != needs));

        var response = await SendAsync(client, method, url);

        Assert.True(response.StatusCode == HttpStatusCode.Forbidden, $"{method} {url} without {needs} answered {(int)response.StatusCode}.");
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task A_caller_whose_role_holds_the_needed_grant_gets_past_authorization(string method, string url, string needs)
    {
        using var client = ClientWith([needs]);

        var response = await SendAsync(client, method, url);

        Assert.True(response.StatusCode == HttpStatusCode.NoContent, $"{method} {url} with {needs} answered {(int)response.StatusCode}.");
    }

    [Theory]
    [InlineData("/api/shifts")]
    [InlineData("/api/holidays")]
    public async Task An_employee_role_with_only_view_and_create_cannot_create_shifts_or_holidays_but_can_punch(string createUrl)
    {
        string[] employeeRole = ["attendance:View", "attendance:Create", "leave:View", "leave:Create", "dashboard:View"];
        using var client = ClientWith(employeeRole);

        var create = await SendAsync(client, "POST", createUrl);
        var punch = await SendAsync(client, "POST", "/api/attendance/punches");

        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, punch.StatusCode);
    }

    [Theory]
    [InlineData(SignInBlock.Inactive)]
    [InlineData(SignInBlock.RoleInactive)]
    [InlineData(SignInBlock.EmployeeLeft)]
    public async Task A_caller_who_can_no_longer_sign_in_is_refused_although_the_role_holds_every_grant(SignInBlock block)
    {
        var account = block switch
        {
            SignInBlock.Inactive => AccessFakes.Account(status: AccountStatus.Inactive),
            SignInBlock.RoleInactive => AccessFakes.Account(roleActive: false),
            _ => AccessFakes.Account(employeeLeft: true),
        };
        var host = SignedInHost.Customize(factory, account, AccessFakes.AllGrants);
        using var client = BrowserHandler.CreateClient(host.Server.CreateHandler(), out _);

        var response = await client.GetAsync("/api/companies/query");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---- Excel: the policies checked by name inside the endpoint (the real endpoint code runs here) ----

    [Fact]
    public async Task An_export_of_a_module_needs_the_view_permission_of_that_module()
    {
        using var client = ClientWith(["excel:Export"], markAuthorized: false);

        var response = await ExportAsync(client, includeSensitive: false);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("employees:ViewSensitive")]
    [InlineData("employees:ViewBank")]
    public async Task A_sensitive_export_needs_both_the_personal_and_the_bank_permission(string only)
    {
        using var client = ClientWith([.. ExportOnly, only], markAuthorized: false);

        var response = await ExportAsync(client, includeSensitive: true);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_export_reaches_the_export_service_when_every_needed_grant_is_held_and_a_plain_one_needs_no_sensitive_grant()
    {
        using var sensitiveClient = ClientWith(
            [.. ExportOnly, "employees:ViewSensitive", "employees:ViewBank"],
            services => services.AddScoped<IExcelExportService, NotFoundExport>(),
            markAuthorized: false);
        using var plainClient = ClientWith(ExportOnly, services => services.AddScoped<IExcelExportService, NotFoundExport>(), markAuthorized: false);

        var sensitive = await ExportAsync(sensitiveClient, includeSensitive: true);
        var plain = await ExportAsync(plainClient, includeSensitive: false);

        Assert.Equal(HttpStatusCode.NotFound, sensitive.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, plain.StatusCode);
    }

    [Theory]
    [InlineData("employees:Create")]
    [InlineData("employees:Edit")]
    [InlineData("employees:EditSensitive")]
    [InlineData("employees:EditBank")]
    public async Task An_employee_import_needs_the_write_permissions_of_the_employee_module_on_top_of_excel_import(string missing)
    {
        string[] needed = ["excel:Import", "employees:Create", "employees:Edit", "employees:EditSensitive", "employees:EditBank"];
        using var client = ClientWith(needed.Where(grant => grant != missing), markAuthorized: false);

        var response = await client.PostAsJsonAsync("/api/excel/imports/employees/validate", new { batchId = "none" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private sealed class NotFoundExport : IExcelExportService
    {
        public Task<Result<ExcelFileResult>> ExportAsync(string moduleCode, ExportRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Failure<ExcelFileResult>(AppError.NotFound("No data in this test.")));
    }

    private sealed class AuthorizedMarker : IAuthorizationMiddlewareResultHandler
    {
        private readonly AuthorizationMiddlewareResultHandler fallback = new();

        public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
        {
            if (authorizeResult.Succeeded)
            {
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return Task.CompletedTask;
            }

            return fallback.HandleAsync(next, context, policy, authorizeResult);
        }
    }
}
