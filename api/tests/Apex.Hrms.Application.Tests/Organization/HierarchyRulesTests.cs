using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Organization.Branches;
using Apex.Hrms.Application.Organization.Departments;
using Apex.Hrms.Application.Organization.JobGrades;
using Apex.Hrms.Application.Organization.Positions;
using Apex.Hrms.Application.Organization.Sections;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Tests.Organization;

public sealed class HierarchyRulesTests : IDisposable
{
    private readonly TestHost host = new();

    public void Dispose() => host.Dispose();

    // ---- Branch ----

    [Fact]
    public async Task Branch_cannot_be_created_under_an_unknown_company()
    {
        var result = await host.Get<ICreateHandler<BranchRequest, BranchDto>>().HandleAsync(Requests.Branch(99), default);

        ResultAssert.FailedOnField(result, "companyId", "Company #99 was not found.");
    }

    [Fact]
    public async Task Branch_cannot_be_created_under_an_inactive_company()
    {
        var company = await host.CreateCompanyAsync(status: "Inactive");

        var result = await host.Get<ICreateHandler<BranchRequest, BranchDto>>().HandleAsync(Requests.Branch(company.Id), default);

        ResultAssert.FailedOnField(result, "companyId", "Company \"Apex Holdings\" is inactive and cannot receive new branches.");
    }

    [Fact]
    public async Task Branch_code_must_be_unique()
    {
        var company = await host.CreateCompanyAsync();
        await host.CreateBranchAsync(company.Id, "YGN");

        var result = await host.Get<ICreateHandler<BranchRequest, BranchDto>>().HandleAsync(Requests.Branch(company.Id, "YGN"), default);

        ResultAssert.FailedOnField(result, "code", "Branch code \"YGN\" already exists.");
    }

    [Fact]
    public async Task Branch_opened_date_cannot_be_in_the_future()
    {
        var company = await host.CreateCompanyAsync();
        var request = Requests.Branch(company.Id, opened: host.Clock.Today.AddDays(1));

        ResultAssert.FailedOnField(await host.Get<ICreateHandler<BranchRequest, BranchDto>>().HandleAsync(request, default), "openedDate");
    }

    [Fact]
    public async Task Branch_type_must_be_one_of_the_known_texts()
    {
        var company = await host.CreateCompanyAsync();
        var request = Requests.Branch(company.Id) with { Type = "Outpost" };

        ResultAssert.FailedOnField(await host.Get<ICreateHandler<BranchRequest, BranchDto>>().HandleAsync(request, default), "type");
    }

    [Fact]
    public async Task Branch_can_be_updated_while_its_company_is_inactive_as_long_as_the_company_is_unchanged()
    {
        var company = await host.CreateCompanyAsync();
        var branch = await host.CreateBranchAsync(company.Id);
        await DeactivateCompanyAsync(company);

        var result = await host.Get<IUpdateHandler<BranchRequest, BranchDto>>()
            .HandleAsync(branch.Id, Requests.Branch(company.Id) with { Type = "Warehouse" }, default);

        Assert.Equal("Warehouse", ResultAssert.Succeeded(result).Type);
    }

    [Fact]
    public async Task Branch_delete_is_blocked_by_departments()
    {
        var company = await host.CreateCompanyAsync();
        var branch = await host.CreateBranchAsync(company.Id);
        host.References.Set(ReferenceQuery.DepartmentsOfBranch, branch.Id, 3);

        var result = await host.Get<IDeleteHandler<BranchDto>>().HandleAsync(branch.Id, default);

        ResultAssert.Failed(
            result,
            ErrorType.Conflict,
            "\"Yangon Office\" cannot be deleted because it is used by 3 department(s). Deactivate it instead.");
    }

    // ---- Department ----

    [Fact]
    public async Task Department_branch_must_belong_to_the_selected_company()
    {
        var first = await host.CreateCompanyAsync(code: "A1", name: "First");
        var second = await host.CreateCompanyAsync(code: "B2", name: "Second");
        var branchOfFirst = await host.CreateBranchAsync(first.Id);

        var result = await host.Get<ICreateHandler<DepartmentRequest, DepartmentDto>>()
            .HandleAsync(Requests.Department(second.Id, branchOfFirst.Id), default);

        ResultAssert.FailedOnField(result, "branchId", "The selected branch does not belong to the selected company.");
    }

