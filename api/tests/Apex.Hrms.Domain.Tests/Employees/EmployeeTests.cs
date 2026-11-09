using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;

namespace Apex.Hrms.Domain.Tests.Employees;

public sealed class EmployeeTests
{
    private static readonly DateOnly Today = new(2026, 11, 9);
    private static readonly ChangeContext Context = new(Today, "Test User");

    private static EmployeeDetails Details(string? code = "emp001") => new(
        code,
        " Mia ",
        "Hartley",
        Gender.Female,
        " Mia.Hartley@Example.Test ",
        PhotoUrl: null,
        CompanyId: 1,
        BranchId: 1,
        DepartmentId: 1,
        SectionId: null,
        PositionId: 1,
        JobGradeId: 1,
        ManagerId: null,
        ShiftId: null,
        EmploymentType.FullTime,
        new DateOnly(2024, 3, 1),
        ProbationEndDate: null);

    private static PersonalDetails Personal(DateOnly? born = null) => new(
        born ?? new DateOnly(1994, 5, 17),
        MaritalStatus.Unmarried,
        "Myanmar",
        "NRC-TEST-000001",
        PersonalEmail: null,
        "+95 9 555 0101",
        AlternatePhone: null,
        new PostalAddress("12 Test Street", " ", "Yangon", null, null, "Myanmar"),
        new EmergencyContact("Test Contact", EmergencyRelationship.Sibling, "+95 9 555 0102"));

    private static Employee NewEmployee(EmploymentStatus status = EmploymentStatus.Active) =>
        Employee.Create("EMP001", Details(), Personal(), status, Context, "Developer").Value;

    // ---- Create ----

    [Fact]
    public void Create_normalizes_the_values_and_records_the_joined_entry()
    {
        var employee = NewEmployee();

        Assert.Equal("EMP001", employee.EmployeeCode);
        Assert.Equal("Mia", employee.FirstName);
        Assert.Equal("mia.hartley@example.test", employee.WorkEmail);
        Assert.Null(employee.Address.Line2);
        Assert.Equal(EmploymentStatus.Active, employee.Status);
        var entry = Assert.Single(employee.History);
        Assert.Equal(HistoryEventType.Joined, entry.Type);
        Assert.Equal("Joined as Developer", entry.Title);
        Assert.Equal("Full-time employee. Record created.", entry.Details);
        Assert.Equal(Today, entry.Date);
        Assert.Equal("Test User", entry.RecordedBy);
    }

    [Fact]
    public void An_explicit_code_is_stored_upper_case()
    {
        var normalized = Details("emp-7a").Normalized();

        Assert.Equal("EMP-7A", normalized.EmployeeCode);
        Assert.Equal(normalized, normalized.Normalized());
    }

    [Theory]
    [InlineData("-EMP")]
    [InlineData("EMP 1")]
    [InlineData("EMP_1")]
    public void Create_rejects_a_malformed_code(string code) =>
        FailedOn(Employee.Create(code, Details(), Personal(), EmploymentStatus.Active, Context, null), "employeeCode");

    [Fact]
    public void Create_accepts_exactly_18_and_rejects_younger_and_future_birth_dates()
    {
        Assert.True(Employee.Create("E1", Details(), Personal(Today.AddYears(-18)), EmploymentStatus.Active, Context, null).IsSuccess);

        FailedOn(Employee.Create("E2", Details(), Personal(Today.AddYears(-18).AddDays(1)), EmploymentStatus.Active, Context, null), "dateOfBirth");
        FailedOn(Employee.Create("E3", Details(), Personal(Today.AddDays(1)), EmploymentStatus.Active, Context, null), "dateOfBirth");
    }

    [Theory]
    [InlineData(1994, 5, 17, 2026, 5, 16, 31)]
    [InlineData(1994, 5, 17, 2026, 5, 17, 32)]
    [InlineData(2008, 2, 29, 2026, 2, 28, 17)]
    [InlineData(2008, 2, 29, 2026, 3, 1, 18)]
    public void AgeOn_counts_whole_years(int by, int bm, int bd, int ty, int tm, int td, int expected) =>
        Assert.Equal(expected, Employee.AgeOn(new DateOnly(by, bm, bd), new DateOnly(ty, tm, td)));

