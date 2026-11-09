using Apex.Hrms.Application.Security;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Tests.Security;

public sealed class AdministratorBootstrapTests
{
    private sealed class FakeStore(bool administratorExists) : IAdministratorStore
    {
        public int CreateCalls { get; private set; }

        public Task<bool> ActiveAdministratorExistsAsync(CancellationToken cancellationToken) => Task.FromResult(administratorExists);

        public Task<Result> CreateAdministratorAsync(string email, string displayName, string password, CancellationToken cancellationToken)
        {
            CreateCalls++;
            return Task.FromResult(Result.Success());
        }
    }

    private const string GoodPassword = "correct horse battery";

    [Fact]
    public async Task It_refuses_when_an_active_linked_administrator_exists_and_creates_nothing()
    {
        var store = new FakeStore(administratorExists: true);

        var result = await new AdministratorBootstrap(store).RunAsync("tst-admin@example.test", "Admin", GoodPassword, default);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal(AdministratorBootstrap.AlreadyExistsMessage, result.Error.Message);
        Assert.Equal(0, store.CreateCalls);
    }

    [Fact]
    public async Task It_creates_the_administrator_when_none_exists()
    {
        var store = new FakeStore(administratorExists: false);

        var result = await new AdministratorBootstrap(store).RunAsync("tst-admin@example.test", "Admin", GoodPassword, default);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, store.CreateCalls);
    }

    [Theory]
    [InlineData("", "Admin", GoodPassword, "email")]
    [InlineData("not-an-email", "Admin", GoodPassword, "email")]
    [InlineData("tst-admin@example.test", " ", GoodPassword, "displayName")]
    [InlineData("tst-admin@example.test", "Admin", "short", "password")]
    public async Task Invalid_details_are_a_validation_failure_and_create_nothing(string email, string name, string password, string field)
    {
        var store = new FakeStore(administratorExists: false);

        var result = await new AdministratorBootstrap(store).RunAsync(email, name, password, default);

        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.True(result.Error.FieldErrors!.ContainsKey(field));
        Assert.Equal(0, store.CreateCalls);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("12345678901", false)]
    [InlineData("123456789012", true)]
    [InlineData("aaaaaaaaaaaa", true)]
    public void The_only_password_rule_is_the_minimum_length_of_twelve(string? password, bool allowed) =>
        Assert.Equal(allowed, SecurityPolicy.PasswordViolation(password) is null);

    [Fact]
    public void The_reset_mail_holds_the_token_and_names_its_lifetime_and_never_the_password()
    {
        var withLink = PasswordResetEmail.Build("tst-user@example.test", "tok+/=en", "https://app.example.test/reset-password");
        var withoutLink = PasswordResetEmail.Build("tst-user@example.test", "tok+/=en", null);

        Assert.Equal("tst-user@example.test", withLink.To);
        Assert.Contains("https://app.example.test/reset-password?email=tst-user%40example.test&token=tok%2B%2F%3Den", withLink.Body, StringComparison.Ordinal);
        Assert.Contains("tok+/=en", withoutLink.Body, StringComparison.Ordinal);
        Assert.Contains("60 minutes", withLink.Body, StringComparison.Ordinal);
    }
}
