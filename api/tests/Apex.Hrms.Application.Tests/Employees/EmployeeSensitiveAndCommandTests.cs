using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;

namespace Apex.Hrms.Application.Tests.Employees;

/// <summary>The personal data endpoint, the bank account sub-resource and the status commands.</summary>
public sealed class EmployeeSensitiveAndCommandTests : IDisposable
{
    private readonly List<IDisposable> hosts = [];

    public void Dispose() => hosts.ForEach(host => host.Dispose());

    private async Task<(TestHost Host, OrgSeed Org, EmployeeDto Employee)> NewEmployeeAsync()
    {
        var host = new TestHost();
        hosts.Add(host);
        var org = await host.SeedOrgAsync();
        return (host, org, await host.CreateEmployeeAsync(EmployeeFixtures.NewEmployee(org)));
    }

    private static Task<Result<EmployeePersonalDto>> GetPersonal(TestHost host, int id) =>
        host.Get<IGetByIdHandler<EmployeePersonalDto>>().HandleAsync(id, default);

    private static Task<Result<BankAccountDto>> GetBank(TestHost host, int id) =>
        host.Get<IGetByIdHandler<BankAccountDto>>().HandleAsync(id, default);

    private static Task<Result<BankAccountDto>> PutBank(TestHost host, int id, BankAccountUpdateRequest request) =>
        host.Get<IUpdateHandler<BankAccountUpdateRequest, BankAccountDto>>().HandleAsync(id, request, default);

    // ---- Personal data ----

    [Fact]
    public async Task Personal_data_is_returned_only_by_the_personal_dto()
    {
        var (host, _, employee) = await NewEmployeeAsync();

        var personal = ResultAssert.Succeeded(await GetPersonal(host, employee.Id));

        Assert.Equal(new DateOnly(1994, 5, 17), personal.DateOfBirth);
        Assert.Equal("NRC-TEST-000001", personal.NationalId);
        Assert.Equal("mia.personal@example.test", personal.PersonalEmail);
        Assert.Equal("12 Test Street", personal.Address.Line1);
        Assert.Equal(string.Empty, personal.Address.Line2);
        Assert.Equal("Sibling", personal.EmergencyContact.Relationship);
        Assert.Equal("Single", personal.MaritalStatus);
        ResultAssert.Failed(await GetPersonal(host, 999), ErrorType.NotFound);
    }

    [Fact]
    public async Task Personal_update_changes_only_personal_data_and_needs_the_row_version()
    {
        var (host, _, employee) = await NewEmployeeAsync();
        var current = ResultAssert.Succeeded(await GetPersonal(host, employee.Id));
        var update = host.Get<IUpdateHandler<EmployeePersonalUpdateRequest, EmployeePersonalDto>>();

        var changed = ResultAssert.Succeeded(await update.HandleAsync(
            employee.Id,
            EmployeeFixtures.PersonalUpdateOf(current) with { MaritalStatus = "Married", Phone = "+95 9 555 0199" },
            default));
        var withoutVersion = await update.HandleAsync(employee.Id, EmployeeFixtures.PersonalUpdateOf(current) with { RowVersion = null }, default);
        var stale = await update.HandleAsync(employee.Id, EmployeeFixtures.PersonalUpdateOf(current), default);

        Assert.Equal("Married", changed.MaritalStatus);
        Assert.Equal("+95 9 555 0199", changed.Phone);
        ResultAssert.FailedOnField(withoutVersion, "rowVersion");
        ResultAssert.Failed(stale, ErrorType.Conflict);
        Assert.Equal("Full-time", (await host.GetEmployee(employee.Id)).Value.EmploymentType);
    }

    [Fact]
    public async Task Personal_update_enforces_the_minimum_age_and_the_required_values()
    {
        var (host, _, employee) = await NewEmployeeAsync();
        var current = ResultAssert.Succeeded(await GetPersonal(host, employee.Id));
        var update = host.Get<IUpdateHandler<EmployeePersonalUpdateRequest, EmployeePersonalDto>>();

        var minor = await update.HandleAsync(employee.Id, EmployeeFixtures.PersonalUpdateOf(current) with { DateOfBirth = host.Clock.Today.AddYears(-17) }, default);
        var blank = await update.HandleAsync(employee.Id, EmployeeFixtures.PersonalUpdateOf(current) with { NationalId = "  " }, default);

        ResultAssert.FailedOnField(minor, "dateOfBirth", "Employees must be at least 18 years old.");
        ResultAssert.FailedOnField(blank, "nationalId");
    }

    // ---- Bank account ----

