using Apex.Hrms.Application.References;
using Apex.Hrms.Application.Organization.Branches;
using Apex.Hrms.Application.Organization.Companies;
using Apex.Hrms.Application.Organization.Departments;
using Apex.Hrms.Application.Organization.JobGrades;
using Apex.Hrms.Application.Organization.Positions;
using Apex.Hrms.Application.Organization.Sections;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;
using Apex.Hrms.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Apex.Hrms.IntegrationTests;

[Collection(SqlServerFixtureDefinition.Name)]
[Trait("Category", "Integration")]
public sealed class DeleteProtectionAndAuditTests : DatabaseTest
{
    [IntegrationFact]
    public async Task A_company_with_branches_cannot_be_deleted()
    {
        var company = await SeedCompanyAsync();
        await SeedBranchAsync(company.Id);

        var result = await Delete<CompanyDto>(company.Id, Requests.Version(company.RowVersion));

        Assert.Equal(
            $"\"{Name("Test Company A")}\" cannot be deleted because it is used by 1 branch(es). Deactivate it instead.",
            Failed(result, ErrorType.Conflict).Message);
        Assert.Equal(1, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM org.Company WHERE Id = @id", ("@id", company.Id)));
    }

    [IntegrationFact]
    public async Task The_message_lists_every_kind_of_record_that_uses_the_company()
    {
        var company = await SeedCompanyAsync();
        var branch = await SeedBranchAsync(company.Id);
        await SeedBranchAsync(company.Id, "A-MDY", "Mandalay Test Office");
        await SeedDepartmentAsync(company.Id, branch.Id);

        var result = await Delete<CompanyDto>(company.Id, Requests.Version(company.RowVersion));

        Assert.Equal(
            $"\"{Name("Test Company A")}\" cannot be deleted because it is used by 2 branch(es), 1 department(s). Deactivate it instead.",
            Failed(result, ErrorType.Conflict).Message);
    }

    [IntegrationFact]
    public async Task A_company_can_be_deleted_once_its_branch_is_gone()
    {
        var company = await SeedCompanyAsync();
        var branch = await SeedBranchAsync(company.Id);
        Assert.True((await Delete<BranchDto>(branch.Id, Requests.Version(branch.RowVersion))).IsSuccess);

        var result = await Delete<CompanyDto>(company.Id, Requests.Version(company.RowVersion));

        Assert.True(result.IsSuccess);
        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM org.Company WHERE Id = @id", ("@id", company.Id)));
    }

    [IntegrationFact]
    public async Task A_job_grade_used_by_a_position_cannot_be_deleted()
    {
        var company = await SeedCompanyAsync();
        var branch = await SeedBranchAsync(company.Id);
        var department = await SeedDepartmentAsync(company.Id, branch.Id);
        var grade = await SeedJobGradeAsync();
        await SeedPositionAsync(department.Id, grade.Id);

        var result = await Delete<JobGradeDto>(grade.Id, Requests.Version(grade.RowVersion));

        Assert.Equal(
            $"\"{Code("G1")} {Name("Test Grade")}\" cannot be deleted because it is used by 1 position(s). Deactivate it instead.",
            Failed(result, ErrorType.Conflict).Message);
    }

    [IntegrationFact]
    public async Task A_department_with_sections_or_positions_and_a_branch_with_departments_cannot_be_deleted()
    {
        var company = await SeedCompanyAsync();
        var branch = await SeedBranchAsync(company.Id);
        var department = await SeedDepartmentAsync(company.Id, branch.Id);
        var section = await SeedSectionAsync(department.Id);
        var grade = await SeedJobGradeAsync();
        var position = await SeedPositionAsync(department.Id, grade.Id, sectionId: section.Id);

        Assert.Contains("1 department(s)", Failed(await Delete<BranchDto>(branch.Id, Requests.Version(branch.RowVersion)), ErrorType.Conflict).Message);
        Assert.Contains("1 section(s), 1 position(s)", Failed(await Delete<DepartmentDto>(department.Id, Requests.Version(department.RowVersion)), ErrorType.Conflict).Message);
        Assert.Contains("1 position(s)", Failed(await Delete<SectionDto>(section.Id, Requests.Version(section.RowVersion)), ErrorType.Conflict).Message);

        Assert.True((await Delete<PositionDto>(position.Id, Requests.Version(position.RowVersion))).IsSuccess);
        Assert.True((await Delete<SectionDto>(section.Id, Requests.Version(section.RowVersion))).IsSuccess);
    }

    [IntegrationFact]
    public async Task The_foreign_key_stops_a_company_delete_when_the_pre_check_is_bypassed()
    {
        Customize(services => services.RemoveAll<IReferenceCheck<Company>>());
        var company = await SeedCompanyAsync();
        await SeedBranchAsync(company.Id);

        var result = await Delete<CompanyDto>(company.Id, Requests.Version(company.RowVersion));

        Assert.Equal("The change conflicts with related records.", Failed(result, ErrorType.Conflict).Message);
        Assert.Equal(1, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM org.Company WHERE Id = @id", ("@id", company.Id)));
    }

    [IntegrationFact]
    public async Task A_new_record_is_stamped_with_the_creation_time_for_both_audit_columns()
    {
        var company = await SeedCompanyAsync();

        var (created, modified) = await ReadStampsAsync(company.Id);

        Assert.Equal(Time.GetUtcNow().UtcDateTime, created);
        Assert.Equal(Time.GetUtcNow().UtcDateTime, modified);
    }

    [IntegrationFact]
    public async Task An_update_moves_ModifiedAtUtc_and_leaves_CreatedAtUtc()
    {
        var company = await SeedCompanyAsync();
        var created = Time.GetUtcNow().UtcDateTime;
        Time.Advance(TimeSpan.FromHours(1));

        Succeeded(await Update(company.Id, Requests.CompanyUpdate(company, city: "Mandalay")));

        var stamps = await ReadStampsAsync(company.Id);
        Assert.Equal(created, stamps.Created);
        Assert.Equal(created.AddHours(1), stamps.Modified);
    }

    [IntegrationFact]
    public async Task A_status_change_also_moves_ModifiedAtUtc()
    {
        var company = await SeedCompanyAsync();
        var created = Time.GetUtcNow().UtcDateTime;
        Time.Advance(TimeSpan.FromMinutes(30));

        Succeeded(await Deactivate<CompanyDto>(company.Id, Requests.Version(company.RowVersion)));

        var stamps = await ReadStampsAsync(company.Id);
        Assert.Equal(created, stamps.Created);
        Assert.Equal(created.AddMinutes(30), stamps.Modified);
    }

    [IntegrationFact]
    public async Task A_second_record_created_later_gets_its_own_creation_time()
    {
        var first = await SeedCompanyAsync("A", "Test Company A");
        Time.Advance(TimeSpan.FromDays(2));
        var second = await SeedCompanyAsync("B", "Test Company B");

        Assert.True((await ReadStampsAsync(second.Id)).Created > (await ReadStampsAsync(first.Id)).Created);
    }

    private static async Task<(DateTime Created, DateTime Modified)> ReadStampsAsync(int companyId)
    {
        var created = await Sql.ScalarAsync<DateTime>("SELECT CreatedAtUtc FROM org.Company WHERE Id = @id", ("@id", companyId));
        var modified = await Sql.ScalarAsync<DateTime>("SELECT ModifiedAtUtc FROM org.Company WHERE Id = @id", ("@id", companyId));
        return (created, modified);
    }
}
