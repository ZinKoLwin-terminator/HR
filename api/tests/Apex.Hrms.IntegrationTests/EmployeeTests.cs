using System.Text.RegularExpressions;
using Apex.Hrms.Application;
using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Organization.Branches;
using Apex.Hrms.Application.Organization.Companies;
using Apex.Hrms.Application.Organization.Departments;
using Apex.Hrms.Application.Organization.JobGrades;
using Apex.Hrms.Application.Organization.Positions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.IntegrationTests;

/// <summary>
/// The employee module on the real stack: EF mapping of hr.Employee and its child tables, the sequence, constraint
/// translation, the placement view the organization checks use, and the personal-data separation. Every row has a
/// work email that starts with the tag of the test; the cleanup removes employees and their child rows first.
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
[Trait("Category", "Integration")]
public sealed partial class EmployeeTests : DatabaseTest
{
    private Task<Result<TDto>> Command<TCommand, TDto>(TCommand command)
        where TCommand : IEntityCommand =>
        InScopeAsync(sp => sp.GetRequiredService<ICommandHandler<TCommand, TDto>>().HandleAsync(command, default));

    private Task<Result<EmployeePersonalDto>> GetPersonal(int id) => Get<EmployeePersonalDto>(id);

    private Task<Result<BankAccountDto>> PutBank(int id, BankAccountUpdateRequest request) =>
        InScopeAsync(sp => sp.GetRequiredService<IUpdateHandler<BankAccountUpdateRequest, BankAccountDto>>().HandleAsync(id, request, default));

    private static BankAccountUpdateRequest Bank(string number = "1234567890", string? rowVersion = null) =>
        new("Sample Bank", "Main Branch", "Mia Testwell", number, rowVersion);

    private static Task<int> CountRowsAsync(string table, int employeeId) =>
        Sql.ScalarAsync<int>($"SELECT COUNT(*) FROM {table} WHERE EmployeeId = @id", ("@id", employeeId));

    [GeneratedRegex(@"^EMP\d{3,}$")]
    private static partial Regex GeneratedCode();

    // ---- Create: the code ----

    [IntegrationFact]
    public async Task Create_without_a_code_generates_one_from_the_sequence_and_stores_the_employee()
    {
        var org = await SeedOrganizationAsync();

        var created = await SeedEmployeeAsync(org, codeSuffix: null);

        Assert.Matches(GeneratedCode(), created.EmployeeCode);
        Assert.Equal(created.EmployeeCode, await Sql.ScalarAsync<string>("SELECT EmployeeCode FROM hr.Employee WHERE Id = @id", ("@id", created.Id)));
        Assert.Equal(8, created.RowVersion.Length);
        Assert.Equal("Active", created.EmploymentStatus);
    }

    [IntegrationFact]
    public async Task An_explicit_code_is_normalized_to_upper_case()
    {
        var org = await SeedOrganizationAsync();
        var request = Requests.Employee(org.Company.Id, org.Branch.Id, org.Department.Id, org.Position.Id, org.Grade.Id, "ab1") with
        {
            EmployeeCode = Requests.EmployeeCode("ab1").ToLowerInvariant(),
        };

        var created = Succeeded(await Create(request));

        Assert.Equal(Requests.EmployeeCode("AB1"), created.EmployeeCode);
        Assert.Equal(Requests.EmployeeCode("AB1"), await Sql.ScalarAsync<string>("SELECT EmployeeCode FROM hr.Employee WHERE Id = @id", ("@id", created.Id)));
    }

    [IntegrationFact]
    public async Task The_generated_code_skips_a_code_that_is_already_in_use()
    {
        var org = await SeedOrganizationAsync();
        var consumed = await Sql.ScalarAsync<int>("SELECT NEXT VALUE FOR hr.EmployeeCodeSeq");
        var taken = $"EMP{consumed + 1:D3}";
        await RawRows.EmployeeAsync(org.Company.Id, org.Branch.Id, org.Department.Id, org.Position.Id, org.Grade.Id, taken, Requests.WorkEmail("raw"));

        var created = await SeedEmployeeAsync(org, codeSuffix: null);

        Assert.Equal($"EMP{consumed + 2:D3}", created.EmployeeCode);
    }