    [Fact]
    public async Task Bank_account_is_missing_until_it_is_set_and_then_shown_in_full_only_by_the_bank_endpoint()
    {
        var (host, _, employee) = await NewEmployeeAsync();
        ResultAssert.Failed(await GetBank(host, employee.Id), ErrorType.NotFound);

        var created = ResultAssert.Succeeded(await PutBank(host, employee.Id, EmployeeFixtures.Bank("1234567890")));
        var read = ResultAssert.Succeeded(await GetBank(host, employee.Id));
        var detail = ResultAssert.Succeeded(await host.GetEmployee(employee.Id));

        Assert.Equal("1234567890", created.AccountNumber);
        Assert.Equal("1234567890", read.AccountNumber);
        Assert.Equal("******7890", detail.BankAccountMasked);
        Assert.DoesNotContain("1234567890", System.Text.Json.JsonSerializer.Serialize(detail), StringComparison.Ordinal);
        Assert.Equal(string.Empty, read.BranchName);
    }

    [Fact]
    public async Task Changing_an_existing_bank_account_needs_its_current_row_version()
    {
        var (host, _, employee) = await NewEmployeeAsync();
        var first = ResultAssert.Succeeded(await PutBank(host, employee.Id, EmployeeFixtures.Bank("1234567890")));

        var withoutVersion = await PutBank(host, employee.Id, EmployeeFixtures.Bank("9876543210"));
        var stale = await PutBank(host, employee.Id, EmployeeFixtures.Bank("9876543210", Requests.Version([9, 9, 9, 9, 9, 9, 9, 9])));
        var changed = ResultAssert.Succeeded(await PutBank(host, employee.Id, EmployeeFixtures.Bank("9876543210", Requests.Version(first.RowVersion))));

        ResultAssert.FailedOnField(withoutVersion, "rowVersion");
        ResultAssert.Failed(stale, ErrorType.Conflict);
        Assert.Equal("9876543210", changed.AccountNumber);
        Assert.False(changed.RowVersion.AsSpan().SequenceEqual(first.RowVersion));
    }

    [Theory]
    [InlineData("1234567")]
    [InlineData("123456789012345678901")]
    [InlineData("12345678AB")]
    [InlineData("1234 5678")]
    [InlineData("")]
    public async Task Bank_account_numbers_must_be_8_to_20_digits(string number)
    {
        var (host, _, employee) = await NewEmployeeAsync();

        var result = await PutBank(host, employee.Id, EmployeeFixtures.Bank(number));

        ResultAssert.FailedOnField(result, "accountNumber");
    }

    [Fact]
    public async Task Bank_account_requires_the_bank_and_holder_names_and_an_existing_employee()
    {
        var (host, _, employee) = await NewEmployeeAsync();

        var result = await PutBank(host, employee.Id, new BankAccountUpdateRequest("", null, " ", "1234567890", null));
        var missing = await PutBank(host, 999, EmployeeFixtures.Bank());

        var error = ResultAssert.Failed(result, ErrorType.Validation);
        Assert.Contains("bankName", error.FieldErrors!.Keys);
        Assert.Contains("accountName", error.FieldErrors.Keys);
        ResultAssert.Failed(missing, ErrorType.NotFound);
    }

    // ---- Commands ----

    [Fact]
    public async Task Exit_records_the_status_the_exit_date_and_a_history_entry()
    {
        var (host, _, employee) = await NewEmployeeAsync();

        var exited = ResultAssert.Succeeded(await host.Command<ExitEmployeeCommand, EmployeeDto>(
            new ExitEmployeeCommand(employee.Id, Requests.Version(employee.RowVersion), new DateOnly(2026, 10, 31), "Terminated")));

        Assert.Equal("Terminated", exited.EmploymentStatus);
        Assert.Equal(new DateOnly(2026, 10, 31), exited.ExitDate);
        var last = host.Repository<Employee>().Items.Single().History.Last();
        Assert.Equal(HistoryEventType.Exit, last.Type);
        Assert.Equal("Status changed to Terminated", last.Title);
        Assert.Equal("Previously Active.", last.Details);
    }

    [Fact]
    public async Task Exit_validates_its_input_and_the_state()
    {
        var (host, _, employee) = await NewEmployeeAsync();
        var version = Requests.Version(employee.RowVersion);

        var noDate = await host.Command<ExitEmployeeCommand, EmployeeDto>(new ExitEmployeeCommand(employee.Id, version, null, "Resigned"));
        var beforeJoining = await host.Command<ExitEmployeeCommand, EmployeeDto>(new ExitEmployeeCommand(employee.Id, version, new DateOnly(2024, 2, 29), "Resigned"));
        var notAnExit = await host.Command<ExitEmployeeCommand, EmployeeDto>(new ExitEmployeeCommand(employee.Id, version, new DateOnly(2026, 1, 1), "Active"));
        var noStatus = await host.Command<ExitEmployeeCommand, EmployeeDto>(new ExitEmployeeCommand(employee.Id, version, new DateOnly(2026, 1, 1), null));
        var noVersion = await host.Command<ExitEmployeeCommand, EmployeeDto>(new ExitEmployeeCommand(employee.Id, null, new DateOnly(2026, 1, 1), "Resigned"));
        var missing = await host.Command<ExitEmployeeCommand, EmployeeDto>(new ExitEmployeeCommand(999, version, new DateOnly(2026, 1, 1), "Resigned"));

        ResultAssert.FailedOnField(noDate, "exitDate");
        ResultAssert.FailedOnField(beforeJoining, "exitDate", "The exit date cannot be before the joining date.");
        ResultAssert.FailedOnField(notAnExit, "status");
        ResultAssert.FailedOnField(noStatus, "status");
        ResultAssert.FailedOnField(noVersion, "rowVersion");
        ResultAssert.Failed(missing, ErrorType.NotFound);
    }

