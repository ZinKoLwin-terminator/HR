using Apex.Hrms.Application.Employees.Documents;
using Apex.Hrms.Application.Employees.Salary;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Tests.Employees;

/// <summary>
/// Employee documents (metadata CRUD) and the salary summary through the real handlers. The document rules follow the frontend
/// mock (the employee must exist) plus the server's own: plain file names, a known type, no upload date in the future.
/// The fake clock says today is Monday 2026-11-09. All data is fictional.
/// </summary>
public sealed class EmployeeExtrasHandlerTests : IDisposable
{
    private readonly TestHost host = new();

    public void Dispose() => host.Dispose();

    private static EmployeeDocumentCreateRequest NewDocument(
        int employeeId,
        string type = "Work Permit",
        string title = "Work permit",
        string fileName = "permit.pdf",
        DateOnly? uploadedOn = null,
        DateOnly? expiry = null) => new(employeeId, type, title, fileName, uploadedOn, expiry);

    private static EmployeeDocumentUpdateRequest UpdateOf(EmployeeDocumentDto current, string? title = null, int? employeeId = null, byte[]? rowVersion = null) => new(
        employeeId ?? current.EmployeeId,
        current.Type,
        title ?? current.Title,
        current.FileName,
        current.UploadedOn,
        current.ExpiryDate,
        Requests.Version(rowVersion ?? current.RowVersion));

    private Task<Result<EmployeeDocumentDto>> Create(EmployeeDocumentCreateRequest request) =>
        host.Get<ICreateHandler<EmployeeDocumentCreateRequest, EmployeeDocumentDto>>().HandleAsync(request, default);

    private async Task<EmployeeDocumentDto> CreateAsync(EmployeeDocumentCreateRequest request) => ResultAssert.Succeeded(await Create(request));

    // ---- Documents ----

    [Fact]
    public async Task A_document_is_created_with_the_type_text_default_upload_date_and_the_mock_activity_wording()
    {
        var seed = await host.SeedPayrollAsync();

        var created = await CreateAsync(NewDocument(seed.Employee.Id, title: "  Work permit  ", expiry: new DateOnly(2027, 1, 1)));

        Assert.Equal("Work Permit", created.Type);
        Assert.Equal("Work permit", created.Title);
        Assert.Equal(new DateOnly(2026, 11, 9), created.UploadedOn);
        Assert.Equal(new DateOnly(2027, 1, 1), created.ExpiryDate);
        Assert.Equal("Document \"Work permit\" created", host.ActivityLog.LastMessage);
        Assert.Equal("employee", host.ActivityLog.Entries[^1].Category);
        Assert.DoesNotContain("permit.pdf", host.ActivityLog.LastMessage);
    }

    [Fact]
    public async Task A_document_needs_an_existing_employee()
    {
        var result = await Create(NewDocument(employeeId: 9999));

        ResultAssert.FailedOnField(result, "employeeId", "The selected employee does not exist.");
    }

    [Theory]
    [InlineData("folder/file.pdf")]
    [InlineData("..\\secret.pdf")]
    [InlineData("C:\\temp\\file.pdf")]
    [InlineData("a\tb.pdf")]
    [InlineData("a\nb.pdf")]
    [InlineData("..")]
    public async Task A_file_name_must_be_a_plain_name(string fileName)
    {
        var seed = await host.SeedPayrollAsync();

        ResultAssert.FailedOnField(await Create(NewDocument(seed.Employee.Id, fileName: fileName)), "fileName");
    }

    [Fact]
    public async Task The_type_must_be_one_of_the_allowed_types_and_the_texts_and_date_have_limits()
    {
        var seed = await host.SeedPayrollAsync();

        ResultAssert.FailedOnField(await Create(NewDocument(seed.Employee.Id, type: "Secret")), "type");
        ResultAssert.FailedOnField(await Create(NewDocument(seed.Employee.Id, title: new string('t', 81))), "title");
        ResultAssert.FailedOnField(await Create(NewDocument(seed.Employee.Id, title: " ")), "title");
        ResultAssert.FailedOnField(await Create(NewDocument(seed.Employee.Id, fileName: new string('f', 261))), "fileName");
        ResultAssert.FailedOnField(
            await Create(NewDocument(seed.Employee.Id, uploadedOn: new DateOnly(2026, 11, 10))),
            "uploadedOn",
            "Uploaded on cannot be in the future.");
    }

