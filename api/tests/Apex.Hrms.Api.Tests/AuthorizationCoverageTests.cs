using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.Api.Tests;

/// <summary>
/// Architecture rule: no endpoint of the API is mapped without an authorization policy, and the policy matches what
/// the endpoint does. Each module policy is a PermissionRequirement checked against sec.RolePermission (see PermissionHandler);
/// this test keeps the seams complete so no endpoint is forgotten and the policy names the frontend relies on stay stable.
/// </summary>
public sealed class AuthorizationCoverageTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public AuthorizationCoverageTests(WebApplicationFactory<Program> factory) =>
        this.factory = SignedInHost.Cached(factory, "development", shared => shared.WithWebHostBuilder(builder => builder.UseEnvironment("Development")));

    private List<RouteEndpoint> ApiEndpoints()
    {
        using var client = factory.CreateClient();
        return [.. factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText!.StartsWith("/api/", StringComparison.Ordinal)
                && !IsAuthRoute(endpoint))];
    }

    // The /api/auth endpoints are covered by AuthEndpointSecurityTests: some of them must be anonymous.
    private static bool IsAuthRoute(RouteEndpoint endpoint) =>
        endpoint.RoutePattern.RawText!.StartsWith("/api/auth", StringComparison.Ordinal);

    [Fact]
    public void Every_api_endpoint_requires_a_named_policy()
    {
        var endpoints = ApiEndpoints();

        Assert.NotEmpty(endpoints);
        var unprotected = endpoints
            .Where(endpoint => !endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(data => !string.IsNullOrWhiteSpace(data.Policy)))
            .Select(endpoint => endpoint.DisplayName)
            .ToList();
        Assert.Empty(unprotected);
        Assert.DoesNotContain(endpoints, endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null);
    }

    private static bool IsEmployeeRoute(RouteEndpoint endpoint) =>
        endpoint.RoutePattern.RawText!.StartsWith("/api/employees", StringComparison.Ordinal);

    private static bool IsBlacklistRoute(RouteEndpoint endpoint) =>
        endpoint.RoutePattern.RawText!.StartsWith("/api/employee-blacklist", StringComparison.Ordinal);

    private static bool IsSelfServiceRoute(RouteEndpoint endpoint) =>
        endpoint.RoutePattern.RawText!.StartsWith("/api/me/", StringComparison.Ordinal);

    private static bool IsOrganizationLookupRoute(RouteEndpoint endpoint) =>
        endpoint.RoutePattern.RawText == "/api/organization/lookup";

    private static bool IsEmployeeDocumentRoute(RouteEndpoint endpoint) =>
        endpoint.RoutePattern.RawText!.StartsWith("/api/employee-documents", StringComparison.Ordinal);

    [Fact]
    public void Employee_documents_are_personal_data_with_sensitive_permissions_and_no_unpaged_list()
    {
        var policies = ApiEndpoints().Where(IsEmployeeDocumentRoute)
            .ToDictionary(
                endpoint => $"{endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Single()} {endpoint.RoutePattern.RawText}",
                endpoint => endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Single(data => data.Policy is not null).Policy);

        Assert.Equal(5, policies.Count);
        Assert.DoesNotContain("GET /api/employee-documents/", policies.Keys);
        Assert.Equal("employees:ViewSensitive", policies["GET /api/employee-documents/query"]);
        Assert.Equal("employees:ViewSensitive", policies["GET /api/employee-documents/{id:int}"]);
        Assert.Equal("employees:EditSensitive", policies["POST /api/employee-documents/"]);
        Assert.Equal("employees:EditSensitive", policies["PUT /api/employee-documents/{id:int}"]);
        Assert.Equal("employees:EditSensitive", policies["DELETE /api/employee-documents/{id:int}"]);
    }

    private static bool IsKpiRoute(RouteEndpoint endpoint) =>
        endpoint.RoutePattern.RawText!.StartsWith("/api/kpi", StringComparison.Ordinal);

    [Fact]
    public void Kpi_maps_sixty_five_endpoints_and_every_one_uses_a_kpi_permission()
    {
        var policies = PoliciesOf(IsKpiRoute);

        Assert.Equal(65, policies.Count);
        Assert.All(policies.Values, policy => Assert.True(policy.StartsWith("kpi-setup:", StringComparison.Ordinal) || policy.StartsWith("kpi:", StringComparison.Ordinal), policy));
    }

    [Fact]
    public void Kpi_setup_endpoints_have_the_policy_of_their_verb_and_only_the_two_pickers_need_the_performance_view()
    {
        var policies = PoliciesOf(IsKpiRoute);

        foreach (var resource in new[] { "categories", "templates", "rating-bands" })
        {
            Assert.Equal("kpi-setup:View", policies[$"GET /api/kpi/{resource}/"]);
            Assert.Equal("kpi-setup:View", policies[$"GET /api/kpi/{resource}/query"]);
            Assert.Equal("kpi-setup:View", policies[$"GET /api/kpi/{resource}/{{id:int}}"]);
            Assert.Equal("kpi-setup:Create", policies[$"POST /api/kpi/{resource}/"]);
            Assert.Equal("kpi-setup:Edit", policies[$"PUT /api/kpi/{resource}/{{id:int}}"]);
            Assert.Equal("kpi-setup:Delete", policies[$"DELETE /api/kpi/{resource}/{{id:int}}"]);
            Assert.Equal("kpi-setup:Edit", policies[$"POST /api/kpi/{resource}/{{id:int}}/activate"]);
            Assert.Equal("kpi-setup:Edit", policies[$"POST /api/kpi/{resource}/{{id:int}}/deactivate"]);
        }

        Assert.Equal("kpi-setup:View", policies["GET /api/kpi/periods/query"]);
        Assert.Equal("kpi-setup:Create", policies["POST /api/kpi/periods/"]);
        Assert.Equal("kpi-setup:Edit", policies["PUT /api/kpi/periods/{id:int}"]);
        Assert.Equal("kpi-setup:Delete", policies["DELETE /api/kpi/periods/{id:int}"]);
        Assert.Equal("kpi-setup:Edit", policies["POST /api/kpi/periods/{id:int}/activate"]);
        Assert.Equal("kpi-setup:Edit", policies["POST /api/kpi/periods/{id:int}/complete"]);
        Assert.Equal("kpi-setup:Edit", policies["POST /api/kpi/periods/{id:int}/close"]);
        Assert.Equal("kpi:View", policies["GET /api/kpi/periods/options"]);
        Assert.Equal("kpi:View", policies["GET /api/kpi/categories/options"]);

        // No other master-data route is open to the performance view.
        string[] masterData = ["/api/kpi/categories", "/api/kpi/templates", "/api/kpi/rating-bands", "/api/kpi/periods"];
        Assert.Equal(
            ["GET /api/kpi/categories/options", "GET /api/kpi/periods/options"],
            policies
                .Where(entry => entry.Value != null && !entry.Value.StartsWith("kpi-setup:", StringComparison.Ordinal)
                    && masterData.Any(prefix => entry.Key.Contains(prefix + "/", StringComparison.Ordinal)))
                .Select(entry => entry.Key)
                .Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Scorecards_are_read_with_the_performance_view_and_planned_by_hr_with_the_setup_permissions()
    {
        var policies = PoliciesOf(IsKpiRoute);

        Assert.Equal("kpi:View", policies["GET /api/kpi/scorecards/query"]);
        Assert.Equal("kpi:View", policies["GET /api/kpi/scorecards/{id:int}"]);
        Assert.Equal("kpi-setup:Delete", policies["DELETE /api/kpi/scorecards/{id:int}"]);
        Assert.Equal("kpi-setup:Edit", policies["POST /api/kpi/scorecards/{id:int}/finalize-plan"]);
        Assert.Equal("kpi-setup:Create", policies["POST /api/kpi/assignments/"]);
        Assert.Equal("kpi-setup:Create", policies["POST /api/kpi/assignments/bulk"]);
        Assert.Equal("kpi-setup:Edit", policies["PUT /api/kpi/scorecard-items/{id:int}"]);
        Assert.Equal("kpi-setup:Edit", policies["DELETE /api/kpi/scorecard-items/{id:int}"]);
    }

    [Fact]
    public void Submission_needs_kpi_create_and_the_review_steps_and_the_queue_need_kpi_approve()
    {
        var policies = PoliciesOf(IsKpiRoute);

        Assert.Equal("kpi:View", policies["GET /api/kpi/my-scorecards"]);
        Assert.Equal("kpi:Create", policies["PUT /api/kpi/scorecards/{id:int}/results"]);
        Assert.Equal("kpi:Create", policies["POST /api/kpi/scorecards/{id:int}/submit"]);
        Assert.Equal("kpi:Approve", policies["GET /api/kpi/reviews/pending"]);
        Assert.Equal("kpi:Approve", policies["PUT /api/kpi/scorecards/{id:int}/review"]);

        foreach (var step in new[] { "start-review", "approve", "return", "reject", "finalize" })
        {
            Assert.Equal("kpi:Approve", policies[$"POST /api/kpi/scorecards/{{id:int}}/{step}"]);
        }
    }

    [Fact]
    public void Evidence_files_are_added_and_removed_with_kpi_create_and_downloaded_with_kpi_view()
    {
        var policies = PoliciesOf(IsKpiRoute);

        Assert.Equal("kpi:Create", policies["POST /api/kpi/scorecard-items/{itemId:int}/attachments"]);
        Assert.Equal("kpi:View", policies["GET /api/kpi/attachments/{id:int}"]);
        Assert.Equal("kpi:Create", policies["DELETE /api/kpi/attachments/{id:int}"]);
    }

    [Fact]
    public void The_dashboards_need_kpi_view_or_kpi_approve_and_every_report_needs_kpi_view()
    {
        var policies = PoliciesOf(IsKpiRoute);

        Assert.Equal("kpi:View", policies["GET /api/kpi/dashboard/employee"]);
        Assert.Equal("kpi:Approve", policies["GET /api/kpi/dashboard/manager"]);
        Assert.Equal("kpi:Approve", policies["GET /api/kpi/dashboard/hr"]);
        Assert.Equal("kpi:View", policies["GET /api/kpi/reports/summary"]);
        Assert.Equal("kpi:View", policies["GET /api/kpi/reports/employee/{employeeId:int}"]);
        Assert.Equal("kpi:View", policies["GET /api/kpi/reports/department/{departmentId:int}"]);
        Assert.Equal("kpi:View", policies["GET /api/kpi/reports/ranking"]);
        Assert.Equal("kpi:View", policies["GET /api/kpi/reports/kpi-achievement"]);
        Assert.Equal("kpi:View", policies["GET /api/kpi/reports/target-vs-actual"]);
    }

    [Fact]
    public void The_kpi_excel_modules_are_registered_export_only_with_the_kpi_view_policy()
    {
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var modules = scope.ServiceProvider.GetServices<Apex.Hrms.Application.Excel.IExcelModule>()
            .Where(module => module.Info.Code.StartsWith("kpi-", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(["kpi-ranking", "kpi-scorecards", "kpi-target-vs-actual"], modules.Select(module => module.Info.Code).Order(StringComparer.Ordinal));
        Assert.All(modules, module =>
        {
            Assert.True(module.Info.CanExport, module.Info.Code);
            Assert.False(module.Info.CanImport, module.Info.Code);
            Assert.Equal(["kpi:View"], module.ExportPolicies);
            Assert.Empty(module.ImportPolicies);
            Assert.Empty(module.SensitiveExportPolicies);
        });
    }

    private static bool IsGrievanceRoute(RouteEndpoint endpoint) =>
        endpoint.RoutePattern.RawText!.StartsWith("/api/grievances", StringComparison.Ordinal);

    private Dictionary<string, string[]> GrievancePolicies() => ApiEndpoints().Where(IsGrievanceRoute)
        .ToDictionary(
            endpoint => $"{endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Single()} {endpoint.RoutePattern.RawText}",
            endpoint => endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(data => data.Policy).OfType<string>().Order(StringComparer.Ordinal).ToArray());

    [Fact]
    public void Grievances_map_fifty_five_endpoints_each_with_the_policy_of_who_may_use_it()
    {
        var policies = GrievancePolicies();
        const string mine = "grievance:View";
        const string write = "grievance:Create";
        const string team = "grievance-case-team";
        const string work = "grievance-case-work";
        const string manageView = "grievance-management:View";
        const string manageEdit = "grievance-management:Edit";
        var expected = new Dictionary<string, string[]>
        {
            ["GET /api/grievances/categories/"] = [manageView],
            ["GET /api/grievances/categories/query"] = [manageView],
            ["GET /api/grievances/categories/{id:int}"] = [manageView],
            ["POST /api/grievances/categories/"] = ["grievance-management:Create"],
            ["PUT /api/grievances/categories/{id:int}"] = [manageEdit],
            ["DELETE /api/grievances/categories/{id:int}"] = ["grievance-management:Delete"],
            ["POST /api/grievances/categories/{id:int}/activate"] = [manageEdit],
            ["POST /api/grievances/categories/{id:int}/deactivate"] = [manageEdit],
            ["GET /api/grievances/options"] = ["grievance-options"],
            ["GET /api/grievances/my"] = [mine],
            ["GET /api/grievances/my/dashboard"] = [mine],
            ["GET /api/grievances/my/{id:int}"] = [mine],
            ["GET /api/grievances/my/attachments/{id:int}"] = [mine],
            ["POST /api/grievances/my/{id:int}/seen"] = [mine],
            ["POST /api/grievances/my"] = [write],
            ["PUT /api/grievances/my/{id:int}"] = [write],
            ["DELETE /api/grievances/my/{id:int}"] = [write],
            ["POST /api/grievances/my/{id:int}/submit"] = [write],
            ["POST /api/grievances/my/{id:int}/messages"] = [write],
            ["POST /api/grievances/my/{id:int}/attachments"] = [write],
            ["GET /api/grievances/cases/query"] = [manageView],
            ["GET /api/grievances/investigations"] = ["grievance-investigation:View"],
            ["GET /api/grievances/reviews/pending"] = ["grievance-review:View"],
            ["GET /api/grievances/cases/{id:int}"] = [team],
            ["GET /api/grievances/cases/{id:int}/history"] = [team],
            ["GET /api/grievances/attachments/{id:int}"] = [team],
            ["GET /api/grievances/cases/{id:int}/audit"] = [manageView, "hr-administrator"],
            ["GET /api/grievances/audit"] = [manageView, "hr-administrator"],
            ["GET /api/grievances/investigators"] = [manageEdit],
            ["GET /api/grievances/employees"] = ["grievance-employee-picker"],
            ["POST /api/grievances/cases/"] = ["grievance-management:Create"],
            ["POST /api/grievances/cases/{id:int}/acknowledge"] = [manageEdit],
            ["POST /api/grievances/cases/{id:int}/start-review"] = [manageEdit],
            ["POST /api/grievances/cases/{id:int}/assign"] = [manageEdit],
            ["POST /api/grievances/cases/{id:int}/unassign"] = [manageEdit],
            ["POST /api/grievances/cases/{id:int}/start-investigation"] = [work],
            ["POST /api/grievances/cases/{id:int}/request-info"] = [work],
            ["POST /api/grievances/cases/{id:int}/propose-resolution"] = [work],
            ["POST /api/grievances/cases/{id:int}/approve-resolution"] = ["grievance-review:Approve"],
            ["POST /api/grievances/cases/{id:int}/reject-resolution"] = ["grievance-review:Approve"],
            ["POST /api/grievances/cases/{id:int}/reject"] = [manageEdit],
            ["POST /api/grievances/cases/{id:int}/close"] = [manageEdit],
            ["POST /api/grievances/cases/{id:int}/reopen"] = [manageEdit],
            ["POST /api/grievances/cases/{id:int}/escalate"] = [work],
            ["POST /api/grievances/cases/{id:int}/archive"] = ["grievance-management:Delete"],
            ["PUT /api/grievances/cases/{id:int}/triage"] = [manageEdit],
            ["POST /api/grievances/cases/{id:int}/messages"] = [work],
            ["POST /api/grievances/cases/{id:int}/notes"] = [work],
            ["POST /api/grievances/cases/{id:int}/attachments"] = [work],
            ["POST /api/grievances/cases/{id:int}/actions"] = [manageEdit],
            ["PUT /api/grievances/case-actions/{id:int}"] = [manageEdit],
            ["GET /api/grievances/dashboard/hr"] = [manageView],
            ["GET /api/grievances/dashboard/review"] = ["grievance-review:View"],
            ["GET /api/grievances/reports/cases"] = [manageView],
            ["GET /api/grievances/reports/summary"] = ["grievance-insight"],
        };

        Assert.Equal(55, policies.Count);
        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), policies.Keys.Order(StringComparer.Ordinal));
        foreach (var (route, wanted) in expected)
        {
            Assert.True(wanted.Order(StringComparer.Ordinal).SequenceEqual(policies[route]), $"{route}: expected {string.Join(", ", wanted)} but found {string.Join(", ", policies[route])}");
        }
    }

    [Fact]
    public void Grievance_uploads_are_rate_limited_and_the_excel_export_is_export_only_with_the_management_view()
    {
        var uploads = ApiEndpoints().Where(IsGrievanceRoute)
            .Where(endpoint => endpoint.RoutePattern.RawText!.EndsWith("/attachments", StringComparison.Ordinal))
            .ToList();
        Assert.Equal(2, uploads.Count);
        Assert.All(uploads, endpoint => Assert.Equal(
            Apex.Hrms.Api.Http.GrievanceUploadRateLimit.Policy,
            endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute>()?.PolicyName));

        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var module = Assert.Single(scope.ServiceProvider.GetServices<Apex.Hrms.Application.Excel.IExcelModule>(), candidate => candidate.Info.Code == "grievance-cases");
        Assert.True(module.Info.CanExport);
        Assert.False(module.Info.CanImport);
        Assert.Equal(["grievance-management:View"], module.ExportPolicies);
        Assert.False(module.RecordsActivity);
        Assert.Equal(
            ["caseNo", "category", "status", "priority", "department", "submittedOn", "targetResolutionDate", "resolvedOn", "resolutionDays", "isOverdue", "leadInvestigator", "isEscalated"],
            module.Info.ExportColumns!.Select(column => column.Key));
    }

    private static bool IsLeaveRoute(RouteEndpoint endpoint) =>
        endpoint.RoutePattern.RawText!.StartsWith("/api/leave", StringComparison.Ordinal);

    private static readonly string[] AttendancePrefixes = ["/api/shifts", "/api/holidays", "/api/attendance", "/api/overtime-records"];

    private static readonly string[] RecruitmentPrefixes = ["/api/job-openings", "/api/candidates", "/api/applications", "/api/interviews", "/api/recruitment"];

    private static bool IsRecruitmentRoute(RouteEndpoint endpoint) =>
        RecruitmentPrefixes
            .Any(prefix => endpoint.RoutePattern.RawText!.StartsWith(prefix, StringComparison.Ordinal));

    private static bool IsAttendanceRoute(RouteEndpoint endpoint) =>
        AttendancePrefixes
            .Any(prefix => endpoint.RoutePattern.RawText!.StartsWith(prefix, StringComparison.Ordinal));

    private static readonly string[] PayrollPrefixes = ["/api/salary-components", "/api/salary-structures", "/api/salary-assignments", "/api/payroll-runs", "/api/payslips"];

    private static bool IsPayrollRoute(RouteEndpoint endpoint) =>
        PayrollPrefixes.Any(prefix => endpoint.RoutePattern.RawText!.StartsWith(prefix, StringComparison.Ordinal));

    private static bool IsExcelRoute(RouteEndpoint endpoint) =>
        endpoint.RoutePattern.RawText!.StartsWith("/api/excel", StringComparison.Ordinal);

    private static readonly string[] ActivityPrefixes = ["/api/dashboard", "/api/activity", "/api/notifications"];

    private static bool IsActivityRoute(RouteEndpoint endpoint) =>
        ActivityPrefixes.Any(prefix => endpoint.RoutePattern.RawText!.StartsWith(prefix, StringComparison.Ordinal));

    private static readonly string[] SettingsPrefixes = ["/api/users", "/api/roles", "/api/role-permissions", "/api/general-settings"];

    private static bool IsSettingsRoute(RouteEndpoint endpoint) =>
        SettingsPrefixes.Any(prefix => endpoint.RoutePattern.RawText!.StartsWith(prefix, StringComparison.Ordinal));

    /// <summary>Every policy of the endpoints (a settings endpoint has its module policy and the administrator policy).</summary>
    private Dictionary<string, string[]> SettingsPolicies() => ApiEndpoints().Where(IsSettingsRoute)
        .ToDictionary(
            endpoint => $"{endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Single()} {endpoint.RoutePattern.RawText}",
            endpoint => endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(data => data.Policy!).Order(StringComparer.Ordinal).ToArray());

    [Fact]
    public void Settings_maps_twenty_three_endpoints_and_no_unpaged_user_list_and_no_sign_up()
    {
        var policies = SettingsPolicies();

        Assert.Equal(23, policies.Count);
        Assert.DoesNotContain("GET /api/users/", policies.Keys);
        Assert.Contains("GET /api/users/query", policies.Keys);

        // Users exist only because an administrator creates them: the one route that creates an account is admin only, and no
        // settings endpoint is anonymous.
        Assert.Equal(["hr-administrator", "settings:Create"], policies["POST /api/users/"]);
        Assert.DoesNotContain(ApiEndpoints().Where(IsSettingsRoute), endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null);
    }

    [Fact]
    public void Settings_endpoints_have_the_module_policy_of_their_verb_and_need_an_administrator()
    {
        var policies = SettingsPolicies();

        Assert.Equal(["hr-administrator", "settings:View"], policies["GET /api/users/query"]);
        Assert.Equal(["hr-administrator", "settings:View"], policies["GET /api/users/{id:int}"]);
        Assert.Equal(["hr-administrator", "settings:Edit"], policies["PUT /api/users/{id:int}"]);
        Assert.Equal(["hr-administrator", "settings:Delete"], policies["DELETE /api/users/{id:int}"]);
        Assert.Equal(["hr-administrator", "settings:Edit"], policies["POST /api/users/{id:int}/activate"]);
        Assert.Equal(["hr-administrator", "settings:Edit"], policies["POST /api/users/{id:int}/deactivate"]);
        Assert.Equal(["hr-administrator", "settings:Edit"], policies["POST /api/users/{id:int}/unlock"]);
        Assert.Equal(["hr-administrator", "settings:Edit"], policies["POST /api/users/{id:int}/link-employee"]);
        Assert.Equal(["hr-administrator", "settings:Edit"], policies["POST /api/users/{id:int}/password-reset"]);
        Assert.Equal(["hr-administrator", "settings:Create"], policies["POST /api/roles/"]);
        Assert.Equal(["hr-administrator", "settings:Edit"], policies["PUT /api/roles/{id:int}"]);
        Assert.Equal(["hr-administrator", "settings:Delete"], policies["DELETE /api/roles/{id:int}"]);
        Assert.Equal(["hr-administrator", "settings:Edit"], policies["POST /api/roles/{id:int}/deactivate"]);
        Assert.Equal(["hr-administrator", "settings:View"], policies["GET /api/role-permissions/"]);
        Assert.Equal(["hr-administrator", "settings:View"], policies["GET /api/role-permissions/{roleId:int}"]);
        Assert.Equal(["hr-administrator", "settings:Edit"], policies["PUT /api/role-permissions/{roleId:int}"]);
        Assert.Equal(["settings:View"], policies["GET /api/general-settings/"]);
        Assert.Equal(["hr-administrator", "settings:Edit"], policies["PUT /api/general-settings/"]);
    }

    [Fact]
    public void Six_organization_resources_map_eight_endpoints_each()
    {
        var endpoints = ApiEndpoints()
            .Where(endpoint => !IsEmployeeRoute(endpoint) && !IsEmployeeDocumentRoute(endpoint) && !IsLeaveRoute(endpoint) && !IsAttendanceRoute(endpoint) && !IsPayrollRoute(endpoint) && !IsRecruitmentRoute(endpoint) && !IsActivityRoute(endpoint) && !IsSettingsRoute(endpoint) && !IsExcelRoute(endpoint) && !IsBlacklistRoute(endpoint) && !IsSelfServiceRoute(endpoint) && !IsOrganizationLookupRoute(endpoint) && !IsKpiRoute(endpoint) && !IsGrievanceRoute(endpoint))
            .ToList();

        Assert.Equal(48, endpoints.Count);
    }

    [Fact]
    public void Employees_map_seventeen_endpoints_and_no_unpaged_list()
    {
        var endpoints = ApiEndpoints().Where(IsEmployeeRoute).ToList();

        Assert.Equal(17, endpoints.Count);
        Assert.DoesNotContain(
            endpoints,
            endpoint => endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains(HttpMethods.Get)
                && endpoint.RoutePattern.RawText!.TrimEnd('/') == "/api/employees");
    }

    [Fact]
    public void Employee_personal_and_bank_endpoints_have_their_own_permissions()
    {
        var policies = ApiEndpoints().Where(IsEmployeeRoute)
            .ToDictionary(
                endpoint => $"{endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Single()} {endpoint.RoutePattern.RawText}",
                endpoint => endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Single(data => data.Policy is not null).Policy);

        Assert.Equal("employees:ViewSensitive", policies["GET /api/employees/{id:int}/personal"]);
        Assert.Equal("employees:EditSensitive", policies["PUT /api/employees/{id:int}/personal"]);
        Assert.Equal("employees:ViewBank", policies["GET /api/employees/{id:int}/bank-account"]);
        Assert.Equal("employees:EditBank", policies["PUT /api/employees/{id:int}/bank-account"]);
        Assert.Equal("employees:View", policies["GET /api/employees/query"]);
        Assert.Equal("employees:View", policies["GET /api/employees/{id:int}"]);
        Assert.Equal("employees:View", policies["GET /api/employees/{id:int}/history"]);
        Assert.Equal("employees:Create", policies["GET /api/employees/next-code"]);
        Assert.Equal("employees:Create", policies["POST /api/employees/"]);
        Assert.Equal("employees:Edit", policies["PUT /api/employees/{id:int}"]);
        Assert.Equal("employees:Delete", policies["DELETE /api/employees/{id:int}"]);
        Assert.Equal("employees:Edit", policies["POST /api/employees/{id:int}/exit"]);
        Assert.Equal("employees:Edit", policies["POST /api/employees/{id:int}/reinstate"]);
        Assert.Equal("employees:Edit", policies["POST /api/employees/{id:int}/status"]);
        Assert.Equal("payroll:View", policies["GET /api/employees/{id:int}/salary-summary"]);
    }

    private Dictionary<string, string> PoliciesOf(Func<RouteEndpoint, bool> filter) => ApiEndpoints().Where(filter)
        .ToDictionary(
            endpoint => $"{endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Single()} {endpoint.RoutePattern.RawText}",
            endpoint => endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Single(data => data.Policy is not null).Policy!);

    [Fact]
    public void The_do_not_rehire_list_has_its_own_permissions_and_no_unpaged_list()
    {
        var policies = PoliciesOf(IsBlacklistRoute);

        Assert.Equal(4, policies.Count);
        Assert.DoesNotContain("GET /api/employee-blacklist/", policies.Keys);
        Assert.Equal("employees:ViewBlacklist", policies["GET /api/employee-blacklist/query"]);
        Assert.Equal("employees:ViewBlacklist", policies["GET /api/employee-blacklist/{id:int}"]);
        Assert.Equal("employees:EditBlacklist", policies["POST /api/employee-blacklist/"]);
        Assert.Equal("employees:EditBlacklist", policies["POST /api/employee-blacklist/{id:int}/lift"]);
        Assert.Equal("employees:ViewBlacklist", PoliciesOf(IsEmployeeRoute)["GET /api/employees/{id:int}/blacklist"]);
    }

    [Fact]
    public void The_employee_picker_has_its_own_policy_and_my_payslips_need_only_a_signed_in_user()
    {
        Assert.Equal("employee-lookup", PoliciesOf(IsEmployeeRoute)["GET /api/employees/lookup"]);

        var mine = PoliciesOf(IsSelfServiceRoute);
        Assert.Equal(2, mine.Count);
        Assert.All(mine.Values, policy => Assert.Equal("signed-in", policy));
        Assert.Contains("GET /api/me/payslips/", mine.Keys);
        Assert.Contains("GET /api/me/payslips/{id:int}", mine.Keys);
    }

    /// <summary>
    /// The policy <c>signed-in</c> lets every account through without a module grant, so the routes that carry it are pinned: a new
    /// endpoint cannot get it silently. Every endpoint of the host is checked, <c>/api/auth</c> included.
    /// </summary>
    [Fact]
    public void Only_the_known_routes_need_nothing_but_a_signed_in_user()
    {
        using var client = factory.CreateClient();
        var signedIn = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(data => data.Policy == "signed-in"))
            .Select(endpoint => $"{string.Join('|', endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])} {endpoint.RoutePattern.RawText}")
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
            [
                "GET /api/me/payslips/",
                "GET /api/me/payslips/{id:int}",
                "GET /api/notifications/query",
                "GET /api/organization/lookup",
                "GET /api/shifts/lookup",
                "POST /api/notifications/read-all",
                "POST /api/notifications/{id:long}/read",
            ],
            signedIn);
    }

    [Fact]
    public void The_organization_lookup_is_one_read_that_needs_only_a_signed_in_user()
    {
        var lookup = PoliciesOf(IsOrganizationLookupRoute);

        Assert.Equal(new Dictionary<string, string> { ["GET /api/organization/lookup"] = "signed-in" }, lookup);
    }

    [Fact]
    public async Task Every_policy_in_use_is_registered()
    {
        var provider = factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        var policies = ApiEndpoints()
            .SelectMany(endpoint => endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>())
            .Select(data => data.Policy!)
            .Distinct()
            .ToList();

        foreach (var policy in policies)
        {
            Assert.NotNull(await provider.GetPolicyAsync(policy));
        }

        Assert.Equal(
            [
                "activity:View",
                "attendance:Approve", "attendance:Create", "attendance:Delete", "attendance:Edit", "attendance:View",
                "dashboard:View",
                "employee-lookup",
                "employees:Create", "employees:Delete", "employees:Edit", "employees:EditBank", "employees:EditBlacklist", "employees:EditSensitive",
                "employees:View", "employees:ViewBank", "employees:ViewBlacklist", "employees:ViewSensitive",
                "excel:Export", "excel:Import", "excel:View",
                "grievance-case-team", "grievance-case-work", "grievance-employee-picker", "grievance-insight",
                "grievance-investigation:View",
                "grievance-management:Create", "grievance-management:Delete", "grievance-management:Edit", "grievance-management:View",
                "grievance-options",
                "grievance-review:Approve", "grievance-review:View",
                "grievance:Create", "grievance:View",
                "hr-administrator",
                "kpi-setup:Create", "kpi-setup:Delete", "kpi-setup:Edit", "kpi-setup:View", "kpi:Approve", "kpi:Create", "kpi:View",
                "leave:Approve", "leave:Create", "leave:Delete", "leave:Edit", "leave:View",
                "organization:Create", "organization:Delete", "organization:Edit", "organization:View",
                "payroll:Approve", "payroll:Create", "payroll:Edit", "payroll:View",
                "recruitment:Create", "recruitment:Delete", "recruitment:Edit", "recruitment:View",
                "settings:Create", "settings:Delete", "settings:Edit", "settings:View",
                "signed-in",
            ],
            policies.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void The_policy_of_each_endpoint_matches_its_http_method()
    {
        foreach (var endpoint in ApiEndpoints()
            .Where(endpoint => !IsEmployeeRoute(endpoint) && !IsEmployeeDocumentRoute(endpoint) && !IsLeaveRoute(endpoint) && !IsAttendanceRoute(endpoint) && !IsPayrollRoute(endpoint) && !IsRecruitmentRoute(endpoint) && !IsActivityRoute(endpoint) && !IsSettingsRoute(endpoint) && !IsExcelRoute(endpoint) && !IsBlacklistRoute(endpoint) && !IsSelfServiceRoute(endpoint) && !IsOrganizationLookupRoute(endpoint) && !IsKpiRoute(endpoint) && !IsGrievanceRoute(endpoint)))
        {
            var method = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Single();
            var isCommand = method == HttpMethods.Post && endpoint.RoutePattern.RawText!.Contains("/{id:int}/", StringComparison.Ordinal);
            var expected = method switch
            {
                "GET" => "organization:View",
                "POST" when isCommand => "organization:Edit",
                "POST" => "organization:Create",
                "PUT" => "organization:Edit",
                "DELETE" => "organization:Delete",
                _ => throw new InvalidOperationException($"Unexpected method {method}."),
            };

            var policy = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Single(data => data.Policy is not null).Policy;
            Assert.True(expected == policy, $"{endpoint.DisplayName}: expected {expected} but found {policy}");
        }
    }

    private Dictionary<string, string?> LeavePolicies() => ApiEndpoints().Where(IsLeaveRoute)
        .ToDictionary(
            endpoint => $"{endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Single()} {endpoint.RoutePattern.RawText}",
            endpoint => endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Single(data => data.Policy is not null).Policy);

    private static readonly string[] SeededPayrollPolicies = ["payroll:View", "payroll:Create", "payroll:Edit", "payroll:Approve"];

    private Dictionary<string, string?> PayrollPolicies() => ApiEndpoints().Where(IsPayrollRoute)
        .ToDictionary(
            endpoint => $"{endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Single()} {endpoint.RoutePattern.RawText}",
            endpoint => endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Single(data => data.Policy is not null).Policy);

    [Fact]
    public void Payroll_maps_thirty_three_endpoints_and_no_unpaged_salary_or_payslip_list()
    {
        var policies = PayrollPolicies();

        Assert.Equal(33, policies.Count);
        Assert.DoesNotContain("GET /api/salary-assignments/", policies.Keys);
        Assert.DoesNotContain("GET /api/payslips/", policies.Keys);
        Assert.Contains("GET /api/salary-assignments/query", policies.Keys);
        Assert.Contains("GET /api/payslips/query", policies.Keys);
        Assert.Contains("GET /api/salary-components/", policies.Keys);
        Assert.Contains("GET /api/salary-structures/", policies.Keys);
    }

    [Fact]
    public void Payroll_uses_only_the_actions_the_database_seed_has_and_no_payslip_route_writes()
    {
        var policies = PayrollPolicies();

        // The seed has no payroll Delete: View, Create, Edit and Approve only.
        Assert.All(policies.Values, policy => Assert.Contains(policy, SeededPayrollPolicies));
        Assert.DoesNotContain(PermissionModuleKeys("payroll"), key => key.EndsWith(":Delete", StringComparison.Ordinal));
        Assert.DoesNotContain(policies.Keys, key => key.Contains("/api/payslips", StringComparison.Ordinal) && !key.StartsWith("GET ", StringComparison.Ordinal));
        Assert.DoesNotContain(policies.Keys, key => key.Contains("/api/payslips", StringComparison.Ordinal) && key.Contains("{id:int}/", StringComparison.Ordinal));
    }

    private IEnumerable<string> PermissionModuleKeys(string module) =>
        ApiEndpoints().SelectMany(endpoint => endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>())
            .Select(data => data.Policy!)
            .Where(policy => policy.StartsWith(module + ":", StringComparison.Ordinal));

    [Fact]
    public void Payroll_endpoints_have_the_policy_of_who_may_use_them()
    {
        var policies = PayrollPolicies();

        Assert.Equal("payroll:View", policies["GET /api/payroll-runs/query"]);
        Assert.Equal("payroll:View", policies["GET /api/payroll-runs/{id:int}"]);
        Assert.Equal("payroll:Create", policies["POST /api/payroll-runs/"]);
        Assert.Equal("payroll:Edit", policies["PUT /api/payroll-runs/{id:int}"]);
        Assert.Equal("payroll:Edit", policies["DELETE /api/payroll-runs/{id:int}"]);
        Assert.Equal("payroll:Edit", policies["POST /api/payroll-runs/{id:int}/process"]);
        Assert.Equal("payroll:Approve", policies["POST /api/payroll-runs/{id:int}/approve"]);
        Assert.Equal("payroll:Approve", policies["POST /api/payroll-runs/{id:int}/mark-paid"]);
        Assert.Equal("payroll:Approve", policies["POST /api/payroll-runs/{id:int}/return-to-draft"]);
        Assert.Equal("payroll:View", policies["GET /api/payslips/query"]);
        Assert.Equal("payroll:View", policies["GET /api/payslips/{id:int}"]);
        Assert.Equal("payroll:View", policies["GET /api/salary-assignments/query"]);
        Assert.Equal("payroll:View", policies["GET /api/salary-assignments/{id:int}"]);
        Assert.Equal("payroll:Create", policies["POST /api/salary-assignments/"]);
        Assert.Equal("payroll:Edit", policies["PUT /api/salary-assignments/{id:int}"]);
        Assert.Equal("payroll:Edit", policies["DELETE /api/salary-assignments/{id:int}"]);
        Assert.Equal("payroll:Create", policies["POST /api/salary-components/"]);
        Assert.Equal("payroll:Edit", policies["DELETE /api/salary-components/{id:int}"]);
        Assert.Equal("payroll:Edit", policies["POST /api/salary-components/{id:int}/activate"]);
        Assert.Equal("payroll:Edit", policies["POST /api/salary-structures/{id:int}/deactivate"]);
        Assert.Equal("payroll:Edit", policies["DELETE /api/salary-structures/{id:int}"]);
    }

    [Fact]
    public void Leave_maps_twenty_seven_endpoints_and_no_unpaged_application_list()
    {
        var policies = LeavePolicies();

        Assert.Equal(27, policies.Count);
        Assert.DoesNotContain("GET /api/leave-applications/", policies.Keys);
        Assert.Contains("GET /api/leave-types/", policies.Keys);
        Assert.Contains("GET /api/leave-policies/", policies.Keys);
    }

    [Fact]
    public void Leave_workflow_endpoints_have_the_policy_of_who_may_use_them()
    {
        var policies = LeavePolicies();

        Assert.Equal("leave:View", policies["GET /api/leave-applications/query"]);
        Assert.Equal("leave:View", policies["GET /api/leave-applications/{id:int}"]);
        Assert.Equal("leave:Create", policies["POST /api/leave-applications/"]);
        Assert.Equal("leave:Edit", policies["PUT /api/leave-applications/{id:int}"]);
        Assert.Equal("leave:Delete", policies["DELETE /api/leave-applications/{id:int}"]);
        Assert.Equal("leave:Create", policies["POST /api/leave-applications/{id:int}/submit"]);
        Assert.Equal("leave:Create", policies["POST /api/leave-applications/{id:int}/cancel"]);
        Assert.Equal("leave:Approve", policies["POST /api/leave-applications/{id:int}/approve"]);
        Assert.Equal("leave:Approve", policies["POST /api/leave-applications/{id:int}/reject"]);
        Assert.Equal("leave:View", policies["GET /api/leave/balances"]);
        Assert.Equal("leave:View", policies["GET /api/leave/calendar"]);
        Assert.Equal("leave:Edit", policies["POST /api/leave-types/{id:int}/activate"]);
        Assert.Equal("leave:Edit", policies["POST /api/leave-policies/{id:int}/deactivate"]);
        Assert.Equal("leave:Delete", policies["DELETE /api/leave-types/{id:int}"]);
    }

    private Dictionary<string, string?> AttendancePolicies() => ApiEndpoints().Where(IsAttendanceRoute)
        .ToDictionary(
            endpoint => $"{endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Single()} {endpoint.RoutePattern.RawText}",
            endpoint => endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Single(data => data.Policy is not null).Policy);

    [Fact]
    public void Attendance_maps_thirty_two_endpoints_and_no_way_to_write_a_record_directly()
    {
        var policies = AttendancePolicies();

        Assert.Equal(32, policies.Count);
        Assert.DoesNotContain(policies.Keys, key => key.Contains("/api/attendance-records", StringComparison.Ordinal)
            && !key.StartsWith("GET ", StringComparison.Ordinal));
        Assert.DoesNotContain("GET /api/attendance-adjustments/", policies.Keys);
        Assert.DoesNotContain("PUT /api/attendance-adjustments/{id:int}", policies.Keys);
        Assert.DoesNotContain("GET /api/overtime-records/", policies.Keys);
    }

    [Fact]
    public void Attendance_endpoints_have_the_policy_of_who_may_use_them()
    {
        var policies = AttendancePolicies();

        Assert.Equal("attendance:Create", policies["POST /api/attendance/punches"]);
        Assert.Equal("attendance:View", policies["GET /api/attendance-records/query"]);
        Assert.Equal("attendance:View", policies["GET /api/attendance-records/{id:int}"]);
        Assert.Equal("attendance:View", policies["GET /api/attendance/overview"]);
        Assert.Equal("attendance:View", policies["GET /api/attendance/schedule"]);
        Assert.Equal("attendance:Create", policies["POST /api/attendance-adjustments/"]);
        Assert.Equal("attendance:Approve", policies["POST /api/attendance-adjustments/{id:int}/approve"]);
        Assert.Equal("attendance:Approve", policies["POST /api/attendance-adjustments/{id:int}/reject"]);
        Assert.Equal("attendance:Create", policies["POST /api/overtime-records/"]);
        Assert.Equal("attendance:Approve", policies["POST /api/overtime-records/{id:int}/approve"]);
        Assert.Equal("attendance:Approve", policies["POST /api/overtime-records/{id:int}/reject"]);
        Assert.Equal("attendance:Edit", policies["POST /api/shifts/{id:int}/activate"]);
        Assert.Equal("attendance:Delete", policies["DELETE /api/shifts/{id:int}"]);
        Assert.Equal("attendance:Edit", policies["POST /api/holidays/"]);
        Assert.Equal("attendance:Edit", policies["POST /api/shifts/"]);
        Assert.Equal("attendance:Edit", policies["PUT /api/holidays/{id:int}"]);
        Assert.Equal("attendance:View", policies["GET /api/shifts/{id:int}"]);
        Assert.Equal("attendance:View", policies["GET /api/shifts/"]);

        // The shift names of an employee profile: any signed-in user, like the organization lookup.
        Assert.Equal("signed-in", policies["GET /api/shifts/lookup"]);
    }
    private Dictionary<string, string?> ActivityPolicies() => ApiEndpoints().Where(IsActivityRoute)
        .ToDictionary(
            endpoint => $"{endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Single()} {endpoint.RoutePattern.RawText}",
            endpoint => endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Single(data => data.Policy is not null).Policy);


    private Dictionary<string, string?> RecruitmentPolicies() => ApiEndpoints().Where(IsRecruitmentRoute)
        .ToDictionary(
            endpoint => $"{endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Single()} {endpoint.RoutePattern.RawText}",
            endpoint => endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Single(data => data.Policy is not null).Policy);

    [Fact]
    public void Dashboard_activity_and_notifications_map_six_endpoints_with_their_policies()
    {
        var policies = ActivityPolicies();

        Assert.Equal(6, policies.Count);
        Assert.Equal("dashboard:View", policies["GET /api/dashboard/summary"]);
        Assert.Equal("activity:View", policies["GET /api/activity/query"]);
        Assert.Equal("activity:View", policies["GET /api/activity/{id:long}"]);
        Assert.Equal("signed-in", policies["GET /api/notifications/query"]);
        Assert.Equal("signed-in", policies["POST /api/notifications/{id:long}/read"]);
        Assert.Equal("signed-in", policies["POST /api/notifications/read-all"]);
    }

    [Fact]
    public void The_activity_log_is_append_only_so_no_route_creates_changes_or_deletes_an_entry()
    {
        var activityRoutes = ApiEndpoints()
            .Where(endpoint => endpoint.RoutePattern.RawText!.StartsWith("/api/activity", StringComparison.Ordinal))
            .Select(endpoint => endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Single())
            .ToList();

        Assert.NotEmpty(activityRoutes);
        Assert.All(activityRoutes, method => Assert.Equal(HttpMethods.Get, method));
    }

    [Fact]
    public void Notifications_have_no_route_to_create_change_or_delete_one_and_only_read_marking_writes()
    {
        var writes = ApiEndpoints()
            .Where(endpoint => endpoint.RoutePattern.RawText!.StartsWith("/api/notifications", StringComparison.Ordinal))
            .Select(endpoint => (Method: endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Single(), Route: endpoint.RoutePattern.RawText!))
            .Where(route => route.Method != HttpMethods.Get)
            .ToList();

        Assert.All(writes, route => Assert.Equal(HttpMethods.Post, route.Method));
        Assert.Equal(["/api/notifications/read-all", "/api/notifications/{id:long}/read"], writes.Select(route => route.Route).Order(StringComparer.Ordinal));
    }
    [Fact]

    public void Recruitment_maps_thirty_four_endpoints_and_has_no_approval_permission()
    {
        var policies = RecruitmentPolicies();

        Assert.Equal(34, policies.Count);
        Assert.DoesNotContain(policies.Values, policy => policy!.EndsWith(":Approve", StringComparison.Ordinal));
    }

    [Fact]
    public void Recruitment_endpoints_have_the_policy_of_who_may_use_them()
    {
        var policies = RecruitmentPolicies();

        Assert.Equal("recruitment:View", policies["GET /api/job-openings/query"]);
        Assert.Equal("recruitment:View", policies["GET /api/job-openings/{id:int}/pipeline"]);
        Assert.Equal("recruitment:Create", policies["POST /api/job-openings/"]);
        Assert.Equal("recruitment:Edit", policies["PUT /api/job-openings/{id:int}"]);
        Assert.Equal("recruitment:Delete", policies["DELETE /api/job-openings/{id:int}"]);
        Assert.Equal("recruitment:Edit", policies["POST /api/job-openings/{id:int}/open"]);
        Assert.Equal("recruitment:Edit", policies["POST /api/job-openings/{id:int}/reopen"]);
        Assert.Equal("recruitment:Edit", policies["POST /api/job-openings/{id:int}/hold"]);
        Assert.Equal("recruitment:Edit", policies["POST /api/job-openings/{id:int}/close"]);
        Assert.Equal("recruitment:View", policies["GET /api/candidates/query"]);
        Assert.Equal("recruitment:Create", policies["POST /api/candidates/"]);
        Assert.Equal("recruitment:Delete", policies["DELETE /api/candidates/{id:int}"]);
        Assert.Equal("recruitment:Create", policies["POST /api/applications/"]);
        Assert.Equal("recruitment:Edit", policies["POST /api/applications/{id:int}/stage"]);
        Assert.Equal("recruitment:Delete", policies["DELETE /api/applications/{id:int}"]);
        Assert.Equal("recruitment:Create", policies["POST /api/interviews/"]);
        Assert.Equal("recruitment:Edit", policies["PUT /api/interviews/{id:int}"]);
        Assert.Equal("recruitment:Edit", policies["POST /api/interviews/{id:int}/outcome"]);
        Assert.Equal("recruitment:Edit", policies["POST /api/interviews/{id:int}/cancel"]);
        Assert.Equal("recruitment:Delete", policies["DELETE /api/interviews/{id:int}"]);
        Assert.Equal("recruitment:View", policies["GET /api/recruitment/pipeline"]);
        Assert.Equal("recruitment:View", policies["GET /api/recruitment/counts"]);
    }

    [Fact]
    public void Excel_maps_nine_endpoints_with_the_policy_of_what_they_do()
    {
        var policies = ApiEndpoints().Where(IsExcelRoute)
            .ToDictionary(
                endpoint => $"{endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Single()} {endpoint.RoutePattern.RawText}",
                endpoint => endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Single(data => data.Policy is not null).Policy);

        Assert.Equal(9, policies.Count);
        Assert.Equal("excel:View", policies["GET /api/excel/modules"]);
        Assert.Equal("excel:Import", policies["GET /api/excel/templates/{module}"]);
        Assert.Equal("excel:Import", policies["POST /api/excel/imports/{module}/inspect"]);
        Assert.Equal("excel:Import", policies["POST /api/excel/imports/{module}/validate"]);
        Assert.Equal("excel:Import", policies["POST /api/excel/imports/{batchId}/execute"]);
        Assert.Equal("excel:View", policies["GET /api/excel/imports/history"]);
        Assert.Equal("excel:View", policies["GET /api/excel/imports/{batchId}"]);
        Assert.Equal("excel:View", policies["GET /api/excel/imports/{batchId}/error-report"]);
        Assert.Equal("excel:Export", policies["POST /api/excel/exports/{module}"]);
    }
}
