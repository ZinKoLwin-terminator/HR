using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Apex.Hrms.Api.Auth;
using Apex.Hrms.Application.Settings;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.Api.Tests;

/// <summary>
/// <c>POST /api/users/{id}/password-reset</c>: status mapping, authorization and the per-user throttle. The handler is replaced
/// by a fake (its rules are covered by the Application tests, which also check that the mail reaches the right user).
/// </summary>
public sealed class PasswordResetEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public PasswordResetEndpointTests(WebApplicationFactory<Program> factory) => this.factory = factory;

    private HttpClient ClientFor(string roleName, FakeResetHandler handler)
    {
        var account = AccessFakes.Account(roleName);
        var host = SignedInHost.Customize(factory, account, AccessFakes.AllGrants).WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddScoped<IUserPasswordResetHandler>(_ => handler)));
        return BrowserHandler.CreateClient(host.Server.CreateHandler(), out _);
    }

    [Fact]
    public async Task An_administrator_gets_202_with_no_body_and_the_handler_runs_once_for_that_user()
    {
        var handler = new FakeResetHandler();
        using var client = ClientFor("HR Administrator", handler);

        var response = await client.PostAsync("/api/users/7/password-reset", content: null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
        Assert.Equal([7], handler.Calls);
    }

    [Fact]
    public async Task An_empty_json_object_body_is_accepted_too()
    {
        var handler = new FakeResetHandler();
        using var client = ClientFor("HR Administrator", handler);

        var response = await client.PostAsJsonAsync("/api/users/8/password-reset", new { });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task A_caller_who_is_not_an_administrator_gets_403_and_nothing_is_sent()
    {
        var handler = new FakeResetHandler();
        using var client = ClientFor("HR Officer", handler);

        var response = await client.PostAsync("/api/users/7/password-reset", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(handler.Calls);
    }

    [Theory]
    [InlineData(ErrorType.Conflict, HttpStatusCode.Conflict, "Conflict")]
    [InlineData(ErrorType.NotFound, HttpStatusCode.NotFound, "NotFound")]
    [InlineData(ErrorType.Unavailable, HttpStatusCode.ServiceUnavailable, "Unknown")]
    public async Task A_refusal_of_the_handler_keeps_its_status_and_code_with_the_message(ErrorType type, HttpStatusCode status, string code)
    {
        var handler = new FakeResetHandler
        {
            Failure = type switch
            {
                ErrorType.Conflict => AppError.Conflict("Activate the account before sending a password reset."),
                ErrorType.NotFound => AppError.NotFound("User was not found."),
                _ => AppError.Unavailable("Not configured."),
            },
        };
        using var client = ClientFor("HR Administrator", handler);

        var response = await client.PostAsync("/api/users/7/password-reset", content: null);

        Assert.Equal(status, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<JsonElement>())!;
        Assert.Equal(code, body.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("detail").GetString()));
    }

    [Fact]
    public async Task A_second_reset_for_the_same_user_within_a_minute_is_429_with_Retry_After_but_another_user_is_fine()
    {
        var handler = new FakeResetHandler();
        using var client = ClientFor("HR Administrator", handler);

        var first = await client.PostAsync("/api/users/7/password-reset", content: null);
        var second = await client.PostAsync("/api/users/7/password-reset", content: null);
        var other = await client.PostAsync("/api/users/9/password-reset", content: null);

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.True(second.Headers.RetryAfter?.Delta is { } delta && delta > TimeSpan.Zero && delta <= PasswordResetThrottle.Window);
        Assert.Equal("Unknown", (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Accepted, other.StatusCode);
        Assert.Equal([7, 9], handler.Calls);
    }

    [Fact]
    public async Task A_refused_reset_does_not_use_up_the_allowance()
    {
        var handler = new FakeResetHandler { Failure = AppError.Conflict("Activate the account before sending a password reset.") };
        using var client = ClientFor("HR Administrator", handler);

        var first = await client.PostAsync("/api/users/7/password-reset", content: null);
        handler.Failure = null;
        var second = await client.PostAsync("/api/users/7/password-reset", content: null);

        Assert.Equal(HttpStatusCode.Conflict, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
    }

    [Fact]
    public async Task A_failure_after_the_mail_went_out_keeps_the_allowance_used()
    {
        var handler = new FakeResetHandler { Failure = AppError.Unavailable("Saving failed."), MailSent = true };
        using var client = ClientFor("HR Administrator", handler);

        var first = await client.PostAsync("/api/users/7/password-reset", content: null);
        handler.Failure = null;
        var second = await client.PostAsync("/api/users/7/password-reset", content: null);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
    }

    [Fact]
    public async Task One_caller_is_capped_across_many_users_with_429_and_Retry_After()
    {
        var handler = new FakeResetHandler();
        using var client = ClientFor("HR Administrator", handler);

        for (var user = 1; user <= PasswordResetThrottle.CallerLimit; user++)
        {
            Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsync($"/api/users/{user}/password-reset", content: null)).StatusCode);
        }

        var over = await client.PostAsync("/api/users/999/password-reset", content: null);

        Assert.Equal(HttpStatusCode.TooManyRequests, over.StatusCode);
        Assert.True(over.Headers.RetryAfter?.Delta is { } delta && delta > TimeSpan.Zero && delta <= PasswordResetThrottle.CallerWindow);
        Assert.DoesNotContain(999, handler.Calls);
    }

    [Fact]
    public void The_caller_cap_counts_a_window_and_a_release_gives_one_back()
    {
        var clock = new ManualTime();
        var throttle = new PasswordResetThrottle(clock);
        for (var i = 0; i < PasswordResetThrottle.CallerLimit; i++)
        {
            Assert.True(throttle.TryAcquireForCaller(1, out _));
        }

        Assert.False(throttle.TryAcquireForCaller(1, out _));
        Assert.True(throttle.TryAcquireForCaller(2, out _));
        throttle.ReleaseForCaller(1);
        Assert.True(throttle.TryAcquireForCaller(1, out _));
        Assert.False(throttle.TryAcquireForCaller(1, out var wait));
        Assert.Equal(PasswordResetThrottle.CallerWindow, wait);
        clock.Advance(PasswordResetThrottle.CallerWindow);
        Assert.True(throttle.TryAcquireForCaller(1, out _));
    }

    [Fact]
    public void The_throttle_frees_a_user_after_a_minute()
    {
        var clock = new ManualTime();
        var throttle = new PasswordResetThrottle(clock);

        Assert.True(throttle.TryAcquire(1, out _));
        clock.Advance(TimeSpan.FromSeconds(59));
        Assert.False(throttle.TryAcquire(1, out var wait));
        Assert.Equal(TimeSpan.FromSeconds(1), wait);
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True(throttle.TryAcquire(1, out _));
    }

    private sealed class FakeResetHandler : IUserPasswordResetHandler
    {
        public List<int> Calls { get; } = [];

        public AppError? Failure { get; set; }

        public bool MailSent { get; set; }

        public Task<Result> HandleAsync(int id, CancellationToken cancellationToken)
        {
            Calls.Add(id);
            return Task.FromResult(Failure is { } error ? Result.Failure(error) : Result.Success());
        }
    }

    private sealed class ManualTime : TimeProvider
    {
        private DateTimeOffset now = new(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan by) => now += by;
    }
}
