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
        { "GET", "/api/kpi/categories/query", "kpi-setup:View" },
        { "POST", "/api/kpi/templates", "kpi-setup:Create" },
        { "PUT", "/api/kpi/templates/1", "kpi-setup:Edit" },
        { "DELETE", "/api/kpi/templates/1", "kpi-setup:Delete" },
        { "POST", "/api/kpi/templates/1/deactivate", "kpi-setup:Edit" },
        { "GET", "/api/kpi/periods/query", "kpi-setup:View" },
        { "POST", "/api/kpi/periods", "kpi-setup:Create" },
        { "DELETE", "/api/kpi/periods/1", "kpi-setup:Delete" },
        { "POST", "/api/kpi/periods/1/activate", "kpi-setup:Edit" },
        { "POST", "/api/kpi/periods/1/complete", "kpi-setup:Edit" },
        { "POST", "/api/kpi/periods/1/close", "kpi-setup:Edit" },
        // The period picker is for employees and managers: it needs the performance view, not the setup permission.
        { "GET", "/api/kpi/periods/options", "kpi:View" },
        { "GET", "/api/kpi/rating-bands/query", "kpi-setup:View" },
        { "POST", "/api/kpi/rating-bands/1/activate", "kpi-setup:Edit" },
        // Scorecard reads need the performance view (rows are scoped in the query); assigning and planning are HR work.
        { "GET", "/api/kpi/scorecards/query", "kpi:View" },
        { "GET", "/api/kpi/scorecards/1", "kpi:View" },
        { "DELETE", "/api/kpi/scorecards/1", "kpi-setup:Delete" },
        { "POST", "/api/kpi/scorecards/1/finalize-plan", "kpi-setup:Edit" },
        { "POST", "/api/kpi/assignments", "kpi-setup:Create" },
        { "POST", "/api/kpi/assignments/bulk", "kpi-setup:Create" },
        { "PUT", "/api/kpi/scorecard-items/1", "kpi-setup:Edit" },
        { "DELETE", "/api/kpi/scorecard-items/1", "kpi-setup:Edit" },
        { "GET", "/api/kpi/my-scorecards", "kpi:View" },
        { "PUT", "/api/kpi/scorecards/1/results", "kpi:Create" },
        { "POST", "/api/kpi/scorecards/1/submit", "kpi:Create" },
        { "GET", "/api/kpi/reviews/pending", "kpi:Approve" },
        // Evidence files: add and remove are the employee's (kpi:Create, own scorecard decided in the application layer); download follows the scorecard read.
        { "POST", "/api/kpi/scorecard-items/1/attachments", "kpi:Create" },
        { "GET", "/api/kpi/attachments/1", "kpi:View" },
        { "DELETE", "/api/kpi/attachments/1", "kpi:Create" },
        { "PUT", "/api/kpi/scorecards/1/review", "kpi:Approve" },
        { "POST", "/api/kpi/scorecards/1/start-review", "kpi:Approve" },
        { "POST", "/api/kpi/scorecards/1/approve", "kpi:Approve" },
        { "POST", "/api/kpi/scorecards/1/return", "kpi:Approve" },
        { "POST", "/api/kpi/scorecards/1/reject", "kpi:Approve" },
        { "POST", "/api/kpi/scorecards/1/finalize", "kpi:Approve" },
        // Dashboards and reports (G1): the HR dashboard also needs the HR tier, which the application layer checks (403).
        { "GET", "/api/kpi/dashboard/employee", "kpi:View" },
        { "GET", "/api/kpi/dashboard/manager", "kpi:Approve" },
        { "GET", "/api/kpi/dashboard/hr", "kpi:Approve" },
        { "GET", "/api/kpi/reports/summary", "kpi:View" },
        { "GET", "/api/kpi/reports/employee/1", "kpi:View" },
        { "GET", "/api/kpi/reports/department/1", "kpi:View" },
        { "GET", "/api/kpi/reports/ranking", "kpi:View" },
        { "GET", "/api/kpi/reports/kpi-achievement", "kpi:View" },
        { "GET", "/api/kpi/reports/target-vs-actual", "kpi:View" },
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

    [Fact]
    public async Task An_employee_role_with_the_performance_view_can_pick_a_period_but_cannot_read_the_setup()
    {
        string[] employeeRole = ["kpi:View", "kpi:Create"];
        using var client = ClientWith(employeeRole);

        var options = await SendAsync(client, "GET", "/api/kpi/periods/options");
        var periods = await SendAsync(client, "GET", "/api/kpi/periods/query");
        var templates = await SendAsync(client, "GET", "/api/kpi/templates/query");
        var activate = await SendAsync(client, "POST", "/api/kpi/periods/1/activate");

        Assert.Equal(HttpStatusCode.NoContent, options.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, periods.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, templates.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, activate.StatusCode);
    }

    [Fact]
    public async Task An_employee_role_reads_scorecards_but_cannot_assign_or_plan_and_hr_setup_alone_cannot_read_them()
    {
        string[] employeeRole = ["kpi:View", "kpi:Create"];
        using var employee = ClientWith(employeeRole);
        string[] setupOnly = ["kpi-setup:View", "kpi-setup:Create", "kpi-setup:Edit", "kpi-setup:Delete"];
        using var hr = ClientWith(setupOnly);

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(employee, "GET", "/api/kpi/scorecards/query")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(employee, "GET", "/api/kpi/scorecards/1")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(employee, "POST", "/api/kpi/assignments")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(employee, "POST", "/api/kpi/assignments/bulk")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(employee, "PUT", "/api/kpi/scorecard-items/1")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(employee, "POST", "/api/kpi/scorecards/1/finalize-plan")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(hr, "POST", "/api/kpi/assignments")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(hr, "GET", "/api/kpi/scorecards/query")).StatusCode);
    }

    [Fact]
    public async Task An_employee_role_submits_its_scorecard_but_cannot_review_and_a_manager_role_reviews_but_cannot_plan()
    {
        string[] employeeRole = ["kpi:View", "kpi:Create"];
        string[] managerRole = ["kpi:View", "kpi:Create", "kpi:Approve"];
        using var employee = ClientWith(employeeRole);
        using var manager = ClientWith(managerRole);

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(employee, "GET", "/api/kpi/my-scorecards")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(employee, "PUT", "/api/kpi/scorecards/1/results")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(employee, "POST", "/api/kpi/scorecards/1/submit")).StatusCode);

        foreach (var review in new[] { "start-review", "approve", "return", "reject", "finalize" })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(employee, "POST", $"/api/kpi/scorecards/1/{review}")).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(manager, "POST", $"/api/kpi/scorecards/1/{review}")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(employee, "PUT", "/api/kpi/scorecards/1/review")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(employee, "GET", "/api/kpi/reviews/pending")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(manager, "PUT", "/api/kpi/scorecards/1/review")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(manager, "GET", "/api/kpi/reviews/pending")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(manager, "POST", "/api/kpi/scorecards/1/finalize-plan")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(manager, "POST", "/api/kpi/assignments")).StatusCode);
    }

    [Fact]
    public async Task The_setup_permission_alone_does_not_open_the_period_picker()
    {
        string[] setupOnly = ["kpi-setup:View", "kpi-setup:Create", "kpi-setup:Edit", "kpi-setup:Delete"];
        using var client = ClientWith(setupOnly);

        var options = await SendAsync(client, "GET", "/api/kpi/periods/options");

        Assert.Equal(HttpStatusCode.Forbidden, options.StatusCode);
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

    // ---- Excel: the other modules (organization, leave, attendance, payroll) check their own policies by name ----

    // One representative per policy family (each case builds a host, which is slow): the grant named is the one left out.
    [Theory]
    [InlineData("companies", "organization:Edit", "organization:Create organization:Edit")]
    [InlineData("shift-assignments", "employees:Edit", "attendance:Edit attendance:Approve employees:Edit")]
    [InlineData("shift-assignments", "attendance:Approve", "attendance:Edit attendance:Approve employees:Edit")]
    [InlineData("salary-assignments", "payroll:Approve", "payroll:View payroll:Create payroll:Edit payroll:Approve")]
    public async Task An_import_needs_every_policy_of_its_module_on_top_of_excel_import(string module, string missing, string policies)
    {
        string[] needed = ["excel:Import", .. policies.Split(' ')];
        using var client = ClientWith(needed.Where(grant => grant != missing), markAuthorized: false);

        var validate = await client.PostAsJsonAsync($"/api/excel/imports/{module}/validate", new { uploadId = "none", strategy = "InsertOnly" });
        var template = await client.GetAsync($"/api/excel/templates/{module}");

        Assert.Equal(HttpStatusCode.Forbidden, validate.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, template.StatusCode);
    }

    [Theory]
    [InlineData("companies", "organization:View", "organization:View")]
    [InlineData("leave-balances", "leave:View", "leave:View")]
    [InlineData("attendance-records", "attendance:View", "attendance:View")]
    [InlineData("kpi-scorecards", "kpi:View", "kpi:View")]
    [InlineData("kpi-ranking", "kpi:View", "kpi:View")]
    [InlineData("kpi-target-vs-actual", "kpi:View", "kpi:View")]
    [InlineData("kpi-ranking", "excel:Export", "kpi:View")]
    [InlineData("salary-assignments", "payroll:Approve", "payroll:View payroll:Approve")]
    public async Task An_export_needs_every_policy_of_its_module_on_top_of_excel_export(string module, string missing, string policies)
    {
        string[] needed = ["excel:Export", .. policies.Split(' ')];
        using var client = ClientWith(needed.Where(grant => grant != missing), markAuthorized: false);

        var response = await client.PostAsJsonAsync($"/api/excel/exports/{module}", new { includeSensitive = false });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("leave-applications", "leave:View")]
    [InlineData("salary-assignments", "payroll:View payroll:Approve")]
    [InlineData("kpi-scorecards", "kpi:View")]
    [InlineData("kpi-ranking", "kpi:View")]
    [InlineData("kpi-target-vs-actual", "kpi:View")]
    public async Task An_export_reaches_the_service_when_every_grant_of_the_module_is_held(string module, string policies)
    {
        using var client = ClientWith(["excel:Export", .. policies.Split(' ')], services => services.AddScoped<IExcelExportService, NotFoundExport>(), markAuthorized: false);

        var response = await client.PostAsJsonAsync($"/api/excel/exports/{module}", new { includeSensitive = true });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