    [Fact]
    public async Task Department_cannot_be_created_under_an_inactive_branch()
    {
        var company = await host.CreateCompanyAsync();
        var branch = await host.CreateBranchAsync(company.Id, status: "Inactive");

        var result = await host.Get<ICreateHandler<DepartmentRequest, DepartmentDto>>()
            .HandleAsync(Requests.Department(company.Id, branch.Id), default);

        ResultAssert.FailedOnField(result, "branchId", "Branch \"Yangon Office\" is inactive and cannot receive new departments.");
    }

    [Fact]
    public async Task Department_code_must_be_unique_and_the_branch_must_exist()
    {
        var company = await host.CreateCompanyAsync();
        var branch = await host.CreateBranchAsync(company.Id);
        await host.CreateDepartmentAsync(company.Id, branch.Id, "ENG");
        var create = host.Get<ICreateHandler<DepartmentRequest, DepartmentDto>>();

        ResultAssert.FailedOnField(await create.HandleAsync(Requests.Department(company.Id, branch.Id, "ENG"), default), "code");
        ResultAssert.FailedOnField(await create.HandleAsync(Requests.Department(company.Id, 404, "OPS"), default), "branchId");
    }

    [Fact]
    public async Task Department_delete_lists_every_blocking_reference()
    {
        var company = await host.CreateCompanyAsync();
        var branch = await host.CreateBranchAsync(company.Id);
        var department = await host.CreateDepartmentAsync(company.Id, branch.Id);
        host.References.Set(ReferenceQuery.SectionsOfDepartment, department.Id, 1);
        host.References.Set(ReferenceQuery.PositionsOfDepartment, department.Id, 4);
        host.References.Set(ReferenceQuery.EmployeesOfDepartment, department.Id, 9);

        var result = await host.Get<IDeleteHandler<DepartmentDto>>().HandleAsync(department.Id, default);

        ResultAssert.Failed(
            result,
            ErrorType.Conflict,
            "\"Engineering\" cannot be deleted because it is used by 1 section(s), 4 position(s), 9 employee(s). Deactivate it instead.");
    }

    // ---- Section ----

    [Fact]
    public async Task Section_cannot_be_created_under_an_inactive_department()
    {
        var department = await CreateDepartmentAsync(departmentStatus: "Inactive");

        var result = await host.Get<ICreateHandler<SectionRequest, SectionDto>>().HandleAsync(Requests.Section(department.Id), default);

        ResultAssert.FailedOnField(result, "departmentId", "Department \"Engineering\" is inactive and cannot receive new sections.");
    }

    [Fact]
    public async Task Section_code_must_be_unique()
    {
        var department = await CreateDepartmentAsync();
        await host.CreateSectionAsync(department.Id, "BE");

        var result = await host.Get<ICreateHandler<SectionRequest, SectionDto>>().HandleAsync(Requests.Section(department.Id, "BE"), default);

        ResultAssert.FailedOnField(result, "code", "Section code \"BE\" already exists.");
    }

    // ---- Job grade ----

    [Fact]
    public async Task JobGrade_maximum_salary_cannot_be_below_the_minimum()
    {
        var request = Requests.JobGrade(min: 500m, max: 100m);

        var result = await host.Get<ICreateHandler<JobGradeRequest, JobGradeDto>>().HandleAsync(request, default);

        ResultAssert.FailedOnField(result, "maxSalary", "Maximum salary must be greater than or equal to minimum salary.");
    }

    [Fact]
    public async Task JobGrade_salary_amounts_keep_at_most_two_decimals()
    {
        var request = Requests.JobGrade(min: 100.123m, max: 200m);

        ResultAssert.FailedOnField(await host.Get<ICreateHandler<JobGradeRequest, JobGradeDto>>().HandleAsync(request, default), "minSalary");
    }

    [Fact]
    public async Task JobGrade_code_must_be_unique()
    {
        await host.CreateJobGradeAsync("G1");

        var result = await host.Get<ICreateHandler<JobGradeRequest, JobGradeDto>>().HandleAsync(Requests.JobGrade("G1"), default);

        ResultAssert.FailedOnField(result, "code", "Grade code \"G1\" already exists.");
    }

    [Fact]
    public async Task JobGrade_delete_message_uses_the_code_and_name()
    {
        var grade = await host.CreateJobGradeAsync();
        host.References.Set(ReferenceQuery.PositionsOfJobGrade, grade.Id, 2);

        var result = await host.Get<IDeleteHandler<JobGradeDto>>().HandleAsync(grade.Id, default);

        ResultAssert.Failed(
            result,
            ErrorType.Conflict,
            "\"G1 Junior\" cannot be deleted because it is used by 2 position(s). Deactivate it instead.");
    }

