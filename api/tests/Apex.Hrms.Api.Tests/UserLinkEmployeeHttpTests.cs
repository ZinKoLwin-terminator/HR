using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Apex.Hrms.Application.Settings;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Security;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.Api.Tests;

/// <summary>
/// <c>POST /api/users/{id}/link-employee</c> through the real pipeline without a database: it needs <c>settings:Edit</c> and the
/// HR Administrator role, answers 200 with the user in the contract shape, passes the handler only the route id and the row
/// version (an <c>employeeId</c> in the body is ignored), and a refusal reaches the client as a 409 problem with the message as
/// the detail. The handler is a fake that records the commands it gets.
/// </summary>
public sealed class UserLinkEmployeeHttpTests(WebApplicationFactory<Program> fixture) : IClassFixture<WebApplicationFactory<Program>>
{
    private const string RowVersion = "AAAAAAAAB9E=";

    private static readonly FakeLinkHandler Handler = new();

    /// <summary>One host per role name; the grants of the role travel in a request header.</summary>
    private HttpClient ClientFor(string roleName, IEnumerable<string> grants)
    {
        var host = SignedInHost.Cached(
            fixture,
            $"link-employee|{roleName}",
            factory => SignedInHost.Customize(factory, AccessFakes.Account(roleName), grantsFromHeader: true)
                .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                    services.AddSingleton<ICommandHandler<LinkUserEmployeeCommand, UserDto>>(Handler))));
        return SignedInHost.CreateClientWithGrants(host, grants);
    }

    private static string[] PropertiesOf(JsonElement element) =>
        [.. element.EnumerateObject().Select(property => property.Name)];

    [Fact]
    public async Task An_administrator_without_settings_Edit_gets_403()
    {
        using var client = ClientFor("HR Administrator", AccessFakes.AllGrants.Where(grant => grant != "settings:Edit"));
        var before = Handler.Commands.Count;

        var response = await client.PostAsJsonAsync("/api/users/5/link-employee", new { rowVersion = RowVersion });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(before, Handler.Commands.Count);
    }

    [Fact]
    public async Task Settings_Edit_without_the_HR_Administrator_role_gets_403()
    {
        using var client = ClientFor("HR Officer", AccessFakes.AllGrants);
        var before = Handler.Commands.Count;

        var response = await client.PostAsJsonAsync("/api/users/5/link-employee", new { rowVersion = RowVersion });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(before, Handler.Commands.Count);
    }

    [Fact]
    public async Task An_administrator_gets_200_with_the_user_and_the_handler_gets_only_the_route_id_and_the_row_version()
    {
        using var client = ClientFor("HR Administrator", ["settings:View", "settings:Edit"]);

        // The caller cannot choose the employee: an employeeId in the body is not part of the command.
        var response = await client.PostAsJsonAsync("/api/users/5/link-employee", new { rowVersion = RowVersion, employeeId = 99 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(["id", "employeeId", "displayName", "email", "roleId", "status", "lastLoginOn", "rowVersion"], PropertiesOf(body));
        Assert.Equal(5, body.GetProperty("id").GetInt32());
        Assert.Equal(12, body.GetProperty("employeeId").GetInt32());
        Assert.Equal("TST- Linked User", body.GetProperty("displayName").GetString());
        Assert.Equal("Active", body.GetProperty("status").GetString());
        Assert.Contains(new LinkUserEmployeeCommand(5, RowVersion), Handler.Commands);
        Assert.Equal(["Id", "RowVersion"], typeof(LinkUserEmployeeCommand).GetProperties().Select(property => property.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_refusal_is_a_409_problem_whose_detail_is_the_message()
    {
        using var client = ClientFor("HR Administrator", ["settings:View", "settings:Edit"]);

        var response = await client.PostAsJsonAsync($"/api/users/{FakeLinkHandler.LinkedId}/link-employee", new { rowVersion = RowVersion });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Conflict", body.GetProperty("code").GetString());
        Assert.Equal("This account is already linked to an employee.", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task An_unknown_user_is_404()
    {
        using var client = ClientFor("HR Administrator", ["settings:View", "settings:Edit"]);

        var response = await client.PostAsJsonAsync($"/api/users/{FakeLinkHandler.MissingId}/link-employee", new { rowVersion = RowVersion });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Links every id except <see cref="LinkedId"/> (already linked) and <see cref="MissingId"/> (no such user); fictional data only.</summary>
    private sealed class FakeLinkHandler : ICommandHandler<LinkUserEmployeeCommand, UserDto>
    {
        public const int LinkedId = 409;
        public const int MissingId = 404;

        public ConcurrentBag<LinkUserEmployeeCommand> Commands { get; } = [];

        public Task<Result<UserDto>> HandleAsync(LinkUserEmployeeCommand command, CancellationToken cancellationToken)
        {
            Commands.Add(command);
            return Task.FromResult(command.Id switch
            {
                LinkedId => Result.Failure<UserDto>(AppError.Conflict(AppAccount.AlreadyLinkedMessage)),
                MissingId => Result.Failure<UserDto>(AppError.NotFound("User not found.")),
                _ => Result.Success(new UserDto(command.Id, 12, "TST- Linked User", "tst.linked@example.test", 1, "Active", null, [0, 0, 0, 0, 0, 0, 7, 209])),
            });
        }
    }
}
