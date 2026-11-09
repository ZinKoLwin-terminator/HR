using System.Security.Claims;
using System.Text.Encodings.Web;
using Apex.Hrms.TestSupport;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Apex.Hrms.Api.Tests;

/// <summary>
/// A host whose default authentication always signs in a fictional user, so tests of routing, binding and the error
/// contract (which never reach the database) get past the "signed in" requirement without a database.
/// </summary>
internal static class SignedInHost
{
    public const string Scheme = "TestSignedIn";

    public static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        BrowserHandler.CreateClient(Customize(factory).Server.CreateHandler(), out _);

    public static WebApplicationFactory<Program> Customize(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureTestServices(services => services
                .AddAuthentication(options =>
                {
                    options.DefaultScheme = Scheme;
                    options.DefaultAuthenticateScheme = Scheme;
                    options.DefaultChallengeScheme = Scheme;
                })
                .AddScheme<AuthenticationSchemeOptions, AlwaysSignedInHandler>(Scheme, _ => { }));
        });

    private sealed class AlwaysSignedInHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "tst-subject"),
                    new Claim(ClaimTypes.Name, "TST- Test User"),
                    new Claim("hrms:uid", "1"),
                ],
                SignedInHost.Scheme);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SignedInHost.Scheme)));
        }
    }
}