    [IntegrationFact]
    public async Task The_next_code_handler_suggests_a_code_that_is_free()
    {
        var suggestion = Succeeded(await InScopeAsync(sp => sp.GetRequiredService<INextEmployeeCodeHandler>().HandleAsync(default)));

        Assert.Matches(GeneratedCode(), suggestion.Code);
        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM hr.Employee WHERE EmployeeCode = @code", ("@code", suggestion.Code)));
    }

    // ---- Create: uniqueness ----

    [IntegrationFact]
    public async Task A_duplicate_work_email_is_rejected_on_workEmail()
    {
        var org = await SeedOrganizationAsync();
        await SeedEmployeeAsync(org, "1", "same");

        var duplicate = await Create(Requests.Employee(org.Company.Id, org.Branch.Id, org.Department.Id, org.Position.Id, org.Grade.Id, "2", "same"));

        Assert.Equal("Work email must be unique.", FailedOnField(duplicate, "workEmail"));
    }

    [IntegrationFact]
    public async Task A_duplicate_code_is_rejected_on_employeeCode()
    {
        var org = await SeedOrganizationAsync();
        await SeedEmployeeAsync(org, "1", "first");

        var duplicate = await Create(Requests.Employee(org.Company.Id, org.Branch.Id, org.Department.Id, org.Position.Id, org.Grade.Id, "1", "second"));

        Assert.Equal("Employee code must be unique.", FailedOnField(duplicate, "employeeCode"));
    }

    [IntegrationFact]
    public async Task A_work_email_written_by_another_session_between_check_and_save_is_translated_by_constraint_name()
    {
        var org = await SeedOrganizationAsync();
        BeforeNextSave = async () => await RawRows.EmployeeAsync(
            org.Company.Id, org.Branch.Id, org.Department.Id, org.Position.Id, org.Grade.Id, Requests.EmployeeCode("RAW"), Requests.WorkEmail("race"));

        var result = await Create(Requests.Employee(org.Company.Id, org.Branch.Id, org.Department.Id, org.Position.Id, org.Grade.Id, "1", "race"));

        Assert.Equal("Work email must be unique.", FailedOnField(result, "workEmail"));
        Assert.DoesNotContain("@", result.Error.Message, StringComparison.Ordinal);
    }

    // ---- Create: stored shape ----