    [Fact]
    public async Task An_update_changes_the_fields_but_not_the_employee_and_needs_the_current_row_version()
    {
        var seed = await host.SeedPayrollAsync();
        var other = await host.CreatePayrollEmployeeAsync(seed.Org, "bo.test@example.test");
        var created = await CreateAsync(NewDocument(seed.Employee.Id));
        var update = host.Get<IUpdateHandler<EmployeeDocumentUpdateRequest, EmployeeDocumentDto>>();

        var updated = ResultAssert.Succeeded(await update.HandleAsync(created.Id, UpdateOf(created, title: "Renewed permit"), default));
        var moved = await update.HandleAsync(created.Id, UpdateOf(updated, employeeId: other.Id), default);
        var stale = await update.HandleAsync(created.Id, UpdateOf(updated, title: "Again", rowVersion: created.RowVersion), default);
        var unversioned = await update.HandleAsync(created.Id, UpdateOf(updated) with { RowVersion = null }, default);

        Assert.Equal("Renewed permit", updated.Title);
        Assert.Equal("Document \"Renewed permit\" updated", host.ActivityLog.LastMessage);
        ResultAssert.FailedOnField(moved, "employeeId");
        ResultAssert.Failed(stale, ErrorType.Conflict);
        ResultAssert.FailedOnField(unversioned, "rowVersion");
    }

    [Fact]
    public async Task A_missing_document_is_not_found_and_a_delete_needs_the_current_row_version()
    {
        var seed = await host.SeedPayrollAsync();
        var created = await CreateAsync(NewDocument(seed.Employee.Id));
        var delete = host.Get<IDeleteHandler<EmployeeDocumentDto>>();
        var get = host.Get<IGetByIdHandler<EmployeeDocumentDto>>();

        ResultAssert.Failed(await get.HandleAsync(9999, default), ErrorType.NotFound);
        ResultAssert.Failed(await delete.HandleAsync(created.Id, Requests.Version(new byte[8]), default), ErrorType.Conflict);
        Assert.True((await delete.HandleAsync(created.Id, Requests.Version(created.RowVersion), default)).IsSuccess);
        Assert.Equal("Document \"Work permit\" deleted", host.ActivityLog.LastMessage);
        ResultAssert.Failed(await get.HandleAsync(created.Id, default), ErrorType.NotFound);
    }

    [Fact]
    public async Task The_query_filters_by_employee_and_type_and_searches_title_file_name_and_type()
    {
        var seed = await host.SeedPayrollAsync();
        var other = await host.CreatePayrollEmployeeAsync(seed.Org, "bo.test@example.test");
        await CreateAsync(NewDocument(seed.Employee.Id, "Passport", "Passport scan", "scan.pdf"));
        await CreateAsync(NewDocument(seed.Employee.Id, "Resume", "CV", "cv.docx"));
        await CreateAsync(NewDocument(other.Id, "Passport", "Other passport", "p2.pdf"));
        var query = host.Get<IQueryHandler<EmployeeDocumentDto, EmployeeDocumentFilter>>();

        async Task<int> CountAsync(EmployeeDocumentFilter filter, string? search = null) =>
            ResultAssert.Succeeded(await query.HandleAsync(new PageRequest<EmployeeDocumentFilter>(0, 20, null, search, filter), default)).Total;

        Assert.Equal(2, await CountAsync(new EmployeeDocumentFilter(EmployeeId: seed.Employee.Id)));
        Assert.Equal(2, await CountAsync(new EmployeeDocumentFilter(Type: "Passport")));
        Assert.Equal(0, await CountAsync(new EmployeeDocumentFilter(Type: "Nonsense")));
        Assert.Equal(1, await CountAsync(new EmployeeDocumentFilter(), "cv.docx"));
        Assert.Equal(1, await CountAsync(new EmployeeDocumentFilter(), "resume"));
    }

    // ---- Salary summary ----

    private Task<Result<EmployeeSalarySummaryDto>> Summary(int employeeId) =>
        host.Get<IGetByIdHandler<EmployeeSalarySummaryDto>>().HandleAsync(employeeId, default);

    [Fact]
    public async Task The_summary_of_an_unknown_employee_is_not_found()
    {
        ResultAssert.Failed(await Summary(9999), ErrorType.NotFound, "Employee #9999 was not found.");
    }