    // ---- Position ----

    [Fact]
    public async Task Position_section_must_belong_to_the_selected_department()
    {
        var company = await host.CreateCompanyAsync();
        var branch = await host.CreateBranchAsync(company.Id);
        var engineering = await host.CreateDepartmentAsync(company.Id, branch.Id, "ENG");
        var finance = await host.CreateDepartmentAsync(company.Id, branch.Id, "FIN");
        var section = await host.CreateSectionAsync(finance.Id);
        var grade = await host.CreateJobGradeAsync();

        var result = await host.Get<ICreateHandler<PositionRequest, PositionDto>>()
            .HandleAsync(Requests.Position(engineering.Id, grade.Id, sectionId: section.Id), default);

        ResultAssert.FailedOnField(result, "sectionId", "The selected section does not belong to the selected department.");
    }

    [Fact]
    public async Task Position_cannot_use_an_inactive_job_grade()
    {
        var department = await CreateDepartmentAsync();
        var grade = await host.CreateJobGradeAsync(status: "Inactive");

        var result = await host.Get<ICreateHandler<PositionRequest, PositionDto>>()
            .HandleAsync(Requests.Position(department.Id, grade.Id), default);

        ResultAssert.FailedOnField(result, "jobGradeId", "Job grade \"G1 Junior\" is inactive and cannot receive new positions.");
    }

    [Fact]
    public async Task Position_reports_to_must_exist()
    {
        var department = await CreateDepartmentAsync();
        var grade = await host.CreateJobGradeAsync();

        var result = await host.Get<ICreateHandler<PositionRequest, PositionDto>>()
            .HandleAsync(Requests.Position(department.Id, grade.Id, reportsTo: 55), default);

        ResultAssert.FailedOnField(result, "reportsToPositionId", "Position #55 was not found.");
    }

    [Fact]
    public async Task Position_cannot_report_to_itself()
    {
        var department = await CreateDepartmentAsync();
        var grade = await host.CreateJobGradeAsync();
        var position = await host.CreatePositionAsync(department.Id, grade.Id);

        var result = await host.Get<IUpdateHandler<PositionRequest, PositionDto>>()
            .HandleAsync(position.Id, Requests.Position(department.Id, grade.Id, reportsTo: position.Id), default);

        ResultAssert.FailedOnField(result, "reportsToPositionId", "A position cannot report to itself.");
    }

    [Fact]
    public async Task Position_can_report_to_an_existing_position_in_the_same_department()
    {
        var department = await CreateDepartmentAsync();
        var grade = await host.CreateJobGradeAsync();
        var manager = await host.CreatePositionAsync(department.Id, grade.Id, "MGR");

        var report = await host.CreatePositionAsync(department.Id, grade.Id, "DEV", reportsTo: manager.Id);

        Assert.Equal(manager.Id, report.ReportsToPositionId);
    }

    [Fact]
    public async Task Position_delete_is_blocked_by_employees_and_reporting_positions()
    {
        var department = await CreateDepartmentAsync();
        var grade = await host.CreateJobGradeAsync();
        var position = await host.CreatePositionAsync(department.Id, grade.Id);
        host.References.Set(ReferenceQuery.EmployeesOfPosition, position.Id, 1);
        host.References.Set(ReferenceQuery.PositionsReportingToPosition, position.Id, 2);

        var result = await host.Get<IDeleteHandler<PositionDto>>().HandleAsync(position.Id, default);

        ResultAssert.Failed(
            result,
            ErrorType.Conflict,
            "\"Developer\" cannot be deleted because it is used by 1 employee(s), 2 position(s) reporting to it. Deactivate it instead.");
    }

    private async Task<DepartmentDto> CreateDepartmentAsync(string departmentStatus = "Active")
    {
        var company = await host.CreateCompanyAsync();
        var branch = await host.CreateBranchAsync(company.Id);
        return await host.CreateDepartmentAsync(company.Id, branch.Id, status: departmentStatus);
    }

    private async Task DeactivateCompanyAsync(Apex.Hrms.Application.Organization.Companies.CompanyDto company)
    {
        var update = host.Get<IUpdateHandler<Apex.Hrms.Application.Organization.Companies.CompanyRequest, Apex.Hrms.Application.Organization.Companies.CompanyDto>>();
        ResultAssert.Succeeded(await update.HandleAsync(company.Id, Requests.Company(status: "Inactive"), default));
    }
}
