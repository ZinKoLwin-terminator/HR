using System.Linq.Expressions;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Organization.JobGrades;
using Apex.Hrms.Application.Organization.Positions;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.Application.Tests.Employees;

/// <summary>Create, update, delete, list and the code rules of employees, through the real handlers and validators.</summary>
public sealed class EmployeeHandlerTests : IDisposable
{
    private readonly List<IDisposable> hosts = [];

    public void Dispose() => hosts.ForEach(host => host.Dispose());

    private TestHost NewHost(Action<IServiceCollection>? configure = null)
    {
        var host = new TestHost(configure);
        hosts.Add(host);
        return host;
    }

    // ---- Create ----

    [Fact]
    public async Task Create_returns_the_employee_without_personal_data_and_records_the_joined_history()
    {
        var host = NewHost();
        var org = await host.SeedOrgAsync();

        var created = await host.CreateEmployeeAsync(EmployeeFixtures.NewEmployee(org, "emp100"));

        Assert.Equal("EMP100", created.EmployeeCode);
        Assert.Equal("Mia Hartley", created.FullName);
        Assert.Equal("Active", created.EmploymentStatus);
        Assert.Equal("Full-time", created.EmploymentType);
        Assert.Null(created.BankAccountMasked);
        var entry = Assert.Single(host.Repository<Employee>().Items.Single().History);
        Assert.Equal(HistoryEventType.Joined, entry.Type);
        Assert.Equal("Joined as Developer", entry.Title);
        Assert.Equal("Test User", entry.RecordedBy);
        Assert.Equal(new DateOnly(2026, 11, 9), entry.Date);
    }

    [Fact]
    public async Task Create_generates_codes_from_the_sequence_and_skips_codes_in_use()
    {
        var host = NewHost();
        var org = await host.SeedOrgAsync();
        await host.CreateEmployeeAsync(EmployeeFixtures.NewEmployee(org, "EMP001", "first@example.test"));
        await host.CreateEmployeeAsync(EmployeeFixtures.NewEmployee(org, "EMP002", "second@example.test"));

        var generated = await host.CreateEmployeeAsync(EmployeeFixtures.NewEmployee(org, code: null, "third@example.test"));
        var next = await host.CreateEmployeeAsync(EmployeeFixtures.NewEmployee(org, code: null, "fourth@example.test"));

        Assert.Equal("EMP003", generated.EmployeeCode);
        Assert.Equal("EMP004", next.EmployeeCode);
    }

    [Fact]
    public async Task Next_code_handler_suggests_a_free_code()
    {
        var host = NewHost();
        var org = await host.SeedOrgAsync();
        await host.CreateEmployeeAsync(EmployeeFixtures.NewEmployee(org, "EMP001"));

        var suggestion = ResultAssert.Succeeded(await host.Get<INextEmployeeCodeHandler>().HandleAsync(default));

        Assert.Equal("EMP002", suggestion.Code);
    }

    [Fact]
    public async Task Create_rejects_a_duplicate_code_and_a_duplicate_work_email_ignoring_case()
    {
        var host = NewHost();
        var org = await host.SeedOrgAsync();
        await host.CreateEmployeeAsync(EmployeeFixtures.NewEmployee(org, "EMP001", "mia@example.test"));

        var sameCode = await host.CreateEmployee(EmployeeFixtures.NewEmployee(org, "emp001", "other@example.test"));
        var sameEmail = await host.CreateEmployee(EmployeeFixtures.NewEmployee(org, "EMP002", "MIA@Example.test"));

        ResultAssert.FailedOnField(sameCode, "employeeCode");
        ResultAssert.FailedOnField(sameEmail, "workEmail");
    }

    [Theory]
    [InlineData("-EMP1")]
    [InlineData("EMP 1")]
    [InlineData("EMP_1")]
    [InlineData("EMP0000000001")]
    public async Task Create_rejects_a_malformed_or_too_long_code(string code)
    {
        var host = NewHost();
        var org = await host.SeedOrgAsync();

        var result = await host.CreateEmployee(EmployeeFixtures.NewEmployee(org, code));

        ResultAssert.FailedOnField(result, "employeeCode");
    }