    [IntegrationFact]
    public async Task Create_stores_every_column_history_and_audit_and_no_bank_account()
    {
        var org = await SeedOrganizationAsync();

        var created = await SeedEmployeeAsync(org);

        var row = await Sql.ScalarAsync<string>(
            """
            SELECT CONCAT(Gender, '|', MaritalStatus, '|', EmploymentType, '|', EmploymentStatus, '|', AddressLine1, '|', AddressCity, '|',
                          EmergencyContactRelationship, '|', CONVERT(varchar(10), DateOfBirth, 23), '|', CONVERT(varchar(23), CreatedAtUtc, 121))
            FROM hr.Employee WHERE Id = @id
            """,
            ("@id", created.Id));
        Assert.Equal("Female|Single|Full-time|Active|12 Test Street|Yangon|Sibling|1994-05-17|2026-11-09 08:00:00.123", row);
        Assert.Equal(1, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM hr.EmploymentHistory WHERE EmployeeId = @id AND Type = 'Joined'", ("@id", created.Id)));
        Assert.Equal(0, await CountRowsAsync("hr.EmployeeBankAccount", created.Id));
        Assert.Null(created.BankAccountMasked);
    }

    [IntegrationFact]
    public async Task Create_rejects_a_company_branch_department_mismatch_before_the_database_does()
    {
        var org = await SeedOrganizationAsync();
        var other = await SeedCompanyAsync("B", "Test Company B");

        var result = await Create(Requests.Employee(other.Id, org.Branch.Id, org.Department.Id, org.Position.Id, org.Grade.Id, "1"));

        Assert.Equal("This branch does not belong to the chosen company.", FailedOnField(result, "branchId"));
    }

    [IntegrationFact]
    public async Task A_shift_that_does_not_exist_is_a_conflict_from_the_foreign_key()
    {
        var org = await SeedOrganizationAsync();

        var result = await Create(Requests.Employee(org.Company.Id, org.Branch.Id, org.Department.Id, org.Position.Id, org.Grade.Id, "1") with { ShiftId = 2_000_000 });

        Failed(result, ErrorType.Conflict);
    }

    // ---- Update, history, status ----

    [IntegrationFact]
    public async Task Update_persists_changes_and_history_and_never_the_status()
    {
        var org = await SeedOrganizationAsync();
        var senior = await SeedPositionAsync(org.Department.Id, org.Grade.Id, "A-SEN");
        var created = await SeedEmployeeAsync(org);

        var updated = Succeeded(await Update(created.Id, Requests.EmployeeUpdate(created, positionId: senior.Id)));

        Assert.Equal(senior.Id, updated.PositionId);
        Assert.Equal("Active", await Sql.ScalarAsync<string>("SELECT EmploymentStatus FROM hr.Employee WHERE Id = @id", ("@id", created.Id)));
        Assert.Equal(1, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM hr.EmploymentHistory WHERE EmployeeId = @id AND Type = 'Transfer'", ("@id", created.Id)));
        Assert.Equal(2, await CountRowsAsync("hr.EmploymentHistory", created.Id));
    }

    [IntegrationFact]
    public async Task A_stale_row_version_on_update_is_a_conflict_and_a_concurrent_write_is_caught_at_save()
    {
        var org = await SeedOrganizationAsync();
        var created = await SeedEmployeeAsync(org);
        BeforeNextSave = async () => await Sql.ExecuteAsync("UPDATE hr.Employee SET FirstName = N'Other' WHERE Id = @id", ("@id", created.Id));

        var raced = await Update(created.Id, Requests.EmployeeUpdate(created, workEmail: Requests.WorkEmail("renamed")));
        var stale = await Update(created.Id, Requests.EmployeeUpdate(created));

        Failed(raced, ErrorType.Conflict);
        Failed(stale, ErrorType.Conflict);
        Assert.Equal("Other", await Sql.ScalarAsync<string>("SELECT FirstName FROM hr.Employee WHERE Id = @id", ("@id", created.Id)));
    }

    [IntegrationFact]
    public async Task Exit_and_reinstate_persist_the_status_the_exit_date_and_history()
    {
        var org = await SeedOrganizationAsync();
        var created = await SeedEmployeeAsync(org);

        var exited = Succeeded(await Command<ExitEmployeeCommand, EmployeeDto>(
            new ExitEmployeeCommand(created.Id, Requests.Version(created.RowVersion), new DateOnly(2026, 10, 31), "Resigned")));

        Assert.Equal("Resigned|2026-10-31", await Sql.ScalarAsync<string>(
            "SELECT CONCAT(EmploymentStatus, '|', CONVERT(varchar(10), ExitDate, 23)) FROM hr.Employee WHERE Id = @id", ("@id", created.Id)));
        Assert.Equal(1, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM hr.EmploymentHistory WHERE EmployeeId = @id AND Type = 'Exit'", ("@id", created.Id)));

        var back = Succeeded(await Command<ReinstateEmployeeCommand, EmployeeDto>(
            new ReinstateEmployeeCommand(created.Id, Requests.Version(exited.RowVersion), "Probation")));

        Assert.Equal("Probation", back.EmploymentStatus);
        Assert.Null(back.ExitDate);
        Assert.Equal(DBNull.Value, await Sql.ScalarAsync<object>("SELECT ExitDate FROM hr.Employee WHERE Id = @id", ("@id", created.Id)));
    }

    [IntegrationFact]
    public async Task A_manager_who_has_left_cannot_be_chosen_and_a_manager_with_reports_cannot_be_deleted()
    {
        var org = await SeedOrganizationAsync();
        var boss = await SeedEmployeeAsync(org, "1", "boss");
        var request = Requests.Employee(org.Company.Id, org.Branch.Id, org.Department.Id, org.Position.Id, org.Grade.Id, "2", "report") with { ManagerId = boss.Id };
        var report = Succeeded(await Create(request));

        var blocked = await Delete<EmployeeDto>(boss.Id, Requests.Version(boss.RowVersion));
        var exited = Succeeded(await Command<ExitEmployeeCommand, EmployeeDto>(
            new ExitEmployeeCommand(boss.Id, Requests.Version(boss.RowVersion), new DateOnly(2026, 10, 31), "Terminated")));
        var next = await Create(request with { EmployeeCode = Requests.EmployeeCode("3"), WorkEmail = Requests.WorkEmail("other") });

        Assert.Contains("1 direct report(s)", Failed(blocked, ErrorType.Conflict).Message, StringComparison.Ordinal);
        Assert.Equal("A manager cannot be an employee who has left.", FailedOnField(next, "managerId"));
        Assert.Equal(boss.Id, report.ManagerId);
        Assert.Equal("Terminated", exited.EmploymentStatus);
    }

    // ---- Delete ----

    [IntegrationFact]
    public async Task Delete_removes_the_employee_with_its_bank_account_and_history()
    {
        var org = await SeedOrganizationAsync();
        var created = await SeedEmployeeAsync(org);
        Succeeded(await PutBank(created.Id, Bank()));
        var current = Succeeded(await Get<EmployeeDto>(created.Id));

        var deleted = await Delete<EmployeeDto>(created.Id, Requests.Version(current.RowVersion));

        Assert.True(deleted.IsSuccess, deleted.IsFailure ? deleted.Error.Message : null);
        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM hr.Employee WHERE Id = @id", ("@id", created.Id)));
        Assert.Equal(0, await CountRowsAsync("hr.EmployeeBankAccount", created.Id));
        Assert.Equal(0, await CountRowsAsync("hr.EmploymentHistory", created.Id));
        Failed(await Get<EmployeeDto>(created.Id), ErrorType.NotFound);
    }

    // ---- Personal data ----

    [IntegrationFact]
    public async Task Personal_data_round_trips_and_a_personal_update_replaces_the_address_and_contact()
    {
        var org = await SeedOrganizationAsync();
        var created = await SeedEmployeeAsync(org);

        var personal = Succeeded(await GetPersonal(created.Id));
        Assert.Equal("NRC-TEST-000001", personal.NationalId);
        Assert.Equal(new DateOnly(1994, 5, 17), personal.DateOfBirth);
        Assert.Equal("12 Test Street", personal.Address.Line1);

        var update = new EmployeePersonalUpdateRequest(
            personal.DateOfBirth,
            "Married",
            personal.Nationality,
            personal.NationalId,
            personal.PersonalEmail,
            personal.Phone,
            "+95 9 555 0150",
            new AddressRequest("99 New Street", "Flat 2", "Mandalay", "Mandalay Region", "05011", "Myanmar"),
            new EmergencyContactRequest("New Contact", "Spouse", "+95 9 555 0151"),
            Requests.Version(personal.RowVersion));
        var changed = Succeeded(await InScopeAsync(sp => sp.GetRequiredService<IUpdateHandler<EmployeePersonalUpdateRequest, EmployeePersonalDto>>()
            .HandleAsync(created.Id, update, default)));

        Assert.Equal("Married", changed.MaritalStatus);
        var reloaded = Succeeded(await GetPersonal(created.Id));
        Assert.Equal("99 New Street", reloaded.Address.Line1);
        Assert.Equal("Flat 2", reloaded.Address.Line2);
        Assert.Equal("Mandalay Region", reloaded.Address.State);
        Assert.Equal("Spouse", reloaded.EmergencyContact.Relationship);
        Assert.Equal("+95 9 555 0150", reloaded.AlternatePhone);
        Failed(await InScopeAsync(sp => sp.GetRequiredService<IUpdateHandler<EmployeePersonalUpdateRequest, EmployeePersonalDto>>()
            .HandleAsync(created.Id, update, default)), ErrorType.Conflict);
    }

    [Theory]
    [InlineData(typeof(EmployeeListItemDto))]
    [InlineData(typeof(EmployeeDto))]
    public void The_list_and_detail_dtos_have_no_personal_members(Type dto)
    {
        string[] personal =
        [
            "NationalId", "DateOfBirth", "PersonalEmail", "Phone", "AlternatePhone", "Address", "EmergencyContact",
            "MaritalStatus", "Nationality", "AccountNumber",
        ];

        var members = dto.GetProperties().Select(property => property.Name).ToList();

        Assert.All(personal, name => Assert.DoesNotContain(name, members));
    }

    [IntegrationFact]
    public async Task The_list_returns_only_list_rows_and_can_be_searched_by_the_tag()
    {
        var org = await SeedOrganizationAsync();
        await SeedEmployeeAsync(org, "1", "one");
        await SeedEmployeeAsync(org, "2", "two");

        var page = Succeeded(await Query<EmployeeListItemDto, EmployeeFilter>(
            new PageRequest<EmployeeFilter>(0, 10, new SortState("employeeCode", "asc"), Tag.ToLowerInvariant(), new EmployeeFilter())));

        Assert.Equal(2, page.Total);
        Assert.Equal([Requests.EmployeeCode("1"), Requests.EmployeeCode("2")], page.Items.Select(item => item.EmployeeCode));
        Assert.All(page.Items, item => Assert.Equal("Mia Testwell", item.FullName));
    }

    // ---- Bank account ----

    [IntegrationFact]
    public async Task The_bank_account_is_created_read_in_full_masked_elsewhere_and_changed_with_its_row_version()
    {
        var org = await SeedOrganizationAsync();
        var created = await SeedEmployeeAsync(org);
        Failed(await Get<BankAccountDto>(created.Id), ErrorType.NotFound);

        var first = Succeeded(await PutBank(created.Id, Bank("1234567890")));
        var read = Succeeded(await Get<BankAccountDto>(created.Id));
        var detail = Succeeded(await Get<EmployeeDto>(created.Id));

        Assert.Equal("1234567890", first.AccountNumber);
        Assert.Equal("1234567890", read.AccountNumber);
        Assert.Equal("******7890", detail.BankAccountMasked);
        Assert.Equal(1, await CountRowsAsync("hr.EmployeeBankAccount", created.Id));

        FailedOnField(await PutBank(created.Id, Bank("9876543210")), "rowVersion");
        var changed = Succeeded(await PutBank(created.Id, Bank("9876543210", Requests.Version(first.RowVersion))));
        Assert.Equal("9876543210", changed.AccountNumber);
        Failed(await PutBank(created.Id, Bank("1111222233", Requests.Version(first.RowVersion))), ErrorType.Conflict);
        Assert.Equal("9876543210", await Sql.ScalarAsync<string>("SELECT AccountNumber FROM hr.EmployeeBankAccount WHERE EmployeeId = @id", ("@id", created.Id)));
    }

    [IntegrationFact]
    public async Task A_bad_account_number_is_a_validation_error_and_nothing_is_stored()
    {
        var org = await SeedOrganizationAsync();
        var created = await SeedEmployeeAsync(org);

        FailedOnField(await PutBank(created.Id, Bank("12345")), "accountNumber");
        FailedOnField(await PutBank(created.Id, Bank("12345678AB")), "accountNumber");

        Assert.Equal(0, await CountRowsAsync("hr.EmployeeBankAccount", created.Id));
    }

    // ---- The organization checks count employees through the view ----

    [IntegrationFact]
    public async Task Employees_block_the_delete_of_their_organization_records_through_the_placement_view()
    {
        var org = await SeedOrganizationAsync();
        await SeedEmployeeAsync(org);

        var position = await Delete<PositionDto>(org.Position.Id, Requests.Version(org.Position.RowVersion));
        var department = await Delete<DepartmentDto>(org.Department.Id, Requests.Version(org.Department.RowVersion));
        var branch = await Delete<BranchDto>(org.Branch.Id, Requests.Version(org.Branch.RowVersion));
        var grade = await Delete<JobGradeDto>(org.Grade.Id, Requests.Version(org.Grade.RowVersion));
        var company = await Delete<CompanyDto>(org.Company.Id, Requests.Version(org.Company.RowVersion));

        foreach (var result in new[] { position, department, branch, grade, company })
        {
            Assert.Contains("1 employee(s)", Failed(result, ErrorType.Conflict).Message, StringComparison.Ordinal);
        }
    }

    [IntegrationFact]
    public async Task An_employee_blocks_moving_its_branch_to_another_company()
    {
        var org = await SeedOrganizationAsync();
        var other = await SeedCompanyAsync("B", "Test Company B");
        await SeedEmployeeAsync(org);

        var moved = await Update(org.Branch.Id, Requests.BranchUpdate(org.Branch, companyId: other.Id));

        Assert.Contains("employee(s)", FailedOnField(moved, "companyId"), StringComparison.Ordinal);
    }

    [IntegrationFact]
    public async Task After_the_employee_is_deleted_the_organization_records_can_be_deleted_again()
    {
        var org = await SeedOrganizationAsync();
        var created = await SeedEmployeeAsync(org);
        Assert.True((await Delete<EmployeeDto>(created.Id, Requests.Version(created.RowVersion))).IsSuccess);

        var position = await Delete<PositionDto>(org.Position.Id, Requests.Version(org.Position.RowVersion));

        Assert.True(position.IsSuccess, position.IsFailure ? position.Error.Message : null);
    }
}
