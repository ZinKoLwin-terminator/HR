using System.Net;
using Apex.Hrms.Api.Hosting;
using Apex.Hrms.Infrastructure.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Apex.Hrms.Api.Tests;

/// <summary>
/// The OpenAPI document and Swagger UI are off outside Development unless OpenApi:Enabled is set; when on, they are
/// anonymous, obey the host filter, and Swagger UI sends the antiforgery header like the Angular client.
/// </summary>
public sealed class ApiDocumentationTests
{
    private const string Host = "api.example.test";

    private static WebApplicationFactory<Program> Production(bool? openApiEnabled) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:Hrms", "Server=.;Database=Placeholder;Integrated Security=True");
            builder.UseSetting("https_port", "443");
            builder.UseSetting("AllowedHosts", Host);
            builder.UseSetting(GrievancePseudonymSettings.KeyName, TestSecrets.NewPseudonymKey());
            if (openApiEnabled is { } enabled)
            {
                builder.UseSetting(ApiDocumentation.EnabledKey, enabled ? "true" : "false");
            }
        });

    private static HttpClient Client(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri($"https://{Host}"),
            AllowAutoRedirect = false,
        });

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    public async Task Outside_development_the_document_and_swagger_ui_are_off_by_default(bool? enabled)
    {
        using var factory = Production(enabled);
        using var client = Client(factory);

        var document = await client.GetAsync(ApiDocumentation.DocumentPath);
        var ui = await client.GetAsync("/swagger/index.html");

        Assert.NotEqual(HttpStatusCode.OK, document.StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, ui.StatusCode);
    }

    [Fact]
    public async Task When_enabled_the_document_is_anonymous_and_lists_the_api()
    {
        using var factory = Production(openApiEnabled: true);
        using var client = Client(factory);

        var response = await client.GetAsync(ApiDocumentation.DocumentPath);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("\"/api/auth/login\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task When_enabled_swagger_ui_is_served_and_copies_the_xsrf_cookie_into_the_header()
    {
        using var factory = Production(openApiEnabled: true);
        using var client = Client(factory);

        var page = await client.GetAsync("/swagger/index.html");
        var script = await client.GetAsync("/swagger/index.js");
        var served = await page.Content.ReadAsStringAsync() + await script.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains(ApiDocumentation.DocumentPath, served, StringComparison.Ordinal);
        Assert.Contains("X-XSRF-TOKEN", served, StringComparison.Ordinal);
    }

    [Fact]
    public async Task When_enabled_swagger_ui_still_obeys_the_host_filter()
    {
        using var factory = Production(openApiEnabled: true);
        using var client = Client(factory);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/swagger/index.html");
        request.Headers.Host = "evil.example";

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