    [Fact]
    public async Task Create_requires_age_18_in_the_business_calendar()
    {
        var host = NewHost();
        var org = await host.SeedOrgAsync();
        var today = host.Clock.Today;
        var request = EmployeeFixtures.NewEmployee(org);

        var exactly18 = await host.CreateEmployee(request with { DateOfBirth = today.AddYears(-18), EmployeeCode = "EMP018" });
        var oneDayShort = await host.CreateEmployee(request with { DateOfBirth = today.AddYears(-18).AddDays(1), WorkEmail = "young@example.test" });
        var future = await host.CreateEmployee(request with { DateOfBirth = today.AddDays(1), WorkEmail = "future@example.test" });

        Assert.True(exactly18.IsSuccess);
        ResultAssert.FailedOnField(oneDayShort, "dateOfBirth", "Employees must be at least 18 years old.");
        ResultAssert.FailedOnField(future, "dateOfBirth", "The date of birth cannot be in the future.");
    }

    [Fact]
    public async Task Create_checks_the_joining_probation_and_status_rules()
    {
        var host = NewHost();
        var org = await host.SeedOrgAsync();
        var request = EmployeeFixtures.NewEmployee(org);

        var probation = await host.CreateEmployee(request with { ProbationEndDate = new DateOnly(2024, 2, 28) });
        var exited = await host.CreateEmployee(request with { EmploymentStatus = "Resigned" });
        var unknown = await host.CreateEmployee(request with { EmploymentStatus = "Gone" });

        ResultAssert.FailedOnField(probation, "probationEndDate", "The probation cannot end before the joining date.");
        ResultAssert.FailedOnField(exited, "employmentStatus");
        ResultAssert.FailedOnField(unknown, "employmentStatus");
    }

    [Fact]
    public async Task Create_reports_every_missing_field_at_once_with_nested_names()
    {
        var host = NewHost();
        var org = await host.SeedOrgAsync();
        var request = EmployeeFixtures.NewEmployee(org) with
        {
            FirstName = " ",
            NationalId = "",
            Address = new AddressRequest("", null, "", null, null, "Myanmar"),
            EmergencyContact = new EmergencyContactRequest("Test Contact", "Neighbour", "+95 9 555 0102"),
        };

        var result = await host.CreateEmployee(request);

        var error = ResultAssert.Failed(result, ErrorType.Validation);
        Assert.Contains("firstName", error.FieldErrors!.Keys);
        Assert.Contains("nationalId", error.FieldErrors.Keys);
        Assert.Contains("address.line1", error.FieldErrors.Keys);
        Assert.Contains("address.city", error.FieldErrors.Keys);
        Assert.Contains("emergencyContact.relationship", error.FieldErrors.Keys);
    }

    [Fact]
    public async Task Create_rejects_a_missing_address_without_failing()
    {
        var host = NewHost();
        var org = await host.SeedOrgAsync();

        var result = await host.CreateEmployee(EmployeeFixtures.NewEmployee(org) with { Address = null, EmergencyContact = null });

        var error = ResultAssert.Failed(result, ErrorType.Validation);
        Assert.Contains("address", error.FieldErrors!.Keys);
        Assert.Contains("emergencyContact", error.FieldErrors.Keys);
    }

    // ---- Organization assignment ----

    [Fact]
    public async Task Create_requires_an_existing_and_consistent_organization_assignment()
    {
        var host = NewHost();
        var org = await host.SeedOrgAsync();
        var otherCompany = await host.CreateCompanyAsync("OTH", "Other Company");
        var otherBranch = await host.CreateBranchAsync(otherCompany.Id, "OTH-YGN");
        var otherDepartment = await host.CreateDepartmentAsync(otherCompany.Id, otherBranch.Id, "OTH-ENG");
        var request = EmployeeFixtures.NewEmployee(org);

        var missing = await host.CreateEmployee(request with { CompanyId = 999, BranchId = 998, DepartmentId = 997, PositionId = 996, JobGradeId = 995 });
        var wrongBranch = await host.CreateEmployee(request with { BranchId = otherBranch.Id });
        var wrongDepartment = await host.CreateEmployee(request with { DepartmentId = otherDepartment.Id });

        var missingError = ResultAssert.Failed(missing, ErrorType.Validation);
        Assert.All(["companyId", "branchId", "departmentId", "positionId", "jobGradeId"], field => Assert.Contains(field, missingError.FieldErrors!.Keys));
        Assert.Contains("This branch does not belong to the chosen company.", ResultAssert.Failed(wrongBranch, ErrorType.Validation).FieldErrors!["branchId"]);
        Assert.Contains("This department does not belong to the chosen branch.", ResultAssert.Failed(wrongDepartment, ErrorType.Validation).FieldErrors!["departmentId"]);
    }

