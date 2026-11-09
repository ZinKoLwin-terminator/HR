using Apex.Hrms.Application.Organization.Branches;
using Apex.Hrms.Application.Organization.Companies;
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
        var result = await host.Get<ICreateHandler<BranchCreateRequest, BranchDto>>().HandleAsync(Requests.Branch(99), default);

        ResultAssert.FailedOnField(result, "companyId", "Company #99 was not found.");
    }

    [Fact]
    public async Task Branch_cannot_be_created_under_an_inactive_company()
    {
        var company = await host.CreateCompanyAsync(status: "Inactive");

        var result = await host.Get<ICreateHandler<BranchCreateRequest, BranchDto>>().HandleAsync(Requests.Branch(company.Id), default);

        ResultAssert.FailedOnField(result, "companyId", "Company \"Apex Holdings\" is inactive and cannot receive new branches.");
    }

    [Fact]
    public async Task Branch_code_must_be_unique()
    {
        var company = await host.CreateCompanyAsync();
        await host.CreateBranchAsync(company.Id, "YGN");

        var result = await host.Get<ICreateHandler<BranchCreateRequest, BranchDto>>().HandleAsync(Requests.Branch(company.Id, "YGN"), default);

        ResultAssert.FailedOnField(result, "code", "Branch code \"YGN\" already exists.");
    }

    [Fact]
    public async Task Branch_opened_date_cannot_be_in_the_future()
    {
        var company = await host.CreateCompanyAsync();
        var request = Requests.Branch(company.Id, opened: host.Clock.Today.AddDays(1));

        ResultAssert.FailedOnField(await host.Get<ICreateHandler<BranchCreateRequest, BranchDto>>().HandleAsync(request, default), "openedDate");
    }

    [Fact]
    public async Task Branch_type_must_be_one_of_the_known_texts()
    {
        var company = await host.CreateCompanyAsync();
        var request = Requests.Branch(company.Id) with { Type = "Outpost" };

        ResultAssert.FailedOnField(await host.Get<ICreateHandler<BranchCreateRequest, BranchDto>>().HandleAsync(request, default), "type");
    }

    [Fact]
    public async Task Branch_can_be_updated_while_its_company_is_inactive_as_long_as_the_company_is_unchanged()
    {
        var company = await host.CreateCompanyAsync();
        var branch = await host.CreateBranchAsync(company.Id);
        await DeactivateCompanyAsync(company);

        var result = await host.Get<IUpdateHandler<BranchUpdateRequest, BranchDto>>()
            .HandleAsync(branch.Id, Requests.BranchUpdate(branch.RowVersion, company.Id) with { Type = "Warehouse" }, default);

        Assert.Equal("Warehouse", ResultAssert.Succeeded(result).Type);
    }

    [Fact]
    public async Task Branch_delete_is_blocked_by_departments_and_employees()
    {
        var company = await host.CreateCompanyAsync();
        var branch = await host.CreateBranchAsync(company.Id);
        await host.CreateDepartmentAsync(company.Id, branch.Id);
        host.Employees.Add(companyId: company.Id, branchId: branch.Id);
        host.Employees.Add(companyId: company.Id, branchId: branch.Id);

        var result = await host.Get<IDeleteHandler<BranchDto>>().HandleAsync(branch.Id, Requests.Version(branch.RowVersion), default);

        ResultAssert.Failed(
            result,
            ErrorType.Conflict,
            "\"Yangon Office\" cannot be deleted because it is used by 1 department(s), 2 employee(s). Deactivate it instead.");
    }

    [Fact]
    public async Task Branch_without_children_can_move_to_another_active_company()
    {
        var first = await host.CreateCompanyAsync(code: "A1", name: "First");
        var second = await host.CreateCompanyAsync(code: "B2", name: "Second");
        var branch = await host.CreateBranchAsync(first.Id);

        var result = await host.Get<IUpdateHandler<BranchUpdateRequest, BranchDto>>()
            .HandleAsync(branch.Id, Requests.BranchUpdate(branch.RowVersion, second.Id), default);

        Assert.Equal(second.Id, ResultAssert.Succeeded(result).CompanyId);
    }

    [Fact]
    public async Task Branch_with_departments_or_employees_cannot_move_to_another_company()
    {
        var first = await host.CreateCompanyAsync(code: "A1", name: "First");
        var second = await host.CreateCompanyAsync(code: "B2", name: "Second");
        var branch = await host.CreateBranchAsync(first.Id);
        await host.CreateDepartmentAsync(first.Id, branch.Id);
        host.Employees.Add(companyId: first.Id, branchId: branch.Id);

        var result = await host.Get<IUpdateHandler<BranchUpdateRequest, BranchDto>>()
            .HandleAsync(branch.Id, Requests.BranchUpdate(branch.RowVersion, second.Id), default);

        ResultAssert.FailedOnField(
            result,
            "companyId",
            "Branch \"Yangon Office\" cannot move to another company while it has 1 department(s), 1 employee(s).");
        Assert.Equal(first.Id, host.Repository<Apex.Hrms.Domain.Organization.Branch>().Items.Single().CompanyId);
    }

    // ---- Department ----

    [Fact]
    public async Task Department_branch_must_belong_to_the_selected_company()
    {
        var first = await host.CreateCompanyAsync(code: "A1", name: "First");
        var second = await host.CreateCompanyAsync(code: "B2", name: "Second");
        var branchOfFirst = await host.CreateBranchAsync(first.Id);

        var result = await host.Get<ICreateHandler<DepartmentCreateRequest, DepartmentDto>>()
            .HandleAsync(Requests.Department(second.Id, branchOfFirst.Id), default);

        ResultAssert.FailedOnField(result, "branchId", "The selected branch does not belong to the selected company.");
    }

    [Fact]
    public async Task Department_cannot_be_created_under_an_inactive_branch()
    {
        var company = await host.CreateCompanyAsync();
        var branch = await host.CreateBranchAsync(company.Id, status: "Inactive");

        var result = await host.Get<ICreateHandler<DepartmentCreateRequest, DepartmentDto>>()
            .HandleAsync(Requests.Department(company.Id, branch.Id), default);

        ResultAssert.FailedOnField(result, "branchId", "Branch \"Yangon Office\" is inactive and cannot receive new departments.");
    }

    [Fact]
    public async Task Department_code_must_be_unique_and_the_branch_must_exist()
    {
        var company = await host.CreateCompanyAsync();
        var branch = await host.CreateBranchAsync(company.Id);
        await host.CreateDepartmentAsync(company.Id, branch.Id, "ENG");
        var create = host.Get<ICreateHandler<DepartmentCreateRequest, DepartmentDto>>();

        ResultAssert.FailedOnField(await create.HandleAsync(Requests.Department(company.Id, branch.Id, "ENG"), default), "code");
        ResultAssert.FailedOnField(await create.HandleAsync(Requests.Department(company.Id, 404, "OPS"), default), "branchId");
    }

    [Fact]
    public async Task Department_delete_lists_every_blocking_reference()
    {
        var company = await host.CreateCompanyAsync();
        var branch = await host.CreateBranchAsync(company.Id);
        var department = await host.CreateDepartmentAsync(company.Id, branch.Id);
        await host.CreateSectionAsync(department.Id);
        var grade = await host.CreateJobGradeAsync();
        await host.CreatePositionAsync(department.Id, grade.Id, "P1");
        await host.CreatePositionAsync(department.Id, grade.Id, "P2");
        host.Employees.Add(departmentId: department.Id);

        var result = await host.Get<IDeleteHandler<DepartmentDto>>().HandleAsync(department.Id, Requests.Version(department.RowVersion), default);

        ResultAssert.Failed(
            result,
            ErrorType.Conflict,
            "\"Engineering\" cannot be deleted because it is used by 1 section(s), 2 position(s), 1 employee(s). Deactivate it instead.");
    }

    [Fact]
    public async Task Department_without_children_can_move_to_another_branch_of_the_same_company()
    {
        var company = await host.CreateCompanyAsync();
        var first = await host.CreateBranchAsync(company.Id, "YGN");
        var second = await host.CreateBranchAsync(company.Id, "MDY");
        var department = await host.CreateDepartmentAsync(company.Id, first.Id);

        var result = await host.Get<IUpdateHandler<DepartmentUpdateRequest, DepartmentDto>>()
            .HandleAsync(department.Id, Requests.DepartmentUpdate(department.RowVersion, company.Id, second.Id), default);

        Assert.Equal(second.Id, ResultAssert.Succeeded(result).BranchId);
    }

    [Fact]
    public async Task Department_with_sections_positions_or_employees_cannot_move_to_another_branch()
    {
        var company = await host.CreateCompanyAsync();
        var first = await host.CreateBranchAsync(company.Id, "YGN");
        var second = await host.CreateBranchAsync(company.Id, "MDY");
        var department = await host.CreateDepartmentAsync(company.Id, first.Id);
        await host.CreateSectionAsync(department.Id);
        var grade = await host.CreateJobGradeAsync();
        await host.CreatePositionAsync(department.Id, grade.Id);
        host.Employees.Add(departmentId: department.Id);
        host.Employees.Add(departmentId: department.Id);
        host.Employees.Add(departmentId: department.Id);

        var result = await host.Get<IUpdateHandler<DepartmentUpdateRequest, DepartmentDto>>()
            .HandleAsync(department.Id, Requests.DepartmentUpdate(department.RowVersion, company.Id, second.Id), default);

        ResultAssert.FailedOnField(
            result,
            "branchId",
            "Department \"Engineering\" cannot move to another branch while it has 1 section(s), 1 position(s), 3 employee(s).");
    }

    // ---- Section ----

    [Fact]
    public async Task Section_cannot_be_created_under_an_inactive_department()
    {
        var department = await CreateDepartmentAsync(departmentStatus: "Inactive");

        var result = await host.Get<ICreateHandler<SectionCreateRequest, SectionDto>>().HandleAsync(Requests.Section(department.Id), default);

        ResultAssert.FailedOnField(result, "departmentId", "Department \"Engineering\" is inactive and cannot receive new sections.");
    }

    [Fact]
    public async Task Section_code_must_be_unique()
    {
        var department = await CreateDepartmentAsync();
        await host.CreateSectionAsync(department.Id, "BE");

        var result = await host.Get<ICreateHandler<SectionCreateRequest, SectionDto>>().HandleAsync(Requests.Section(department.Id, "BE"), default);

        ResultAssert.FailedOnField(result, "code", "Section code \"BE\" already exists.");
    }

    [Fact]
    public async Task Section_with_positions_or_employees_cannot_move_to_another_department_but_an_empty_one_can()
    {
        var company = await host.CreateCompanyAsync();
        var branch = await host.CreateBranchAsync(company.Id);
        var engineering = await host.CreateDepartmentAsync(company.Id, branch.Id, "ENG");
        var finance = await host.CreateDepartmentAsync(company.Id, branch.Id, "FIN");
        var busy = await host.CreateSectionAsync(engineering.Id, "BE");
        var empty = await host.CreateSectionAsync(engineering.Id, "FE");
        var grade = await host.CreateJobGradeAsync();
        await host.CreatePositionAsync(engineering.Id, grade.Id, sectionId: busy.Id);
        host.Employees.Add(departmentId: engineering.Id, sectionId: busy.Id);
        var update = host.Get<IUpdateHandler<SectionUpdateRequest, SectionDto>>();

        var blocked = await update.HandleAsync(busy.Id, Requests.SectionUpdate(busy.RowVersion, finance.Id), default);
        var moved = await update.HandleAsync(empty.Id, Requests.SectionUpdate(empty.RowVersion, finance.Id, "FE"), default);

        ResultAssert.FailedOnField(
            blocked,
            "departmentId",
            "Section \"Backend\" cannot move to another department while it has 1 position(s), 1 employee(s).");
        Assert.Equal(finance.Id, ResultAssert.Succeeded(moved).DepartmentId);
    }

    // ---- Job grade ----

    [Fact]
    public async Task JobGrade_maximum_salary_cannot_be_below_the_minimum()
    {
        var request = Requests.JobGrade(min: 500m, max: 100m);

        var result = await host.Get<ICreateHandler<JobGradeCreateRequest, JobGradeDto>>().HandleAsync(request, default);

        ResultAssert.FailedOnField(result, "maxSalary", "Maximum salary must be greater than or equal to minimum salary.");
    }

    [Fact]
    public async Task JobGrade_salary_amounts_keep_at_most_two_decimals()
    {
        var request = Requests.JobGrade(min: 100.123m, max: 200m);

        ResultAssert.FailedOnField(await host.Get<ICreateHandler<JobGradeCreateRequest, JobGradeDto>>().HandleAsync(request, default), "minSalary");
    }

    [Fact]
    public async Task JobGrade_code_must_be_unique()
    {
        await host.CreateJobGradeAsync("G1");

        var result = await host.Get<ICreateHandler<JobGradeCreateRequest, JobGradeDto>>().HandleAsync(Requests.JobGrade("G1"), default);

        ResultAssert.FailedOnField(result, "code", "Grade code \"G1\" already exists.");
    }

    [Fact]
    public async Task JobGrade_delete_message_uses_the_code_and_name()
    {
        var department = await CreateDepartmentAsync();
        var grade = await host.CreateJobGradeAsync();
        await host.CreatePositionAsync(department.Id, grade.Id);
        await host.CreatePositionAsync(department.Id, grade.Id, "QA");

        var result = await host.Get<IDeleteHandler<JobGradeDto>>().HandleAsync(grade.Id, Requests.Version(grade.RowVersion), default);

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

        var result = await host.Get<ICreateHandler<PositionCreateRequest, PositionDto>>()
            .HandleAsync(Requests.Position(engineering.Id, grade.Id, sectionId: section.Id), default);

        ResultAssert.FailedOnField(result, "sectionId", "The selected section does not belong to the selected department.");
    }

    [Fact]
    public async Task Position_cannot_use_an_inactive_job_grade()
    {
        var department = await CreateDepartmentAsync();
        var grade = await host.CreateJobGradeAsync(status: "Inactive");

        var result = await host.Get<ICreateHandler<PositionCreateRequest, PositionDto>>()
            .HandleAsync(Requests.Position(department.Id, grade.Id), default);

        ResultAssert.FailedOnField(result, "jobGradeId", "Job grade \"G1 Junior\" is inactive and cannot receive new positions.");
    }

    [Fact]
    public async Task Position_reports_to_must_exist()
    {
        var department = await CreateDepartmentAsync();
        var grade = await host.CreateJobGradeAsync();

        var result = await host.Get<ICreateHandler<PositionCreateRequest, PositionDto>>()
            .HandleAsync(Requests.Position(department.Id, grade.Id, reportsTo: 55), default);

        ResultAssert.FailedOnField(result, "reportsToPositionId", "Position #55 was not found.");
    }

    [Fact]
    public async Task Position_cannot_report_to_itself()
    {
        var department = await CreateDepartmentAsync();
        var grade = await host.CreateJobGradeAsync();
        var position = await host.CreatePositionAsync(department.Id, grade.Id);

        var result = await UpdatePositionAsync(position, department.Id, grade.Id, reportsTo: position.Id);

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
    public async Task Position_cannot_report_to_someone_who_reports_to_it_directly_or_indirectly()
    {
        var department = await CreateDepartmentAsync();
        var grade = await host.CreateJobGradeAsync();
        var top = await host.CreatePositionAsync(department.Id, grade.Id, "TOP");
        var middle = await host.CreatePositionAsync(department.Id, grade.Id, "MID", reportsTo: top.Id);
        var bottom = await host.CreatePositionAsync(department.Id, grade.Id, "LOW", reportsTo: middle.Id);

        var direct = await UpdatePositionAsync(top, department.Id, grade.Id, "TOP", reportsTo: middle.Id);
        var indirect = await UpdatePositionAsync(top, department.Id, grade.Id, "TOP", reportsTo: bottom.Id);

        ResultAssert.FailedOnField(
            direct,
            "reportsToPositionId",
            "Position \"Developer\" cannot report to \"Developer\" because that would create a reporting loop.");
        ResultAssert.FailedOnField(indirect, "reportsToPositionId");
    }

    [Fact]
    public async Task Position_can_be_moved_under_an_unrelated_manager_in_the_chain()
    {
        var department = await CreateDepartmentAsync();
        var grade = await host.CreateJobGradeAsync();
        var top = await host.CreatePositionAsync(department.Id, grade.Id, "TOP");
        var other = await host.CreatePositionAsync(department.Id, grade.Id, "OTH");
        var middle = await host.CreatePositionAsync(department.Id, grade.Id, "MID", reportsTo: top.Id);

        var result = await UpdatePositionAsync(middle, department.Id, grade.Id, "MID", reportsTo: other.Id);

        Assert.Equal(other.Id, ResultAssert.Succeeded(result).ReportsToPositionId);
    }

    [Fact]
    public async Task Position_reporting_chain_walk_is_bounded()
    {
        var department = await CreateDepartmentAsync();
        var grade = await host.CreateJobGradeAsync();
        var first = await host.CreatePositionAsync(department.Id, grade.Id, "P0");
        var last = first;
        for (var level = 1; level <= ReportingLimits.MaxDepth + 2; level++)
        {
            last = await host.CreatePositionAsync(department.Id, grade.Id, $"P{level}", reportsTo: last.Id);
        }

        var result = await UpdatePositionAsync(first, department.Id, grade.Id, "P0", reportsTo: last.Id);

        ResultAssert.FailedOnField(result, "reportsToPositionId");
    }

    [Fact]
    public async Task Position_with_employees_cannot_move_to_another_department_but_one_without_can()
    {
        var company = await host.CreateCompanyAsync();
        var branch = await host.CreateBranchAsync(company.Id);
        var engineering = await host.CreateDepartmentAsync(company.Id, branch.Id, "ENG");
        var finance = await host.CreateDepartmentAsync(company.Id, branch.Id, "FIN");
        var grade = await host.CreateJobGradeAsync();
        var staffed = await host.CreatePositionAsync(engineering.Id, grade.Id, "DEV");
        var vacant = await host.CreatePositionAsync(engineering.Id, grade.Id, "QA");
        host.Employees.Add(positionId: staffed.Id);

        var blocked = await UpdatePositionAsync(staffed, finance.Id, grade.Id, "DEV");
        var moved = await UpdatePositionAsync(vacant, finance.Id, grade.Id, "QA");

        ResultAssert.FailedOnField(
            blocked,
            "departmentId",
            "Position \"Developer\" cannot move to another department while it has 1 employee(s).");
        Assert.Equal(finance.Id, ResultAssert.Succeeded(moved).DepartmentId);
    }

    [Fact]
    public async Task Position_delete_is_blocked_by_employees_and_reporting_positions()
    {
        var department = await CreateDepartmentAsync();
        var grade = await host.CreateJobGradeAsync();
        var position = await host.CreatePositionAsync(department.Id, grade.Id);
        await host.CreatePositionAsync(department.Id, grade.Id, "QA", reportsTo: position.Id);
        await host.CreatePositionAsync(department.Id, grade.Id, "OPS", reportsTo: position.Id);
        host.Employees.Add(positionId: position.Id);

        var result = await host.Get<IDeleteHandler<PositionDto>>().HandleAsync(position.Id, Requests.Version(position.RowVersion), default);

        ResultAssert.Failed(
            result,
            ErrorType.Conflict,
            "\"Developer\" cannot be deleted because it is used by 2 position(s) reporting to it, 1 employee(s). Deactivate it instead.");
    }

    private Task<Result<PositionDto>> UpdatePositionAsync(
        PositionDto position,
        int departmentId,
        int jobGradeId,
        string code = "DEV",
        int? reportsTo = null) =>
        host.Get<IUpdateHandler<PositionUpdateRequest, PositionDto>>().HandleAsync(
            position.Id,
            Requests.PositionUpdate(position.RowVersion, departmentId, jobGradeId, code, reportsTo: reportsTo),
            default);

    private async Task<DepartmentDto> CreateDepartmentAsync(string departmentStatus = "Active")
    {
        var company = await host.CreateCompanyAsync();
        var branch = await host.CreateBranchAsync(company.Id);
        return await host.CreateDepartmentAsync(company.Id, branch.Id, status: departmentStatus);
    }

    private async Task DeactivateCompanyAsync(CompanyDto company) =>
        ResultAssert.Succeeded(await host.Get<ICommandHandler<DeactivateCommand, CompanyDto>>()
            .HandleAsync(new DeactivateCommand(company.Id, Requests.Version(company.RowVersion)), default));
}
