using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Organization.Branches;
using Apex.Hrms.Application.Organization.Companies;
using Apex.Hrms.Application.Organization.JobGrades;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;
using Apex.Hrms.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.IntegrationTests;

/// <summary>
/// UNIQUE and CHECK constraints. The first tests of each group go through the Application pre-checks; the "race"
/// and "bypassed" tests make the database raise the error itself, which is the only thing that stops two sessions.
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
[Trait("Category", "Integration")]
public sealed class ConstraintTests : DatabaseTest
{
    private const int CheckViolation = 547;

    [IntegrationFact]
    public async Task A_duplicate_company_code_is_a_validation_error_on_code()
    {
        await SeedCompanyAsync("A", "Test Company A");

        var result = await Create(Requests.Company("A", "Another Test Company"));

        Assert.Equal("Company code must be unique.", FailedOnField(result, "code"));
        Assert.Equal($"Company code \"{Code("A")}\" already exists.", result.Error.Message);
    }

    [IntegrationFact]
    public async Task A_duplicate_company_name_is_a_validation_error_on_name()
    {
        await SeedCompanyAsync("A", "Test Company A");

        var result = await Create(Requests.Company("B", "Test Company A"));

        Assert.Equal("Company name must be unique.", FailedOnField(result, "name"));
    }

    [IntegrationFact]
    public async Task Codes_and_names_compare_case_insensitively_under_the_database_collation()
    {
        await SeedCompanyAsync("A", "Test Company A");

        var result = await Create(Requests.Company("B", "TEST COMPANY a"));

        FailedOnField(result, "name");
    }

    [IntegrationFact]
    public async Task Updating_a_company_to_the_code_of_another_one_is_a_validation_error_on_code()
    {
        await SeedCompanyAsync("A", "Test Company A");
        var second = await SeedCompanyAsync("B", "Test Company B");

        var result = await Update(second.Id, Requests.CompanyUpdate(second, code: "A"));

        FailedOnField(result, "code");
    }

    [IntegrationFact]
    public async Task A_code_taken_between_the_pre_check_and_the_save_is_a_validation_error_on_code()
    {
        BeforeNextSave = () => RawRows.CompanyAsync(Code("A"), Name("Raw Winner Company"));

        var result = await Create(Requests.Company("A", "Test Company A"));

        Assert.Equal("Code must be unique.", FailedOnField(result, "code"));
        Assert.Equal("A record with the same code already exists.", result.Error.Message);
        Assert.Equal(1, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM org.Company WHERE Code = @code", ("@code", Code("A"))));
    }

    [IntegrationFact]
    public async Task A_name_taken_between_the_pre_check_and_the_save_is_a_validation_error_on_name()
    {
        BeforeNextSave = () => RawRows.CompanyAsync(Code("RAW"), Name("Test Company A"));

        var result = await Create(Requests.Company("A", "Test Company A"));

        Assert.Equal("Name must be unique.", FailedOnField(result, "name"));
        Assert.Equal("A record with the same name already exists.", result.Error.Message);
    }

    [IntegrationFact]
    public async Task A_branch_code_taken_between_the_pre_check_and_the_save_is_a_validation_error_on_code()
    {
        var company = await SeedCompanyAsync();
        BeforeNextSave = () => RawRows.BranchAsync(company.Id, Code("A-YGN"), Name("Raw Winner Office"));

        var result = await Create(Requests.Branch(company.Id, "A-YGN"));

        Assert.Equal("Code must be unique.", FailedOnField(result, "code"));
    }

    [IntegrationFact]
    public async Task The_error_never_repeats_the_raw_sql_message()
    {
        BeforeNextSave = () => RawRows.CompanyAsync(Code("A"), Name("Raw Winner Company"));

        var result = await Create(Requests.Company("A", "Test Company A"));

        Assert.DoesNotContain("duplicate key", result.Error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UQ_Company", result.Error.Message, StringComparison.Ordinal);
    }

    [IntegrationFact]
    public async Task The_database_rejects_a_job_grade_whose_maximum_is_below_its_minimum()
    {
        var grade = await SeedJobGradeAsync();

        var error = await Sql.ErrorNumberOfAsync(
            "UPDATE org.JobGrade SET MaxSalary = 50 WHERE Id = @id AND MinSalary = 100",
            ("@id", grade.Id));

        Assert.Equal(CheckViolation, error);
    }

    [IntegrationFact]
    public async Task A_job_grade_maximum_below_the_minimum_that_bypasses_the_domain_is_a_validation_error_on_maxSalary()
    {
        var grade = await SeedJobGradeAsync();

        var result = await InScopeAsync(async services =>
        {
            var tracked = await services.GetRequiredService<IRepository<JobGrade>>().FindAsync(grade.Id, default);
            SetPrivate(tracked!, nameof(JobGrade.MaxSalary), 50m);
            return await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(default);
        });

        var error = Failed(result, ErrorType.Validation);
        Assert.Equal("A value is not allowed by a database rule.", error.Message);
        Assert.Equal("This value is not allowed.", Assert.Single(error.FieldErrors!["maxSalary"]));
        Assert.Equal(200m, await Sql.ScalarAsync<decimal>("SELECT MaxSalary FROM org.JobGrade WHERE Id = @id", ("@id", grade.Id)));
    }

    [IntegrationFact]
    public async Task A_code_that_bypasses_the_domain_and_breaks_the_code_check_is_a_validation_error_on_code()
    {
        var company = await SeedCompanyAsync();

        var result = await InScopeAsync(async services =>
        {
            var tracked = await services.GetRequiredService<IRepository<Company>>().FindAsync(company.Id, default);
            SetPrivate(tracked!, nameof(Company.Code), "TST A");
            return await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(default);
        });

        Assert.Equal("This value is not allowed.", FailedOnField(result, "code"));
    }

    [IntegrationFact]
    public async Task A_position_cannot_report_to_itself_even_when_the_domain_check_is_bypassed()
    {
        var company = await SeedCompanyAsync();
        var branch = await SeedBranchAsync(company.Id);
        var department = await SeedDepartmentAsync(company.Id, branch.Id);
        var grade = await SeedJobGradeAsync();
        var position = await SeedPositionAsync(department.Id, grade.Id);

        var error = await Sql.ErrorNumberOfAsync(
            "UPDATE org.Position SET ReportsToPositionId = Id WHERE Id = @id",
            ("@id", position.Id));

        Assert.Equal(CheckViolation, error);
    }

    [IntegrationFact]
    public async Task A_valid_job_grade_band_is_accepted_with_a_maximum_equal_to_the_minimum()
    {
        var grade = await SeedJobGradeAsync();

        var updated = Succeeded(await Update(grade.Id, Requests.JobGradeUpdate(grade, min: 150m, max: 150m)));

        Assert.Equal((150m, 150m), (updated.MinSalary, updated.MaxSalary));
    }

    [IntegrationFact]
    public async Task A_branch_with_a_missing_company_is_a_validation_error_not_a_foreign_key_failure()
    {
        var result = await Create(Requests.Branch(companyId: 999_999));

        FailedOnField(result, "companyId");
    }

    private static void SetPrivate(object target, string property, object value) =>
        target.GetType().GetProperty(property)!.DeclaringType!.GetProperty(property)!.SetValue(target, value);
}