    [Fact]
    public async Task Inactive_units_cannot_receive_an_employee_but_existing_assignments_may_stay()
    {
        var host = NewHost();
        var org = await host.SeedOrgAsync();
        var employee = await host.CreateEmployeeAsync(EmployeeFixtures.NewEmployee(org));
        await host.Get<ICommandHandler<DeactivateCommand, PositionDto>>()
            .HandleAsync(new DeactivateCommand(org.Position.Id, Requests.Version(org.Position.RowVersion)), default);

        var newcomer = await host.CreateEmployee(EmployeeFixtures.NewEmployee(org, "EMP200", "new@example.test"));
        var unchanged = await host.UpdateEmployee(employee.Id, EmployeeFixtures.UpdateOf(employee) with { FirstName = "Maya" });

        ResultAssert.FailedOnField(newcomer, "positionId", "This position is inactive.");
        Assert.True(unchanged.IsSuccess);
    }

    [Fact]
    public async Task A_section_must_belong_to_the_department()
    {
        var host = NewHost();
        var org = await host.SeedOrgAsync();
        var otherDepartment = await host.CreateDepartmentAsync(org.Company.Id, org.Branch.Id, "OTH-DEP");
        var foreignSection = await host.CreateSectionAsync(otherDepartment.Id);

        var result = await host.CreateEmployee(EmployeeFixtures.NewEmployee(org) with { SectionId = foreignSection.Id });

        ResultAssert.FailedOnField(result, "sectionId", "This section does not belong to the chosen department.");
    }

    // ---- Manager ----

    [Fact]
    public async Task A_manager_must_exist_and_must_not_have_left()
    {
        var host = NewHost();
        var org = await host.SeedOrgAsync();
        var manager = await host.CreateEmployeeAsync(EmployeeFixtures.NewEmployee(org, "EMP001", "manager@example.test"));
        var request = EmployeeFixtures.NewEmployee(org, "EMP002", "report@example.test");

        var missing = await host.CreateEmployee(request with { ManagerId = 999 });
        ResultAssert.Succeeded(await host.Command<ExitEmployeeCommand, EmployeeDto>(
            new ExitEmployeeCommand(manager.Id, Requests.Version(manager.RowVersion), new DateOnly(2026, 11, 1), "Resigned")));
        var exited = await host.CreateEmployee(request with { ManagerId = manager.Id });

        ResultAssert.FailedOnField(missing, "managerId", "The chosen manager does not exist.");
        ResultAssert.FailedOnField(exited, "managerId", "A manager cannot be an employee who has left.");
    }

    [Fact]
    public async Task An_employee_cannot_be_their_own_manager_or_create_a_reporting_loop()
    {
        var host = NewHost();
        var org = await host.SeedOrgAsync();
        var boss = await host.CreateEmployeeAsync(EmployeeFixtures.NewEmployee(org, "EMP001", "boss@example.test"));
        var report = await host.CreateEmployeeAsync(EmployeeFixtures.NewEmployee(org, "EMP002", "report@example.test") with { ManagerId = boss.Id });

        var self = await host.UpdateEmployee(boss.Id, EmployeeFixtures.UpdateOf(boss) with { ManagerId = boss.Id });
        var loop = await host.UpdateEmployee(boss.Id, EmployeeFixtures.UpdateOf(boss) with { ManagerId = report.Id });

        ResultAssert.FailedOnField(self, "managerId", "An employee cannot be their own manager.");
        ResultAssert.FailedOnField(loop, "managerId");
    }

    // ---- Update ----

