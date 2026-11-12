using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Apex.Hrms.Application.Security;
using Apex.Hrms.Application.Settings;
using Apex.Hrms.TestSupport;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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

    private static readonly ConditionalWeakTable<WebApplicationFactory<Program>, ConcurrentDictionary<string, Lazy<WebApplicationFactory<Program>>>> Hosts = new();

    /// <summary>
    /// Starting a host costs seconds, so tests that only differ in the caller share one: the host built by <paramref name="build"/>
    /// is kept per <paramref name="factory"/> (the class fixture, disposed with it) and <paramref name="key"/>. Use it only for
    /// hosts without per-test state; a host that holds a recording fake must be built per test.
    /// </summary>
    public static WebApplicationFactory<Program> Cached(
        WebApplicationFactory<Program> factory,
        string key,
        Func<WebApplicationFactory<Program>, WebApplicationFactory<Program>> build) =>
        Hosts.GetOrCreateValue(factory).GetOrAdd(key, _ => new Lazy<WebApplicationFactory<Program>>(() => build(factory))).Value;

    public static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        BrowserHandler.CreateClient(Cached(factory, "signed-in-default", shared => Customize(shared)).Server.CreateHandler(), out _);

    /// <summary>
    /// The signed-in caller is account 1. Without arguments it is an active HR Administrator whose role holds every grant;
    /// pass an account and the grants of its role to test what the permission check does with them (no database is read).
    /// </summary>
    public static WebApplicationFactory<Program> Customize(
        WebApplicationFactory<Program> factory,
        AccountSnapshot? account = null,
        IEnumerable<string>? grants = null,
        bool grantsFromHeader = false) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<IAccountDirectory>(provider =>
                {
                    var caller = account ?? AccessFakes.Account();

                    // The permission check caches the grants per role and role row version: a grant set that comes from the
                    // request gets its own row version, as a changed role would in the database.
                    return new FakeAccountDirectory(grantsFromHeader
                        ? caller with { RoleRowVersion = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(string.Join(',', GrantsOfRequest(provider).Order(StringComparer.Ordinal))))[..8] }
                        : caller);
                });
                services.AddScoped<IPermissionStore>(provider => new FakeGrantStore(
                    grantsFromHeader ? GrantsOfRequest(provider) : grants ?? AccessFakes.AllGrants));
                services
                    .AddAuthentication(options =>
                    {
                        options.DefaultScheme = Scheme;
                        options.DefaultAuthenticateScheme = Scheme;
                        options.DefaultChallengeScheme = Scheme;
                    })
                    .AddScheme<AuthenticationSchemeOptions, AlwaysSignedInHandler>(Scheme, _ => { });
            });
        });

    /// <summary>The header a test client sends to choose the grants of the role per request (see <c>grantsFromHeader</c>).</summary>
    public const string GrantsHeader = "X-Test-Grants";

    /// <summary>Sent for a role without any grant: an empty header value would be dropped.</summary>
    private const string NoGrants = "-";

    private static IEnumerable<string> GrantsOfRequest(IServiceProvider provider) =>
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext?.Request.Headers[GrantsHeader] is { Count: > 0 } values
            ? values.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries).Where(grant => grant != NoGrants)
            : AccessFakes.AllGrants;

    /// <summary>Adds the grants header to every request, so one host serves callers whose roles hold different grants.</summary>
    public static HttpClient CreateClientWithGrants(WebApplicationFactory<Program> host, IEnumerable<string> grants) =>
        BrowserHandler.CreateClient(new GrantsHandler(string.Join(',', grants.DefaultIfEmpty(NoGrants)), host.Server.CreateHandler()), out _);

    private sealed class GrantsHandler(string grants, HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.Remove(GrantsHeader);
            request.Headers.Add(GrantsHeader, grants);
            return base.SendAsync(request, cancellationToken);
        }
    }

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
