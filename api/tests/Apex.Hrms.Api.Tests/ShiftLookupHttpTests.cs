using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Apex.Hrms.Application.Attendance.Shifts;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.TestSupport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.Api.Tests;

/// <summary>
/// <c>GET /api/shifts/lookup</c> through the real pipeline without a database: anonymous callers get 401, any signed-in caller
/// gets the shifts (no module grant needed) in the contract's camelCase shape, and "lookup" is never taken for the id of
/// <c>GET /api/shifts/{id}</c>, which still needs attendance:View. The handlers are fakes with fictional rows. The live account
/// check of the signed-in policy (the session validator) is covered by <see cref="OrganizationLookupHttpTests"/>.
/// </summary>
public sealed class ShiftLookupHttpTests(WebApplicationFactory<Program> fixture) : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Route = "/api/shifts/lookup";

    /// <summary>One host for the class: the grants of the caller's role travel in a request header, and the shift handlers are fakes.</summary>
    private HttpClient ClientWith(params string[] grants)
    {
        var host = SignedInHost.Cached(
            fixture,
            "shift-lookup",
            factory => SignedInHost.Customize(factory, AccessFakes.Account("Payroll Officer"), grantsFromHeader: true)
                .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                {
                    services.AddScoped<IShiftLookupHandler, FakeLookup>();
                    services.AddScoped<IGetByIdHandler<ShiftDto>, FakeGetById>();
                })));
        return SignedInHost.CreateClientWithGrants(host, grants);
    }

    private static string[] PropertiesOf(JsonElement element) =>
        [.. element.EnumerateObject().Select(property => property.Name)];

    [Fact]
    public async Task An_anonymous_caller_gets_a_401_problem()
    {
        var host = SignedInHost.Cached(fixture, "development", factory => factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development")));
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task A_signed_in_caller_whose_role_holds_no_grant_gets_the_shifts_in_the_contract_shape()
    {
        using var client = ClientWith();

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
        Assert.Equal(2, body.GetArrayLength());
        var night = body[1];
        Assert.Equal(["id", "code", "name", "startTime", "endTime", "workDays", "isNightShift", "status"], PropertiesOf(night));
        Assert.Equal(2, night.GetProperty("id").GetInt32());
        Assert.Equal("TNS", night.GetProperty("code").GetString());
        Assert.Equal("TST- Night Shift", night.GetProperty("name").GetString());
        Assert.Equal("22:00", night.GetProperty("startTime").GetString());
        Assert.Equal("06:00", night.GetProperty("endTime").GetString());
        Assert.Equal([0, 3, 6], night.GetProperty("workDays").EnumerateArray().Select(day => day.GetInt32()));
        Assert.True(night.GetProperty("isNightShift").GetBoolean());
        Assert.Equal("Inactive", night.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Lookup_is_never_read_as_a_shift_id_and_one_shift_still_needs_attendance_view()
    {
        using var payroll = ClientWith("employees:View", "payroll:View");
        using var attendance = ClientWith("attendance:View");

        var lookup = await payroll.GetAsync(Route);
        var oneWithoutView = await payroll.GetAsync("/api/shifts/1");
        var oneWithView = await attendance.GetAsync("/api/shifts/1");

        Assert.Equal(HttpStatusCode.OK, lookup.StatusCode);
        Assert.Equal(JsonValueKind.Array, (await lookup.Content.ReadFromJsonAsync<JsonElement>()).ValueKind);
        Assert.Equal(HttpStatusCode.Forbidden, oneWithoutView.StatusCode);
        Assert.Equal(HttpStatusCode.OK, oneWithView.StatusCode);
        var shift = await oneWithView.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, shift.GetProperty("id").GetInt32());
        Assert.Equal(60, shift.GetProperty("breakMinutes").GetInt32());
    }

    /// <summary>A day shift and an inactive night shift (fictional).</summary>
    private sealed class FakeLookup : IShiftLookupHandler
    {
        public Task<Result<IReadOnlyList<ShiftLookupDto>>> HandleAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success<IReadOnlyList<ShiftLookupDto>>(
            [
                new ShiftLookupDto(1, "TDS", "TST- Day Shift", new TimeOnly(9, 0), new TimeOnly(18, 0), [1, 2, 3, 4, 5], false, "Active"),
                new ShiftLookupDto(2, "TNS", "TST- Night Shift", new TimeOnly(22, 0), new TimeOnly(6, 0), [0, 3, 6], true, "Inactive"),
            ]));
    }

    /// <summary>The full shift by id, so a request that reaches <c>GET /api/shifts/{id}</c> answers 200 with the full DTO.</summary>
    private sealed class FakeGetById : IGetByIdHandler<ShiftDto>
    {
        public Task<Result<ShiftDto>> HandleAsync(int id, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success(new ShiftDto(
                id, "TDS", "TST- Day Shift", new TimeOnly(9, 0), new TimeOnly(18, 0), 60, 15, [1, 2, 3, 4, 5], false, "#3f6fd8", "Active", [0, 0, 0, 0, 0, 0, 0, 1])));
    }
}