    [Fact]
    public async Task Update_changes_the_employment_fields_and_records_history_but_never_the_status()
    {
        var host = NewHost();
        var org = await host.SeedOrgAsync();
        var seniorGrade = await host.CreateJobGradeAsync("G2");
        var seniorPosition = await host.CreatePositionAsync(org.Department.Id, seniorGrade.Id, "SEN");
        var created = await host.CreateEmployeeAsync(EmployeeFixtures.NewEmployee(org));

        var updated = ResultAssert.Succeeded(await host.UpdateEmployee(
            created.Id,
            EmployeeFixtures.UpdateOf(created) with { PositionId = seniorPosition.Id, JobGradeId = seniorGrade.Id, EmploymentType = "Contract" }));

        Assert.Equal("Contract", updated.EmploymentType);
        Assert.Equal("Active", updated.EmploymentStatus);
        Assert.False(updated.RowVersion.AsSpan().SequenceEqual(created.RowVersion));
        var history = host.Repository<Employee>().Items.Single().History.ToList();
        Assert.Equal(
            [HistoryEventType.Joined, HistoryEventType.Transfer, HistoryEventType.ContractChange],
            history.Select(entry => entry.Type));
        Assert.StartsWith("Moved: ", history[1].Title, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_higher_job_grade_level_with_a_new_position_is_a_promotion()
    {
        var host = NewHost();
        var org = await host.SeedOrgAsync();
        var created = await host.CreateEmployeeAsync(EmployeeFixtures.NewEmployee(org));
        var higher = ResultAssert.Succeeded(await host.Get<ICreateHandler<JobGradeCreateRequest, JobGradeDto>>()
            .HandleAsync(Requests.JobGrade("G9") with { Level = 9 }, default));
        var lead = await host.CreatePositionAsync(org.Department.Id, higher.Id, "LEAD");

        ResultAssert.Succeeded(await host.UpdateEmployee(created.Id, EmployeeFixtures.UpdateOf(created) with { PositionId = lead.Id, JobGradeId = higher.Id }));

        var entry = host.Repository<Employee>().Items.Single().History.Last();
        Assert.Equal(HistoryEventType.Promotion, entry.Type);
        Assert.StartsWith("Promoted: ", entry.Title, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Update_requires_a_current_row_version_and_keeps_code_and_email_unique()
    {
        var host = NewHost();
        var org = await host.SeedOrgAsync();
        var first = await host.CreateEmployeeAsync(EmployeeFixtures.NewEmployee(org, "EMP001", "first@example.test"));
        var second = await host.CreateEmployeeAsync(EmployeeFixtures.NewEmployee(org, "EMP002", "second@example.test"));

        var missingVersion = await host.UpdateEmployee(first.Id, EmployeeFixtures.UpdateOf(first) with { RowVersion = null });
        var stale = await host.UpdateEmployee(first.Id, EmployeeFixtures.UpdateOf(first) with { RowVersion = Requests.Version(second.RowVersion) });
        var codeTaken = await host.UpdateEmployee(first.Id, EmployeeFixtures.UpdateOf(first) with { EmployeeCode = "emp002" });
        var emailTaken = await host.UpdateEmployee(first.Id, EmployeeFixtures.UpdateOf(first) with { WorkEmail = "second@example.test" });
        var missing = await host.UpdateEmployee(999, EmployeeFixtures.UpdateOf(first));

        ResultAssert.FailedOnField(missingVersion, "rowVersion");
        ResultAssert.Failed(stale, ErrorType.Conflict);
        ResultAssert.FailedOnField(codeTaken, "employeeCode");
        ResultAssert.FailedOnField(emailTaken, "workEmail");
        ResultAssert.Failed(missing, ErrorType.NotFound);
    }

    [Fact]
    public async Task Update_cannot_move_the_joining_date_after_the_exit_date()
    {
        var host = NewHost();
        var org = await host.SeedOrgAsync();
        var created = await host.CreateEmployeeAsync(EmployeeFixtures.NewEmployee(org));
        var exited = ResultAssert.Succeeded(await host.Command<ExitEmployeeCommand, EmployeeDto>(
            new ExitEmployeeCommand(created.Id, Requests.Version(created.RowVersion), new DateOnly(2025, 1, 31), "Resigned")));

        var result = await host.UpdateEmployee(created.Id, EmployeeFixtures.UpdateOf(exited) with { JoiningDate = new DateOnly(2025, 2, 1) });

        ResultAssert.FailedOnField(result, "joiningDate", "The joining date cannot be after the exit date.");
    }

    // ---- Delete ----

    [Fact]
    public async Task Delete_is_blocked_while_the_employee_manages_others_and_needs_the_row_version()
    {
        var host = NewHost();
        var org = await host.SeedOrgAsync();
        var boss = await host.CreateEmployeeAsync(EmployeeFixtures.NewEmployee(org, "EMP001", "boss@example.test"));
        var report = await host.CreateEmployeeAsync(EmployeeFixtures.NewEmployee(org, "EMP002", "report@example.test") with { ManagerId = boss.Id });
        var delete = host.Get<IDeleteHandler<EmployeeDto>>();

        var blocked = await delete.HandleAsync(boss.Id, Requests.Version(boss.RowVersion), default);
        var noVersion = await delete.HandleAsync(report.Id, null, default);
        var removed = await delete.HandleAsync(report.Id, Requests.Version(report.RowVersion), default);
        var now = await host.GetEmployee(report.Id);
        var freshBoss = ResultAssert.Succeeded(await host.GetEmployee(boss.Id));
        var bossRemoved = await delete.HandleAsync(boss.Id, Requests.Version(freshBoss.RowVersion), default);

        ResultAssert.Failed(blocked, ErrorType.Conflict);
        Assert.Contains("1 direct report(s)", blocked.Error.Message, StringComparison.Ordinal);
        ResultAssert.FailedOnField(noVersion, "rowVersion");
        Assert.True(removed.IsSuccess);
        ResultAssert.Failed(now, ErrorType.NotFound);
        Assert.True(bossRemoved.IsSuccess);
    }

    // ---- Reading ----

    [Fact]
    public async Task List_pages_filters_and_searches_without_a_get_all()
    {
        var host = NewHost();
        var org = await host.SeedOrgAsync();
        await host.CreateEmployeeAsync(EmployeeFixtures.NewEmployee(org, "EMP001", "mia@example.test"));
        await host.CreateEmployeeAsync(EmployeeFixtures.NewEmployee(org, "EMP002", "ben@example.test") with { FirstName = "Ben", EmploymentType = "Intern" });
        var query = host.Get<IQueryHandler<EmployeeListItemDto, EmployeeFilter>>();

        var all = ResultAssert.Succeeded(await query.HandleAsync(new PageRequest<EmployeeFilter>(0, 1, new SortState("employeeCode", "desc"), null, new EmployeeFilter()), default));
        var search = ResultAssert.Succeeded(await query.HandleAsync(new PageRequest<EmployeeFilter>(0, 10, null, "ben", new EmployeeFilter()), default));
        var interns = ResultAssert.Succeeded(await query.HandleAsync(new PageRequest<EmployeeFilter>(0, 10, null, null, new EmployeeFilter(EmploymentType: "Intern")), default));
        var unknown = ResultAssert.Succeeded(await query.HandleAsync(new PageRequest<EmployeeFilter>(0, 10, null, null, new EmployeeFilter(EmploymentStatus: "Gone")), default));
        var unsortable = await query.HandleAsync(new PageRequest<EmployeeFilter>(0, 10, new SortState("nationalId", "asc"), null, new EmployeeFilter()), default);

        Assert.Equal(2, all.Total);
        Assert.Equal("EMP002", Assert.Single(all.Items).EmployeeCode);
        Assert.Equal("Ben Hartley", Assert.Single(search.Items).FullName);
        Assert.Equal("EMP002", Assert.Single(interns.Items).EmployeeCode);
        Assert.Empty(unknown.Items);
        ResultAssert.FailedOnField(unsortable, "sort");
        Assert.Null(host.Get<IServiceProvider>().GetService<IGetAllHandler<EmployeeDto>>());
        Assert.Null(host.Get<IServiceProvider>().GetService<IGetAllHandler<EmployeeListItemDto>>());
    }

    [Fact]
    public async Task Page_size_is_limited_to_200()
    {
        var host = NewHost();
        var query = host.Get<IQueryHandler<EmployeeListItemDto, EmployeeFilter>>();

        var page = ResultAssert.Succeeded(await query.HandleAsync(new PageRequest<EmployeeFilter>(0, 5000, null, null, new EmployeeFilter()), default));

        Assert.Equal(200, page.PageSize);
    }

    [Theory]
    [InlineData(typeof(EmployeeListItemDto))]
    [InlineData(typeof(EmployeeDto))]
    public void List_and_detail_dtos_hold_no_personal_data(Type dto)
    {
        string[] personal =
        [
            "NationalId", "DateOfBirth", "PersonalEmail", "Phone", "AlternatePhone", "Address", "EmergencyContact",
            "MaritalStatus", "Nationality", "AccountNumber", "BankName", "AccountName",
        ];

        var members = dto.GetProperties().Select(property => property.Name).ToList();

        Assert.All(personal, name => Assert.DoesNotContain(name, members));
    }

    [Fact]
    public void The_personal_dto_holds_exactly_the_personal_data_and_the_bank_dto_the_account()
    {
        var personal = typeof(EmployeePersonalDto).GetProperties().Select(property => property.Name).ToList();
        var bank = typeof(BankAccountDto).GetProperties().Select(property => property.Name).ToList();

        Assert.All(
            ["NationalId", "DateOfBirth", "PersonalEmail", "Phone", "AlternatePhone", "Address", "EmergencyContact"],
            name => Assert.Contains(name, personal));
        Assert.Contains("AccountNumber", bank);
        Assert.DoesNotContain("AccountNumber", personal);
    }

    [Fact]
    public async Task The_history_of_an_employee_lists_every_recorded_change()
    {
        var host = NewHost();
        var org = await host.SeedOrgAsync();
        var created = await host.CreateEmployeeAsync(EmployeeFixtures.NewEmployee(org));
        ResultAssert.Succeeded(await host.Command<ChangeEmployeeStatusCommand, EmployeeDto>(
            new ChangeEmployeeStatusCommand(created.Id, Requests.Version(created.RowVersion), "On Leave")));
        // The fake repository does not follow navigations, so the entries are stored (with the foreign key EF would
        // set) the way EF persists them.
        var employeeIdProperty = typeof(EmploymentHistoryEntry).GetProperty(nameof(EmploymentHistoryEntry.EmployeeId))!;
        foreach (var entry in host.Repository<Employee>().Items.Single().History)
        {
            employeeIdProperty.SetValue(entry, created.Id);
            host.Repository<EmploymentHistoryEntry>().Add(entry);
        }

        var history = ResultAssert.Succeeded(await host.Get<IEmployeeHistoryHandler>().HandleAsync(created.Id, default));
        var missing = await host.Get<IEmployeeHistoryHandler>().HandleAsync(999, default);

        Assert.Equal(["Joined", "Status Change"], history.Select(entry => entry.Type));
        ResultAssert.Failed(missing, ErrorType.NotFound);
    }

    // ---- Access scope ----

    private sealed class ToggleScope : IAccessScope<Employee>
    {
        public bool Hidden { get; set; }

        public Expression<Func<Employee, bool>> PredicateFor(ICurrentUser user) => _ => !Hidden;
    }

    [Fact]
    public async Task An_employee_outside_the_access_scope_behaves_like_a_missing_one_on_every_endpoint()
    {
        var scope = new ToggleScope();
        var host = NewHost(services => services.AddSingleton<IAccessScope<Employee>>(scope));
        var org = await host.SeedOrgAsync();
        var created = await host.CreateEmployeeAsync(EmployeeFixtures.NewEmployee(org));
        scope.Hidden = true;

        ResultAssert.Failed(await host.GetEmployee(created.Id), ErrorType.NotFound);
        ResultAssert.Failed(await host.Get<IGetByIdHandler<EmployeePersonalDto>>().HandleAsync(created.Id, default), ErrorType.NotFound);
        ResultAssert.Failed(await host.Get<IGetByIdHandler<BankAccountDto>>().HandleAsync(created.Id, default), ErrorType.NotFound);
        ResultAssert.Failed(await host.Get<IEmployeeHistoryHandler>().HandleAsync(created.Id, default), ErrorType.NotFound);
        ResultAssert.Failed(await host.UpdateEmployee(created.Id, EmployeeFixtures.UpdateOf(created)), ErrorType.NotFound);
        ResultAssert.Failed(
            await host.Get<IUpdateHandler<BankAccountUpdateRequest, BankAccountDto>>().HandleAsync(created.Id, EmployeeFixtures.Bank(), default),
            ErrorType.NotFound);
        ResultAssert.Failed(
            await host.Get<IDeleteHandler<EmployeeDto>>().HandleAsync(created.Id, Requests.Version(created.RowVersion), default),
            ErrorType.NotFound);
    }
}