    [Fact]
    public void Create_requires_names_email_and_probation_not_before_joining()
    {
        var details = Details() with { FirstName = " ", LastName = "", WorkEmail = " ", ProbationEndDate = new DateOnly(2024, 2, 1) };

        var error = Employee.Create("EMP001", details, Personal(), EmploymentStatus.Active, Context, null).Error;

        Assert.Equal(["firstName", "lastName", "probationEndDate", "workEmail"], error.FieldErrors!.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Create_reports_missing_personal_values_with_nested_field_names()
    {
        var personal = Personal() with
        {
            Nationality = "",
            NationalId = " ",
            Phone = "",
            Address = new PostalAddress("", null, "", null, null, ""),
            EmergencyContact = new EmergencyContact("", EmergencyRelationship.Other, ""),
        };

        var error = Employee.Create("EMP001", Details(), personal, EmploymentStatus.Active, Context, null).Error;

        string[] expected =
        [
            "nationality", "nationalId", "phone", "address.line1", "address.city", "address.country",
            "emergencyContact.name", "emergencyContact.phone",
        ];
        Assert.All(expected, field => Assert.Contains(field, error.FieldErrors!.Keys));
    }

    [Theory]
    [InlineData(EmploymentStatus.Resigned)]
    [InlineData(EmploymentStatus.Terminated)]
    public void A_new_employee_cannot_start_as_a_leaver(EmploymentStatus status) =>
        FailedOn(Employee.Create("EMP001", Details(), Personal(), status, Context, null), "employmentStatus");

    // ---- Update ----

    [Fact]
    public void Update_records_a_transfer_a_contract_change_and_keeps_status_and_exit_date()
    {
        var employee = NewEmployee();

        var result = employee.Update(
            Details() with { PositionId = 2, EmploymentType = EmploymentType.Contract },
            Context,
            new PlacementNotes("Developer", "Tester", IsPromotion: false));

        Assert.True(result.IsSuccess);
        Assert.Equal(EmploymentStatus.Active, employee.Status);
        Assert.Null(employee.ExitDate);
        Assert.Equal(
            [HistoryEventType.Joined, HistoryEventType.Transfer, HistoryEventType.ContractChange],
            employee.History.Select(entry => entry.Type));
        Assert.Equal("Moved: Developer → Tester", employee.History.ElementAt(1).Title);
        Assert.Equal("Employment type changed to Contract", employee.History.ElementAt(2).Title);
        Assert.Equal("Previously Full-time.", employee.History.ElementAt(2).Details);
    }

    [Fact]
    public void Update_records_a_promotion_and_an_organization_change_without_a_position_change()
    {
        var promoted = NewEmployee();
        promoted.Update(Details() with { PositionId = 2 }, Context, new PlacementNotes("Developer", "Lead", IsPromotion: true));
        var moved = NewEmployee();
        moved.Update(Details() with { DepartmentId = 2 }, Context, PlacementNotes.None);

        Assert.Equal(HistoryEventType.Promotion, promoted.History.Last().Type);
        Assert.Equal("Promoted: Developer → Lead", promoted.History.Last().Title);
        Assert.Equal(HistoryEventType.Transfer, moved.History.Last().Type);
        Assert.Equal("Organization assignment changed", moved.History.Last().Title);
    }

    [Fact]
    public void Update_without_changes_records_nothing()
    {
        var employee = NewEmployee();

        employee.Update(Details(), Context, PlacementNotes.None);

        Assert.Single(employee.History);
    }

    [Fact]
    public void A_failed_update_changes_nothing()
    {
        var employee = NewEmployee();

        var result = employee.Update(Details() with { FirstName = "Changed", ProbationEndDate = new DateOnly(2020, 1, 1) }, Context, PlacementNotes.None);

        FailedOn(result, "probationEndDate");
        Assert.Equal("Mia", employee.FirstName);
    }

    [Fact]
    public void Update_keeps_the_joining_date_before_the_exit_date()
    {
        var employee = NewEmployee();
        employee.Exit(new DateOnly(2025, 1, 31), EmploymentStatus.Resigned, Context);

        FailedOn(employee.Update(Details() with { JoiningDate = new DateOnly(2025, 2, 1) }, Context, PlacementNotes.None), "joiningDate");
    }

    [Fact]
    public void Personal_update_applies_or_rejects_as_a_whole()
    {
        var employee = NewEmployee();

        Assert.True(employee.UpdatePersonal(Personal() with { MaritalStatus = MaritalStatus.Married, Nationality = " Thai " }, Today).IsSuccess);
        FailedOn(employee.UpdatePersonal(Personal(Today.AddYears(-17)) with { Nationality = "Other" }, Today), "dateOfBirth");

        Assert.Equal(MaritalStatus.Married, employee.MaritalStatus);
        Assert.Equal("Thai", employee.Nationality);
    }

    // ---- Status ----

    [Fact]
    public void Exit_sets_status_and_date_and_records_the_exit_entry()
    {
        var employee = NewEmployee();

        var result = employee.Exit(new DateOnly(2026, 10, 31), EmploymentStatus.Terminated, Context);

        Assert.True(result.IsSuccess);
        Assert.True(employee.HasLeft);
        Assert.Equal(new DateOnly(2026, 10, 31), employee.ExitDate);
        Assert.Equal(HistoryEventType.Exit, employee.History.Last().Type);
        Assert.Equal("Status changed to Terminated", employee.History.Last().Title);
    }

    [Fact]
    public void Exit_rejects_a_current_status_a_date_before_joining_and_a_second_exit()
    {
        var employee = NewEmployee();

        FailedOn(employee.Exit(new DateOnly(2026, 1, 1), EmploymentStatus.Active, Context), "status");
        FailedOn(employee.Exit(new DateOnly(2024, 2, 29), EmploymentStatus.Resigned, Context), "exitDate");
        Assert.True(employee.Exit(new DateOnly(2024, 3, 1), EmploymentStatus.Resigned, Context).IsSuccess);
        Assert.Equal(ErrorType.Conflict, employee.Exit(new DateOnly(2025, 1, 1), EmploymentStatus.Resigned, Context).Error.Type);
    }

    [Fact]
    public void ChangeStatus_moves_between_current_statuses_and_is_idempotent()
    {
        var employee = NewEmployee();

        Assert.True(employee.ChangeStatus(EmploymentStatus.Suspended, Context).IsSuccess);
        Assert.True(employee.ChangeStatus(EmploymentStatus.Suspended, Context).IsSuccess);

        Assert.Equal(EmploymentStatus.Suspended, employee.Status);
        Assert.Equal(2, employee.History.Count);
        Assert.Equal("Status changed to Suspended", employee.History.Last().Title);
        Assert.Equal("Previously Active.", employee.History.Last().Details);
    }

    [Fact]
    public void ChangeStatus_cannot_exit_and_cannot_touch_a_leaver()
    {
        var employee = NewEmployee();

        FailedOn(employee.ChangeStatus(EmploymentStatus.Resigned, Context), "status");
        employee.Exit(new DateOnly(2026, 1, 1), EmploymentStatus.Resigned, Context);
        Assert.Equal(ErrorType.Conflict, employee.ChangeStatus(EmploymentStatus.Active, Context).Error.Type);
    }

    [Fact]
    public void Reinstate_clears_the_exit_date_and_needs_a_leaver_and_a_current_status()
    {
        var employee = NewEmployee();
        Assert.Equal(ErrorType.Conflict, employee.Reinstate(EmploymentStatus.Active, Context).Error.Type);
        employee.Exit(new DateOnly(2026, 1, 1), EmploymentStatus.Resigned, Context);

        FailedOn(employee.Reinstate(EmploymentStatus.Terminated, Context), "status");
        Assert.True(employee.Reinstate(EmploymentStatus.Probation, Context).IsSuccess);

        Assert.Equal(EmploymentStatus.Probation, employee.Status);
        Assert.Null(employee.ExitDate);
        Assert.Equal("Reinstated as Probation", employee.History.Last().Title);
    }

    // ---- Bank account ----

    [Fact]
    public void Bank_account_is_created_then_replaced_and_masks_all_but_four_digits()
    {
        var employee = NewEmployee();

        var created = employee.SetBankAccount(new BankDetails(" Sample Bank ", " ", "Mia Hartley", "1234567890"));
        var replaced = employee.SetBankAccount(new BankDetails("Other Bank", "Main", "Mia Hartley", "99887766"));

        Assert.True(created.IsSuccess);
        Assert.Same(created.Value, replaced.Value);
        Assert.Equal("Other Bank", employee.BankAccount!.BankName);
        Assert.Equal("****7766", employee.BankAccount.MaskedAccountNumber);
        Assert.Equal("99887766", employee.BankAccount.AccountNumber);
    }

    [Theory]
    [InlineData("1234567")]
    [InlineData("123456789012345678901")]
    [InlineData("1234567a")]
    [InlineData("1234-5678")]
    [InlineData("")]
    public void Bank_account_numbers_must_be_8_to_20_digits(string number)
    {
        var employee = NewEmployee();

        FailedOn(employee.SetBankAccount(new BankDetails("Bank", null, "Holder", number)), "accountNumber");
        Assert.Null(employee.BankAccount);
    }

    [Theory]
    [InlineData("12", "**")]
    [InlineData("1234", "****")]
    [InlineData("12345", "*2345")]
    public void Mask_hides_everything_but_the_last_four_digits(string number, string expected) =>
        Assert.Equal(expected, EmployeeBankAccount.Mask(number));

    // ---- Documents and texts ----

    [Fact]
    public void Document_defaults_the_upload_date_and_requires_title_and_file_name()
    {
        var created = EmployeeDocument.Create(5, new DocumentDetails(DocumentType.WorkPermit, " Permit ", "permit.pdf", null, new DateOnly(2027, 1, 1)), Today);

        Assert.Equal(Today, created.Value.UploadedOn);
        Assert.Equal("Permit", created.Value.Title);
        Assert.Equal(5, created.Value.EmployeeId);
        FailedOn(EmployeeDocument.Create(5, new DocumentDetails(DocumentType.Other, " ", "", null, null), Today), "title");
    }

    [Theory]
    [InlineData(EmploymentStatus.OnLeave, "On Leave")]
    [InlineData(EmploymentStatus.Active, "Active")]
    public void Employment_status_texts_round_trip(EmploymentStatus status, string text)
    {
        Assert.Equal(text, EmploymentStatusText.ToText(status));
        Assert.True(EmploymentStatusText.TryParse(text, out var parsed));
        Assert.Equal(status, parsed);
    }

    [Theory]
    [InlineData("OnLeave")]
    [InlineData("on leave")]
    [InlineData("2")]
    [InlineData(null)]
    public void Texts_are_exact_and_numbers_are_rejected(string? text)
    {
        Assert.False(EmploymentStatusText.TryParse(text, out _));
        Assert.False(GenderText.TryParse(text, out _));
    }

    [Fact]
    public void Every_enum_value_has_a_text_that_parses_back()
    {
        Assert.All(Enum.GetValues<Gender>(), value => Assert.Equal(value, GenderText.Parse(GenderText.ToText(value))));
        Assert.All(Enum.GetValues<MaritalStatus>(), value => Assert.Equal(value, MaritalStatusText.Parse(MaritalStatusText.ToText(value))));
        Assert.All(Enum.GetValues<EmploymentType>(), value => Assert.Equal(value, EmploymentTypeText.Parse(EmploymentTypeText.ToText(value))));
        Assert.All(Enum.GetValues<EmploymentStatus>(), value => Assert.Equal(value, EmploymentStatusText.Parse(EmploymentStatusText.ToText(value))));
        Assert.All(Enum.GetValues<EmergencyRelationship>(), value => Assert.Equal(value, EmergencyRelationshipText.Parse(EmergencyRelationshipText.ToText(value))));
        Assert.All(Enum.GetValues<DocumentType>(), value => Assert.Equal(value, DocumentTypeText.Parse(DocumentTypeText.ToText(value))));
        Assert.All(Enum.GetValues<HistoryEventType>(), value => Assert.Equal(value, HistoryEventTypeText.Parse(HistoryEventTypeText.ToText(value))));
        Assert.Equal("Male, Female or Other", GenderText.Map.OneOf);
    }

    private static void FailedOn(Result result, string field)
    {
        Assert.True(result.IsFailure, "Expected a failure.");
        Assert.NotNull(result.Error.FieldErrors);
        Assert.Contains(field, result.Error.FieldErrors.Keys);
    }
}
