using Apex.Hrms.Application.Organization.Branches;
using Apex.Hrms.Application.Organization.Companies;
using Apex.Hrms.Application.Organization.Departments;
using Apex.Hrms.Application.Organization.JobGrades;
using Apex.Hrms.Application.Organization.Positions;
using Apex.Hrms.Application.Organization.Sections;
using Apex.Hrms.Application.References;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;
using Apex.Hrms.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Apex.Hrms.IntegrationTests;

/// <summary>
/// Moving a record under another parent. Every test here used to pass against fakes while the real EF model threw
/// (a 500) because the composite foreign keys made the parent column immutable.
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
[Trait("Category", "Integration")]
public sealed class ReparentingTests : DatabaseTest
{
    [IntegrationFact]
    public async Task A_branch_without_children_moves_to_another_company()
    {
        var companyA = await SeedCompanyAsync("A", "Test Company A");
        var companyB = await SeedCompanyAsync("B", "Test Company B");
        var branch = await SeedBranchAsync(companyA.Id);

        var moved = Succeeded(await Update(branch.Id, Requests.BranchUpdate(branch, companyId: companyB.Id)));

        Assert.Equal(companyB.Id, moved.CompanyId);
        Assert.Equal(companyB.Id, (await Get<BranchDto>(branch.Id)).Value.CompanyId);
        Assert.Equal(companyB.Id, await Sql.ScalarAsync<int>("SELECT CompanyId FROM org.Branch WHERE Id = @id", ("@id", branch.Id)));
    }

    [IntegrationFact]
    public async Task A_department_without_children_moves_to_another_branch_of_its_company()
    {
        var company = await SeedCompanyAsync();
        var north = await SeedBranchAsync(company.Id, "A-N", "North Test Office");
        var south = await SeedBranchAsync(company.Id, "A-S", "South Test Office");
        var department = await SeedDepartmentAsync(company.Id, north.Id);

        var moved = Succeeded(await Update(department.Id, Requests.DepartmentUpdate(department, branchId: south.Id)));

        Assert.Equal(south.Id, moved.BranchId);
        Assert.Equal(south.Id, await Sql.ScalarAsync<int>("SELECT BranchId FROM org.Department WHERE Id = @id", ("@id", department.Id)));
    }

    [IntegrationFact]
    public async Task A_department_moves_to_a_branch_of_another_company_together_with_its_company()
    {
        var companyA = await SeedCompanyAsync("A", "Test Company A");
        var companyB = await SeedCompanyAsync("B", "Test Company B");
        var branchA = await SeedBranchAsync(companyA.Id, "A-YGN", "Yangon Test Office");
        var branchB = await SeedBranchAsync(companyB.Id, "B-YGN", "Yangon Second Office");
        var department = await SeedDepartmentAsync(companyA.Id, branchA.Id);

        var moved = Succeeded(await Update(department.Id, Requests.DepartmentUpdate(department, companyId: companyB.Id, branchId: branchB.Id)));

        Assert.Equal((companyB.Id, branchB.Id), (moved.CompanyId, moved.BranchId));
    }

    [IntegrationFact]
    public async Task A_section_without_children_moves_to_another_department()
    {
        var company = await SeedCompanyAsync();
        var branch = await SeedBranchAsync(company.Id);
        var engineering = await SeedDepartmentAsync(company.Id, branch.Id, "A-ENG", "Engineering");
        var finance = await SeedDepartmentAsync(company.Id, branch.Id, "A-FIN", "Finance");
        var section = await SeedSectionAsync(engineering.Id);

        var moved = Succeeded(await Update(section.Id, Requests.SectionUpdate(section, departmentId: finance.Id)));

        Assert.Equal(finance.Id, moved.DepartmentId);
        Assert.Equal(finance.Id, await Sql.ScalarAsync<int>("SELECT DepartmentId FROM org.Section WHERE Id = @id", ("@id", section.Id)));
    }