    [Fact]
    public async Task An_employee_without_an_assignment_at_today_has_no_summary()
    {
        var org = await host.SeedPayrollOrgAsync();
        var employee = await host.CreatePayrollEmployeeAsync(org, "cy.test@example.test");

        var error = ResultAssert.Failed(await Summary(employee.Id), ErrorType.NotFound);

        Assert.Equal($"{employee.FirstName} {employee.LastName} has no salary assignment yet.", error.Message);
    }

    [Fact]
    public async Task An_assignment_that_starts_in_the_future_does_not_count()
    {
        var org = await host.SeedPayrollOrgAsync();
        var housing = await host.CreateComponentAsync(PayrollFixtures.NewComponent());
        var structure = await host.CreateStructureAsync(PayrollFixtures.NewStructure("STAFF", "Staff", housing.Id));
        var employee = await host.CreatePayrollEmployeeAsync(org, "cy.test@example.test");
        await host.CreateAssignmentAsync(PayrollFixtures.NewAssignment(employee.Id, structure.Id, from: new DateOnly(2026, 12, 1)));

        ResultAssert.Failed(await Summary(employee.Id), ErrorType.NotFound);
    }

    [Fact]
    public async Task The_summary_is_a_full_month_at_the_current_salary_with_the_numbers_of_the_mock()
    {
        var seed = await host.SeedPayrollAsync();

        var summary = ResultAssert.Succeeded(await Summary(seed.Employee.Id));

        // Basic 1000; Housing 10% of basic = 100 and Transport fixed 50 give gross 1150 and allowances 150;
        // Tax 5% and Social 2% of gross = 57.50 + 23.00 = 80.50; net 1069.50. Band 500 to 5000: (1000-500)/4500 = 11%.
        Assert.Equal(seed.Employee.Id, summary.EmployeeId);
        Assert.Equal(1000m, summary.Basic);
        Assert.Equal(150m, summary.Allowances);
        Assert.Equal(1150m, summary.Gross);
        Assert.Equal(80.5m, summary.Deductions);
        Assert.Equal(1069.5m, summary.Net);
        Assert.Equal("PG1", summary.GradeCode);
        Assert.Equal(500m, summary.BandMin);
        Assert.Equal(5000m, summary.BandMax);
        Assert.Equal(11, summary.BandPosition);
        Assert.Equal(
            [
                "Basic salary comes from the current assignment: Staff, effective 2020-01-01.",
                "Housing (earning): 10% of basic.",
                "Transport (earning): Fixed 50.",
                "Tax (deduction): 5% of gross.",
                "Social (deduction): 2% of gross.",
                "These are demonstration rules, not real tax or statutory calculations.",
            ],
            summary.Assumptions);
    }

    [Fact]
    public async Task The_latest_assignment_in_force_is_used()
    {
        var seed = await host.SeedPayrollAsync();
        await host.CreateAssignmentAsync(PayrollFixtures.NewAssignment(seed.Employee.Id, seed.Structure.Id, 2000m, new DateOnly(2026, 6, 1)));

        var summary = ResultAssert.Succeeded(await Summary(seed.Employee.Id));

        Assert.Equal(2000m, summary.Basic);
        Assert.Equal(33, summary.BandPosition);
    }

    [Fact]
    public async Task The_band_position_is_clamped_to_0_and_100_when_the_grade_band_moved_away_from_the_salary()
    {
        var seed = await host.SeedPayrollAsync();
        var grade = (await host.Repository<JobGrade>().FindAsync(seed.Org.Grade.Id, default))!;

        Assert.True(grade.Update(new JobGradeDetails(grade.Code, grade.Name, grade.Level, 1200m, 3000m, null)).IsSuccess);
        Assert.Equal(0, ResultAssert.Succeeded(await Summary(seed.Employee.Id)).BandPosition);

        Assert.True(grade.Update(new JobGradeDetails(grade.Code, grade.Name, grade.Level, 100m, 800m, null)).IsSuccess);
        Assert.Equal(100, ResultAssert.Succeeded(await Summary(seed.Employee.Id)).BandPosition);

        Assert.True(grade.Update(new JobGradeDetails(grade.Code, grade.Name, grade.Level, 1000m, 1000m, null)).IsSuccess);
        Assert.Equal(0, ResultAssert.Succeeded(await Summary(seed.Employee.Id)).BandPosition);
    }
}
