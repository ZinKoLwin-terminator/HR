using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Apex.Hrms.Api.Auth;
using Apex.Hrms.TestSupport;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Apex.Hrms.Api.Tests;

/// <summary>
/// The security shape of the API that needs no database: which endpoints are anonymous, deny by default, the cookie
/// settings, antiforgery on unsafe methods and the fail-closed reset flow. Nothing here opens a database connection.
/// </summary>
public sealed class AuthEndpointSecurityTests(WebApplicationFactory<Program> fixture) : IClassFixture<WebApplicationFactory<Program>>
{
    /// <summary>One Development host for the whole class (starting a host is the slow part); the tests only send independent requests through their own clients.</summary>
    private WebApplicationFactory<Program> Development() =>
        SignedInHost.Cached(fixture, "development", factory => factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development")));

    private static WebApplicationFactory<Program> DevelopmentWithCookie(string cookieName) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Auth:DevelopmentCookieName", cookieName);
        });

    private static WebApplicationFactory<Program> Production() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:Hrms", "Server=.;Database=Placeholder;Integrated Security=True");
            builder.UseSetting("AllowedHosts", "api.example.test");
            builder.UseSetting("https_port", "443");
        });

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>())!;

    [Fact]
    public void Only_the_four_auth_endpoints_and_the_openapi_document_allow_anonymous_callers()
    {
        var factory = Development();
        using var client = factory.CreateClient();

        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();
        var anonymous = endpoints
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(endpoint => endpoint.RoutePattern.RawText!)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
            ["/api/auth/antiforgery", "/api/auth/forgot-password", "/api/auth/login", "/api/auth/reset-password", "/openapi/{documentName}.json"],
            anonymous);
    }

    [Fact]
    public async Task Deny_by_default_is_a_fallback_policy_and_every_named_policy_requires_a_signed_in_user()
    {
        var factory = Development();
        using var client = factory.CreateClient();
        var provider = factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        var fallback = await provider.GetFallbackPolicyAsync();
        Assert.NotNull(fallback);
        Assert.Contains(fallback.Requirements, requirement => requirement is Microsoft.AspNetCore.Authorization.Infrastructure.DenyAnonymousAuthorizationRequirement);

        var named = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .SelectMany(endpoint => endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>())
            .Select(data => data.Policy)
            .Where(policy => policy is not null)
            .Distinct()
            .ToList();
        Assert.NotEmpty(named);
        foreach (var name in named)
        {
            var policy = await provider.GetPolicyAsync(name!);
            Assert.Contains(policy!.Requirements, requirement => requirement is Microsoft.AspNetCore.Authorization.Infrastructure.DenyAnonymousAuthorizationRequirement);
        }
    }

    [Theory]
    [InlineData("/api/companies/query")]
    [InlineData("/api/employees/1")]
    [InlineData("/api/auth/me")]
    public async Task An_anonymous_read_is_a_401_problem_not_a_redirect(string path)
    {
        var factory = Development();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Unauthorized", (await ReadAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task An_anonymous_write_with_a_valid_antiforgery_token_is_a_401()
    {
        var factory = Development();
        using var client = BrowserHandler.CreateClient(factory.Server.CreateHandler(), out _);

        var response = await client.PostAsJsonAsync("/api/companies", new { });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Unauthorized", (await ReadAsync(response)).GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("POST", "/api/auth/login")]
    [InlineData("POST", "/api/auth/forgot-password")]
    [InlineData("POST", "/api/auth/reset-password")]
    [InlineData("POST", "/api/auth/logout")]
    [InlineData("POST", "/api/companies")]
    [InlineData("PUT", "/api/companies/1")]
    [InlineData("PATCH", "/api/companies/1")]
    [InlineData("DELETE", "/api/companies/1")]
    public async Task An_unsafe_request_without_an_antiforgery_token_is_a_400(string method, string path)
    {
        var factory = Development();
        using var client = BrowserHandler.CreateClient(factory.Server.CreateHandler(), out _, sendXsrfHeader: false);

        using var request = new HttpRequestMessage(new HttpMethod(method), path) { Content = JsonContent.Create(new { }) };
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal("Validation", body.GetProperty("code").GetString());
        Assert.Equal(AntiforgeryMiddleware.RejectedMessage, body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task A_wrong_antiforgery_token_is_a_400()
    {
        var factory = Development();
        using var client = BrowserHandler.CreateClient(factory.Server.CreateHandler(), out _, sendXsrfHeader: false);
        (await client.GetAsync("/api/auth/antiforgery")).EnsureSuccessStatusCode();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login") { Content = JsonContent.Create(new { }) };
        request.Headers.Add("X-XSRF-TOKEN", "not-a-token");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_antiforgery_endpoint_is_anonymous_and_sets_a_readable_token_cookie_and_a_hidden_secret_cookie()
    {
        var factory = Development();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/auth/antiforgery");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var cookies = response.Headers.GetValues("Set-Cookie").ToList();
        var token = Assert.Single(cookies, cookie => cookie.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal));
        Assert.DoesNotContain("httponly", token, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", token, StringComparison.OrdinalIgnoreCase);
        var secret = Assert.Single(cookies, cookie => cookie.StartsWith("hrms-dev-antiforgery=", StringComparison.Ordinal));
        Assert.Contains("httponly", secret, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task In_development_the_cookie_name_is_configurable_and_has_no_host_prefix()
    {
        using var factory = DevelopmentWithCookie("custom-cookie");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/auth/antiforgery");

        Assert.Contains(response.Headers.GetValues("Set-Cookie"), cookie => cookie.StartsWith("custom-cookie-antiforgery=", StringComparison.Ordinal));
        var options = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(IdentityConstants.ApplicationScheme);
        Assert.Equal("custom-cookie", options.Cookie.Name);
        Assert.DoesNotContain("__Host-", options.Cookie.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Outside_development_the_session_cookie_is_host_prefixed_http_only_secure_and_strict()
    {
        using var factory = Production();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://api.example.test") });

        var options = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(IdentityConstants.ApplicationScheme);

        Assert.Equal("__Host-hrms", options.Cookie.Name);
        Assert.True(options.Cookie.HttpOnly);
        Assert.Equal(CookieSecurePolicy.Always, options.Cookie.SecurePolicy);
        Assert.Equal(SameSiteMode.Strict, options.Cookie.SameSite);
        Assert.Equal("/", options.Cookie.Path);
        Assert.Null(options.Cookie.Domain);
        Assert.True(options.SlidingExpiration);
    }

    [Fact]
    public async Task Outside_development_the_token_cookies_are_secure_strict_and_the_secret_one_is_host_prefixed()
    {
        using var factory = Production();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://api.example.test") });

        var response = await client.GetAsync("/api/auth/antiforgery");

        var cookies = response.Headers.GetValues("Set-Cookie").ToList();
        var token = Assert.Single(cookies, cookie => cookie.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal));
        Assert.Contains("secure", token, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", token, StringComparison.OrdinalIgnoreCase);
        var secret = Assert.Single(cookies, cookie => cookie.StartsWith("__Host-hrms-antiforgery=", StringComparison.Ordinal));
        Assert.Contains("secure", secret, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", secret, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", secret, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", secret, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("known@example.test")]
    [InlineData("unknown@example.test")]
    public async Task Outside_development_forgot_password_fails_closed_when_email_is_not_configured(string email)
    {
        using var factory = Production();
        using var client = BrowserHandler.CreateClient(factory.Server.CreateHandler(), out var handler, origin: new Uri("https://api.example.test/"));

        var response = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email });

        // The same answer for every address, decided before any account lookup (no database is touched here).
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(AuthFlows.EmailNotConfiguredMessage, (await ReadAsync(response)).GetProperty("detail").GetString());
        Assert.NotNull(handler.XsrfToken);
    }

    [Fact]
    public void The_appsettings_files_hold_no_smtp_credentials_and_no_secrets()
    {
        var factory = Development();
        using var client = factory.CreateClient();
        var configuration = factory.Services.GetRequiredService<IConfiguration>();

        Assert.Null(configuration["Email:Smtp:Password"]);
        Assert.Null(configuration["Email:Smtp:UserName"]);
        Assert.Null(configuration["Email:Smtp:Host"]);
    }
}