    [IntegrationFact]
    public async Task A_position_moves_to_another_department_and_into_one_of_its_sections()
    {
        var company = await SeedCompanyAsync();
        var branch = await SeedBranchAsync(company.Id);
        var engineering = await SeedDepartmentAsync(company.Id, branch.Id, "A-ENG", "Engineering");
        var finance = await SeedDepartmentAsync(company.Id, branch.Id, "A-FIN", "Finance");
        var accounting = await SeedSectionAsync(finance.Id, "A-ACC", "Accounting");
        var grade = await SeedJobGradeAsync();
        var position = await SeedPositionAsync(engineering.Id, grade.Id);

        var moved = Succeeded(await Update(position.Id, Requests.PositionUpdate(position, departmentId: finance.Id, sectionId: accounting.Id)));

        Assert.Equal((finance.Id, (int?)accounting.Id), (moved.DepartmentId, moved.SectionId));
    }

    [IntegrationFact]
    public async Task A_position_moves_to_another_department_when_it_has_no_section()
    {
        var company = await SeedCompanyAsync();
        var branch = await SeedBranchAsync(company.Id);
        var engineering = await SeedDepartmentAsync(company.Id, branch.Id, "A-ENG", "Engineering");
        var finance = await SeedDepartmentAsync(company.Id, branch.Id, "A-FIN", "Finance");
        var grade = await SeedJobGradeAsync();
        var position = await SeedPositionAsync(engineering.Id, grade.Id);

        var moved = Succeeded(await Update(position.Id, Requests.PositionUpdate(position, departmentId: finance.Id)));

        Assert.Equal(finance.Id, moved.DepartmentId);
        Assert.Null(moved.SectionId);
    }

    [IntegrationFact]
    public async Task A_branch_with_a_department_cannot_move_and_the_error_names_companyId()
    {
        var companyA = await SeedCompanyAsync("A", "Test Company A");
        var companyB = await SeedCompanyAsync("B", "Test Company B");
        var branch = await SeedBranchAsync(companyA.Id);
        await SeedDepartmentAsync(companyA.Id, branch.Id);

        var result = await Update(branch.Id, Requests.BranchUpdate(branch, companyId: companyB.Id));

        var message = FailedOnField(result, "companyId");
        Assert.Equal("Remove or move its 1 department(s) first, or keep the current company.", message);
        Assert.Equal(
            $"Branch \"{Name("Yangon Test Office")}\" cannot move to another company while it has 1 department(s).",
            result.Error.Message);
        Assert.Equal(companyA.Id, (await Get<BranchDto>(branch.Id)).Value.CompanyId);
    }

    [IntegrationFact]
    public async Task A_department_with_a_section_cannot_move_and_the_error_names_branchId()
    {
        var company = await SeedCompanyAsync();
        var north = await SeedBranchAsync(company.Id, "A-N", "North Test Office");
        var south = await SeedBranchAsync(company.Id, "A-S", "South Test Office");
        var department = await SeedDepartmentAsync(company.Id, north.Id);
        await SeedSectionAsync(department.Id);

        var result = await Update(department.Id, Requests.DepartmentUpdate(department, branchId: south.Id));

        Assert.Equal("Remove or move its 1 section(s) first, or keep the current branch.", FailedOnField(result, "branchId"));
        Assert.Equal(
            $"Department \"{Name("Engineering")}\" cannot move to another branch while it has 1 section(s).",
            result.Error.Message);
    }

    [IntegrationFact]
    public async Task A_section_with_a_position_cannot_move_and_the_error_names_departmentId()
    {
        var company = await SeedCompanyAsync();
        var branch = await SeedBranchAsync(company.Id);
        var engineering = await SeedDepartmentAsync(company.Id, branch.Id, "A-ENG", "Engineering");
        var finance = await SeedDepartmentAsync(company.Id, branch.Id, "A-FIN", "Finance");
        var section = await SeedSectionAsync(engineering.Id);
        var grade = await SeedJobGradeAsync();
        await SeedPositionAsync(engineering.Id, grade.Id, sectionId: section.Id);

        var result = await Update(section.Id, Requests.SectionUpdate(section, departmentId: finance.Id));

        Assert.Equal("Remove or move its 1 position(s) first, or keep the current department.", FailedOnField(result, "departmentId"));
    }