    [Fact]
    public async Task A_former_employee_cannot_exit_again_or_change_status_until_reinstated()
    {
        var (host, _, employee) = await NewEmployeeAsync();
        var exited = ResultAssert.Succeeded(await host.Command<ExitEmployeeCommand, EmployeeDto>(
            new ExitEmployeeCommand(employee.Id, Requests.Version(employee.RowVersion), new DateOnly(2026, 10, 31), "Resigned")));
        var version = Requests.Version(exited.RowVersion);

        var again = await host.Command<ExitEmployeeCommand, EmployeeDto>(new ExitEmployeeCommand(employee.Id, version, new DateOnly(2026, 11, 1), "Resigned"));
        var move = await host.Command<ChangeEmployeeStatusCommand, EmployeeDto>(new ChangeEmployeeStatusCommand(employee.Id, version, "On Leave"));
        var back = ResultAssert.Succeeded(await host.Command<ReinstateEmployeeCommand, EmployeeDto>(new ReinstateEmployeeCommand(employee.Id, version, null)));

        ResultAssert.Failed(again, ErrorType.Conflict);
        ResultAssert.Failed(move, ErrorType.Conflict);
        Assert.Equal("Active", back.EmploymentStatus);
        Assert.Null(back.ExitDate);
    }

    [Fact]
    public async Task Reinstate_needs_a_former_employee_and_a_current_status()
    {
        var (host, _, employee) = await NewEmployeeAsync();
        var version = Requests.Version(employee.RowVersion);

        var notLeft = await host.Command<ReinstateEmployeeCommand, EmployeeDto>(new ReinstateEmployeeCommand(employee.Id, version, null));
        var exited = ResultAssert.Succeeded(await host.Command<ExitEmployeeCommand, EmployeeDto>(
            new ExitEmployeeCommand(employee.Id, version, new DateOnly(2026, 10, 31), "Resigned")));
        var toExit = await host.Command<ReinstateEmployeeCommand, EmployeeDto>(new ReinstateEmployeeCommand(employee.Id, Requests.Version(exited.RowVersion), "Terminated"));
        var onProbation = ResultAssert.Succeeded(await host.Command<ReinstateEmployeeCommand, EmployeeDto>(
            new ReinstateEmployeeCommand(employee.Id, Requests.Version(exited.RowVersion), "Probation")));

        ResultAssert.Failed(notLeft, ErrorType.Conflict);
        ResultAssert.FailedOnField(toExit, "status");
        Assert.Equal("Probation", onProbation.EmploymentStatus);
    }

    [Fact]
    public async Task Status_command_moves_between_current_statuses_and_leaves_history()
    {
        var (host, _, employee) = await NewEmployeeAsync();

        var onLeave = ResultAssert.Succeeded(await host.Command<ChangeEmployeeStatusCommand, EmployeeDto>(
            new ChangeEmployeeStatusCommand(employee.Id, Requests.Version(employee.RowVersion), "On Leave")));
        var same = ResultAssert.Succeeded(await host.Command<ChangeEmployeeStatusCommand, EmployeeDto>(
            new ChangeEmployeeStatusCommand(employee.Id, Requests.Version(onLeave.RowVersion), "On Leave")));
        var toExit = await host.Command<ChangeEmployeeStatusCommand, EmployeeDto>(
            new ChangeEmployeeStatusCommand(employee.Id, Requests.Version(same.RowVersion), "Resigned"));
        var unknown = await host.Command<ChangeEmployeeStatusCommand, EmployeeDto>(
            new ChangeEmployeeStatusCommand(employee.Id, Requests.Version(same.RowVersion), "Napping"));

        Assert.Equal("On Leave", same.EmploymentStatus);
        ResultAssert.FailedOnField(toExit, "status");
        ResultAssert.FailedOnField(unknown, "status");
        var types = host.Repository<Employee>().Items.Single().History.Select(entry => entry.Type);
        Assert.Equal([HistoryEventType.Joined, HistoryEventType.StatusChange], types);
    }

    [Fact]
    public async Task Stale_command_row_versions_are_conflicts()
    {
        var (host, _, employee) = await NewEmployeeAsync();
        ResultAssert.Succeeded(await host.Command<ChangeEmployeeStatusCommand, EmployeeDto>(
            new ChangeEmployeeStatusCommand(employee.Id, Requests.Version(employee.RowVersion), "Probation")));

        var stale = await host.Command<ChangeEmployeeStatusCommand, EmployeeDto>(
            new ChangeEmployeeStatusCommand(employee.Id, Requests.Version(employee.RowVersion), "Suspended"));

        ResultAssert.Failed(stale, ErrorType.Conflict);
    }
}
