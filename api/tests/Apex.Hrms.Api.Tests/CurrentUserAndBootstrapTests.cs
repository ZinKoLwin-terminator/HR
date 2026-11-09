using System.Security.Claims;
using Apex.Hrms.Api.Auth;
using Apex.Hrms.Api.Cli;
using Apex.Hrms.Application.Security;
using Apex.Hrms.Domain.Common;
using Microsoft.AspNetCore.Http;

namespace Apex.Hrms.Api.Tests;

public sealed class CurrentUserAndBootstrapTests
{
    private static HttpContextCurrentUser UserFor(ClaimsPrincipal? principal)
    {
        var context = new DefaultHttpContext();
        if (principal is not null)
        {
            context.User = principal;
        }

        return new HttpContextCurrentUser(new HttpContextAccessor { HttpContext = context });
    }

    private static ClaimsPrincipal SignedIn(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, HrmsClaims.AuthenticationType));

    [Fact]
    public void The_current_user_reads_the_account_employee_and_name_from_the_claims()
    {
        var user = UserFor(SignedIn(
            new Claim(HrmsClaims.AccountId, "17"),
            new Claim(HrmsClaims.EmployeeId, "23"),
            new Claim(ClaimTypes.Name, "TST- Test Person")));

        Assert.True(user.IsAuthenticated);
        Assert.False(user.IsSystem);
        Assert.Equal(17, user.UserId);
        Assert.Equal(23, user.EmployeeId);
        Assert.Equal("TST- Test Person", user.DisplayName);
    }

    [Fact]
    public void The_built_in_administrator_has_no_employee()
    {
        var user = UserFor(SignedIn(new Claim(HrmsClaims.AccountId, "1"), new Claim(ClaimTypes.Name, "TST- Admin")));

        Assert.Equal(1, user.UserId);
        Assert.Null(user.EmployeeId);
    }

    [Fact]
    public void An_anonymous_request_has_no_user_id_and_is_never_the_system_identity()
    {
        var user = UserFor(principal: null);

        Assert.False(user.IsAuthenticated);
        Assert.False(user.IsSystem);
        Assert.Null(user.UserId);
        Assert.Null(user.EmployeeId);
        Assert.Equal(HttpContextCurrentUser.AnonymousName, user.DisplayName);
    }

    [Fact]
    public void A_principal_that_is_not_authenticated_gets_no_user_id_even_with_claims()
    {
        var user = UserFor(new ClaimsPrincipal(new ClaimsIdentity([new Claim(HrmsClaims.AccountId, "5")])));

        Assert.False(user.IsAuthenticated);
        Assert.Null(user.UserId);
    }

    [Fact]
    public void Without_a_request_the_user_is_anonymous()
    {
        var user = new HttpContextCurrentUser(new HttpContextAccessor());

        Assert.False(user.IsAuthenticated);
        Assert.Null(user.UserId);
    }

    [Fact]
    public void A_malformed_account_claim_is_no_user_id()
    {
        var user = UserFor(SignedIn(new Claim(HrmsClaims.AccountId, "abc")));

        Assert.Null(user.UserId);
    }

    // ---- bootstrap-admin ----

    private sealed class ScriptedTerminal(bool interactive, params string?[] answers) : ITerminal
    {
        private readonly Queue<string?> queue = new(answers);

        public List<string> Output { get; } = [];

        public bool IsInteractive => interactive;

        public void WriteLine(string text) => Output.Add(text);

        public string? ReadLine(string prompt) => queue.Dequeue();

        public string? ReadSecret(string prompt) => queue.Dequeue();
    }

    private sealed class FakeStore(bool administratorExists) : IAdministratorStore
    {
        public List<(string Email, string Name, string Password)> Created { get; } = [];

        public Task<bool> ActiveAdministratorExistsAsync(CancellationToken cancellationToken) => Task.FromResult(administratorExists);

        public Task<Result> CreateAdministratorAsync(string email, string displayName, string password, CancellationToken cancellationToken)
        {
            Created.Add((email, displayName, password));
            return Task.FromResult(Result.Success());
        }
    }

    private const string GoodPassword = "correct horse battery";

    [Fact]
    public async Task The_command_refuses_when_an_active_administrator_exists_and_creates_nothing()
    {
        var store = new FakeStore(administratorExists: true);
        var terminal = new ScriptedTerminal(true, "yes", GoodPassword, GoodPassword);
        var command = new BootstrapAdminCommand(new AdministratorBootstrap(store), terminal, "test target");

        var exit = await command.ExecuteAsync(["--email", "tst-admin@example.test"], default);

        Assert.Equal(BootstrapAdminCommand.Failed, exit);
        Assert.Empty(store.Created);
        Assert.Contains(terminal.Output, line => line.Contains(AdministratorBootstrap.AlreadyExistsMessage, StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_command_creates_the_administrator_from_a_hidden_prompt_and_never_prints_the_password()
    {
        var store = new FakeStore(administratorExists: false);
        var terminal = new ScriptedTerminal(true, "yes", GoodPassword, GoodPassword);
        var command = new BootstrapAdminCommand(new AdministratorBootstrap(store), terminal, "test target");

        var exit = await command.ExecuteAsync(["--email", "tst-admin@example.test"], default);

        Assert.Equal(BootstrapAdminCommand.Succeeded, exit);
        var created = Assert.Single(store.Created);
        Assert.Equal(("tst-admin@example.test", BootstrapAdminCommand.DefaultDisplayName, GoodPassword), created);
        Assert.DoesNotContain(terminal.Output, line => line.Contains(GoodPassword, StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_command_shows_the_target_and_stops_unless_the_answer_is_yes()
    {
        var store = new FakeStore(administratorExists: false);
        var terminal = new ScriptedTerminal(true, "no");
        var command = new BootstrapAdminCommand(new AdministratorBootstrap(store), terminal, "database 'HR_X'");

        var exit = await command.ExecuteAsync(["--email", "tst-admin@example.test"], default);

        Assert.Equal(BootstrapAdminCommand.Failed, exit);
        Assert.Empty(store.Created);
        Assert.Contains("Target: database 'HR_X'", terminal.Output);
    }

    [Fact]
    public async Task The_command_stops_when_the_two_passwords_differ()
    {
        var store = new FakeStore(administratorExists: false);
        var terminal = new ScriptedTerminal(true, "yes", GoodPassword, "something else entirely");
        var command = new BootstrapAdminCommand(new AdministratorBootstrap(store), terminal, "t");

        var exit = await command.ExecuteAsync(["--email", "tst-admin@example.test"], default);

        Assert.Equal(BootstrapAdminCommand.Failed, exit);
        Assert.Empty(store.Created);
    }

    [Fact]
    public async Task The_command_needs_an_interactive_terminal_so_a_password_cannot_be_piped()
    {
        var store = new FakeStore(administratorExists: false);
        var command = new BootstrapAdminCommand(new AdministratorBootstrap(store), new ScriptedTerminal(false), "t");

        var exit = await command.ExecuteAsync(["--email", "tst-admin@example.test"], default);

        Assert.Equal(BootstrapAdminCommand.UsageError, exit);
        Assert.Empty(store.Created);
    }

    [Theory]
    [InlineData]
    [InlineData("--email")]
    [InlineData("--name", "Someone")]
    [InlineData("--email", "tst-admin@example.test", "--password", "x")]
    public async Task The_command_has_no_password_argument_and_requires_an_email(params string[] args)
    {
        var store = new FakeStore(administratorExists: false);
        var terminal = new ScriptedTerminal(true);
        var command = new BootstrapAdminCommand(new AdministratorBootstrap(store), terminal, "t");

        var exit = await command.ExecuteAsync(args, default);

        Assert.Equal(BootstrapAdminCommand.UsageError, exit);
        Assert.Empty(store.Created);
        Assert.Contains(terminal.Output, line => line.StartsWith("Usage:", StringComparison.Ordinal));
    }
}
