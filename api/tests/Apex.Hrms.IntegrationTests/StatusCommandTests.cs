using Apex.Hrms.Application.Organization.Branches;
using Apex.Hrms.Application.Organization.Companies;
using Apex.Hrms.Application.Organization.Departments;
using Apex.Hrms.Application.Organization.JobGrades;
using Apex.Hrms.Application.Organization.Positions;
using Apex.Hrms.Application.Organization.Sections;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.IntegrationTests.Support;

namespace Apex.Hrms.IntegrationTests;

/// <summary>Activate and deactivate: the command persists, returns the new state and hands out a new row version.</summary>
[Collection(SqlServerFixtureDefinition.Name)]
[Trait("Category", "Integration")]
public sealed class StatusCommandTests : DatabaseTest
{
    [IntegrationFact]
    public async Task Deactivate_then_activate_persist_and_return_the_new_state_with_a_new_row_version()
    {
        var company = await SeedCompanyAsync();

        var inactive = Succeeded(await Deactivate<CompanyDto>(company.Id, Requests.Version(company.RowVersion)));

        Assert.Equal("Inactive", inactive.Status);
        Assert.NotEqual(company.RowVersion, inactive.RowVersion);
        Assert.Equal("Inactive", await Sql.ScalarAsync<string>("SELECT Status FROM org.Company WHERE Id = @id", ("@id", company.Id)));

        var active = Succeeded(await Activate<CompanyDto>(company.Id, Requests.Version(inactive.RowVersion)));

        Assert.Equal("Active", active.Status);
        Assert.NotEqual(inactive.RowVersion, active.RowVersion);
        Assert.Equal("Active", await Sql.ScalarAsync<string>("SELECT Status FROM org.Company WHERE Id = @id", ("@id", company.Id)));
    }

    [IntegrationFact]
    public async Task Activating_an_active_record_succeeds_and_changes_nothing()
    {
        var company = await SeedCompanyAsync();

        var again = Succeeded(await Activate<CompanyDto>(company.Id, Requests.Version(company.RowVersion)));

        Assert.Equal("Active", again.Status);
        Assert.Equal(company.RowVersion, again.RowVersion);
    }

    [IntegrationFact]
    public async Task Every_organization_resource_can_be_deactivated_and_activated()
    {
        var company = await SeedCompanyAsync();
        var branch = await SeedBranchAsync(company.Id);
        var department = await SeedDepartmentAsync(company.Id, branch.Id);
        var section = await SeedSectionAsync(department.Id);
        var grade = await SeedJobGradeAsync();
        var position = await SeedPositionAsync(department.Id, grade.Id);

        await AssertTogglesAsync<CompanyDto>(company.Id, company.RowVersion, "org.Company", dto => dto.RowVersion);
        await AssertTogglesAsync<BranchDto>(branch.Id, branch.RowVersion, "org.Branch", dto => dto.RowVersion);
        await AssertTogglesAsync<DepartmentDto>(department.Id, department.RowVersion, "org.Department", dto => dto.RowVersion);
        await AssertTogglesAsync<SectionDto>(section.Id, section.RowVersion, "org.Section", dto => dto.RowVersion);
        await AssertTogglesAsync<JobGradeDto>(grade.Id, grade.RowVersion, "org.JobGrade", dto => dto.RowVersion);
        await AssertTogglesAsync<PositionDto>(position.Id, position.RowVersion, "org.Position", dto => dto.RowVersion);
    }

    [IntegrationFact]
    public async Task An_inactive_company_cannot_receive_a_new_branch_and_an_active_one_again_can()
    {
        var company = await SeedCompanyAsync();
        var inactive = Succeeded(await Deactivate<CompanyDto>(company.Id, Requests.Version(company.RowVersion)));

        var rejected = await Create(Requests.Branch(company.Id));

        Assert.Equal(
            $"Company \"{Name("Test Company A")}\" is inactive and cannot receive new branches.",
            Failed(rejected, ErrorType.Validation).Message);
        FailedOnField(rejected, "companyId");

        Succeeded(await Activate<CompanyDto>(company.Id, Requests.Version(inactive.RowVersion)));
        Succeeded(await Create(Requests.Branch(company.Id)));
    }

    [IntegrationFact]
    public async Task A_record_can_be_created_inactive()
    {
        var created = Succeeded(await Create(Requests.Company(status: "Inactive")));

        Assert.Equal("Inactive", created.Status);
        Assert.Equal("Inactive", await Sql.ScalarAsync<string>("SELECT Status FROM org.Company WHERE Id = @id", ("@id", created.Id)));
    }

    private async Task AssertTogglesAsync<TDto>(int id, byte[] rowVersion, string table, Func<TDto, byte[]> versionOf)
    {
        var inactive = Succeeded(await Deactivate<TDto>(id, Requests.Version(rowVersion)));
        Assert.Equal("Inactive", await Sql.ScalarAsync<string>($"SELECT Status FROM {table} WHERE Id = @id", ("@id", id)));
        Assert.NotEqual(rowVersion, versionOf(inactive));

        var active = Succeeded(await Activate<TDto>(id, Requests.Version(versionOf(inactive))));
        Assert.Equal("Active", await Sql.ScalarAsync<string>($"SELECT Status FROM {table} WHERE Id = @id", ("@id", id)));
        Assert.NotEqual(versionOf(inactive), versionOf(active));
    }
}