    [IntegrationFact]
    public async Task A_department_with_a_position_cannot_move_to_another_branch()
    {
        var company = await SeedCompanyAsync();
        var north = await SeedBranchAsync(company.Id, "A-N", "North Test Office");
        var south = await SeedBranchAsync(company.Id, "A-S", "South Test Office");
        var department = await SeedDepartmentAsync(company.Id, north.Id);
        var grade = await SeedJobGradeAsync();
        await SeedPositionAsync(department.Id, grade.Id);

        var result = await Update(department.Id, Requests.DepartmentUpdate(department, branchId: south.Id));

        Assert.Equal("Remove or move its 1 position(s) first, or keep the current branch.", FailedOnField(result, "branchId"));
    }

    [IntegrationFact]
    public async Task The_composite_foreign_key_rejects_a_branch_move_when_the_pre_check_is_bypassed()
    {
        Customize(services => services.RemoveAll<IReferenceCheck<Branch>>());
        var companyA = await SeedCompanyAsync("A", "Test Company A");
        var companyB = await SeedCompanyAsync("B", "Test Company B");
        var branch = await SeedBranchAsync(companyA.Id);
        await SeedDepartmentAsync(companyA.Id, branch.Id);

        var result = await Update(branch.Id, Requests.BranchUpdate(branch, companyId: companyB.Id));

        var error = Failed(result, ErrorType.Conflict);
        Assert.Equal("The change conflicts with related records.", error.Message);
        Assert.Equal(companyA.Id, await Sql.ScalarAsync<int>("SELECT CompanyId FROM org.Branch WHERE Id = @id", ("@id", branch.Id)));
    }

    [IntegrationFact]
    public async Task A_department_cannot_move_to_a_branch_of_another_company()
    {
        var companyA = await SeedCompanyAsync("A", "Test Company A");
        var companyB = await SeedCompanyAsync("B", "Test Company B");
        var north = await SeedBranchAsync(companyA.Id, "A-N", "North Test Office");
        var foreign = await SeedBranchAsync(companyB.Id, "B-S", "South Test Office");
        var department = await SeedDepartmentAsync(companyA.Id, north.Id);
        var section = await SeedSectionAsync(department.Id);
        var grade = await SeedJobGradeAsync();
        await SeedPositionAsync(department.Id, grade.Id, sectionId: section.Id);

        // The branch belongs to another company. The application rule rejects it on branchId before SQL runs.
        var result = await Update(department.Id, Requests.DepartmentUpdate(department, branchId: foreign.Id));

        FailedOnField(result, "branchId");
        Assert.Equal(north.Id, await Sql.ScalarAsync<int>("SELECT BranchId FROM org.Department WHERE Id = @id", ("@id", department.Id)));
    }

    [IntegrationFact]
    public async Task The_composite_foreign_key_rejects_a_section_move_when_the_pre_check_is_bypassed()
    {
        Customize(services => services.RemoveAll<IReferenceCheck<Section>>());
        var company = await SeedCompanyAsync();
        var branch = await SeedBranchAsync(company.Id);
        var engineering = await SeedDepartmentAsync(company.Id, branch.Id, "A-ENG", "Engineering");
        var finance = await SeedDepartmentAsync(company.Id, branch.Id, "A-FIN", "Finance");
        var section = await SeedSectionAsync(engineering.Id);
        var grade = await SeedJobGradeAsync();
        await SeedPositionAsync(engineering.Id, grade.Id, sectionId: section.Id);

        var result = await Update(section.Id, Requests.SectionUpdate(section, departmentId: finance.Id));

        Failed(result, ErrorType.Conflict);
    }
}
