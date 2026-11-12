using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Organization.JobGrades;
using Apex.Hrms.Application.Payroll.Assignments;
using Apex.Hrms.Application.Payroll.Components;
using Apex.Hrms.Application.Payroll.Payslips;
using Apex.Hrms.Application.Payroll.Runs;
using Apex.Hrms.Application.Payroll.Structures;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Attendance;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Leave;
using Apex.Hrms.Domain.Payroll;

namespace Apex.Hrms.Application.Tests.Fakes;

/// <summary>Counts the component links of the structures in the fake repository (the table has no repository of its own).</summary>
internal sealed class StructureComponentCounter(InMemoryRepository<SalaryStructure> structures) : IRecordCounter<SalaryStructureComponent>
{
    public Task<int> CountAsync(System.Linq.Expressions.Expression<Func<SalaryStructureComponent, bool>> predicate, CancellationToken cancellationToken) =>
        Task.FromResult(structures.Items.SelectMany(structure => structure.Components).Count(predicate.Compile()));
}

/// <summary>
/// The fake of <see cref="IPayrollRunOutput"/>: saves through the fake unit of work, then removes the payslips of the run, and
/// notes in <see cref="Steps"/> what it did and which status the run had at each step (the database accepts the delete only once
/// the run is Draft, so the run must be saved as Draft first).
/// </summary>
internal sealed class FakePayrollRunOutput(
    IUnitOfWork unitOfWork,
    InMemoryRepository<Payslip> payslips,
    InMemoryRepository<PayrollRun> runs) : IPayrollRunOutput
{
    public List<string> Steps { get; } = [];

    public async Task<Result> SaveAndClearAsync(int runId, CancellationToken cancellationToken)
    {
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        Steps.Add("save");
        if (saved.IsFailure)
        {
            return saved;
        }

        var status = PayrollRunStatusText.ToText(runs.Items.Single(run => run.Id == runId).Status);
        foreach (var payslip in payslips.Items.Where(payslip => payslip.PayrollRunId == runId).ToList())
        {
            payslips.Remove(payslip);
        }

        Steps.Add($"delete payslips (run is {status})");
        Steps.Add($"delete warnings (run is {status})");
        return Result.Success();
    }
}

/// <summary>What the payroll tests start from: fictional people, a structure and a salary, all in November 2026.</summary>
internal sealed record PayrollSeed(
    OrgSeed Org,
    SalaryComponentDto Housing,
    SalaryComponentDto Transport,
    SalaryComponentDto Tax,
    SalaryComponentDto Social,
    SalaryStructureDto Structure,
    EmployeeDto Employee,
    SalaryAssignmentDto Assignment);

/// <summary>Fictional payroll data and the handler calls the payroll tests share. The fake clock says today is Monday 2026-11-09.</summary>
internal static class PayrollFixtures
{
    public static readonly DateOnly November = new(2026, 11, 1);

    public static SalaryComponentCreateRequest NewComponent(
        string code = "HOUSING",
        string name = "Housing",
        string type = "Earning",
        string calculation = "PercentOfBasic",
        decimal value = 10m,
        string status = "Active") => new(code, name, type, calculation, value, Description: null, status);

    public static SalaryComponentUpdateRequest ComponentUpdateOf(SalaryComponentDto current, decimal? value = null) => new(
        current.Code,
        current.Name,
        current.Type,
        current.Calculation,
        value ?? current.Value,
        current.Description,
        Requests.Version(current.RowVersion));

    public static SalaryStructureCreateRequest NewStructure(string code = "STAFF", string name = "Staff", params int[] componentIds) =>
        new(code, name, Description: null, componentIds, "Active");

    public static SalaryStructureUpdateRequest StructureUpdateOf(SalaryStructureDto current, params int[] componentIds) => new(
        current.Code,
        current.Name,
        current.Description,
        componentIds,
        Requests.Version(current.RowVersion));

    public static SalaryAssignmentCreateRequest NewAssignment(
        int employeeId,
        int structureId,
        decimal basic = 1000m,
        DateOnly? from = null,
        string? remarks = null) => new(employeeId, structureId, basic, from ?? new DateOnly(2020, 1, 1), remarks);

    public static SalaryAssignmentUpdateRequest AssignmentUpdateOf(SalaryAssignmentDto current, decimal? basic = null, DateOnly? from = null, int? structureId = null) => new(
        current.EmployeeId,
        structureId ?? current.StructureId,
        basic ?? current.BasicSalary,
        from ?? current.EffectiveFrom,
        current.Remarks,
        Requests.Version(current.RowVersion));

    public static Task<Result<SalaryComponentDto>> CreateComponent(this TestHost host, SalaryComponentCreateRequest request) =>
        host.Get<ICreateHandler<SalaryComponentCreateRequest, SalaryComponentDto>>().HandleAsync(request, default);

    public static async Task<SalaryComponentDto> CreateComponentAsync(this TestHost host, SalaryComponentCreateRequest request) =>
        ResultAssert.Succeeded(await host.CreateComponent(request));

    public static Task<Result<SalaryStructureDto>> CreateStructure(this TestHost host, SalaryStructureCreateRequest request) =>
        host.Get<ICreateHandler<SalaryStructureCreateRequest, SalaryStructureDto>>().HandleAsync(request, default);

    public static async Task<SalaryStructureDto> CreateStructureAsync(this TestHost host, SalaryStructureCreateRequest request) =>
        ResultAssert.Succeeded(await host.CreateStructure(request));

    public static Task<Result<SalaryAssignmentDto>> CreateAssignment(this TestHost host, SalaryAssignmentCreateRequest request) =>
        host.Get<ICreateHandler<SalaryAssignmentCreateRequest, SalaryAssignmentDto>>().HandleAsync(request, default);

    public static async Task<SalaryAssignmentDto> CreateAssignmentAsync(this TestHost host, SalaryAssignmentCreateRequest request) =>
        ResultAssert.Succeeded(await host.CreateAssignment(request));

    public static Task<Result<PayrollRunDto>> CreateRun(this TestHost host, int year = 2026, int month = 11, DateOnly? payDate = null) =>
        host.Get<ICreateHandler<PayrollRunCreateRequest, PayrollRunDto>>().HandleAsync(new PayrollRunCreateRequest(year, month, payDate), default);

    public static async Task<PayrollRunDto> CreateRunAsync(this TestHost host, int year = 2026, int month = 11, DateOnly? payDate = null) =>
        ResultAssert.Succeeded(await host.CreateRun(year, month, payDate));

    public static Task<Result<PayrollRunDto>> GetRun(this TestHost host, int id) =>
        host.Get<IGetByIdHandler<PayrollRunDto>>().HandleAsync(id, default);

    public static Task<Result<PayrollRunDto>> Process(this TestHost host, PayrollRunDto run) =>
        host.Command<ProcessPayrollRunCommand, PayrollRunDto>(new ProcessPayrollRunCommand(run.Id, Requests.Version(run.RowVersion)));

    public static Task<Result<PayrollRunDto>> Approve(this TestHost host, PayrollRunDto run) =>
        host.Command<ApprovePayrollRunCommand, PayrollRunDto>(new ApprovePayrollRunCommand(run.Id, Requests.Version(run.RowVersion)));

    public static Task<Result<PayrollRunDto>> MarkPaid(this TestHost host, PayrollRunDto run) =>
        host.Command<MarkPayrollRunPaidCommand, PayrollRunDto>(new MarkPayrollRunPaidCommand(run.Id, Requests.Version(run.RowVersion)));

    public static Task<Result<PayrollRunDto>> ReturnToDraft(this TestHost host, PayrollRunDto run, string? reason = null) =>
        host.Command<ReturnPayrollRunToDraftCommand, PayrollRunDto>(new ReturnPayrollRunToDraftCommand(run.Id, Requests.Version(run.RowVersion), reason));

    public static async Task<PayrollRunDto> ProcessedAsync(this TestHost host, PayrollRunDto run) =>
        ResultAssert.Succeeded(await host.Process(run));

    public static Task<Result<PagedResult<PayslipDto>>> Payslips(this TestHost host, PayslipFilter? filter = null) =>
        host.Get<IQueryHandler<PayslipDto, PayslipFilter>>()
            .HandleAsync(new PageRequest<PayslipFilter>(0, 100, null, null, filter ?? new PayslipFilter()), default);

    /// <summary>An organization whose job grade band is 500 to 5000 (the default band of the tests, 100 to 200, is too low for a salary).</summary>
    public static async Task<OrgSeed> SeedPayrollOrgAsync(this TestHost host)
    {
        var company = await host.CreateCompanyAsync();
        var branch = await host.CreateBranchAsync(company.Id);
        var department = await host.CreateDepartmentAsync(company.Id, branch.Id);
        var grade = ResultAssert.Succeeded(await host.Get<ICreateHandler<JobGradeCreateRequest, JobGradeDto>>()
            .HandleAsync(Requests.JobGrade("PG1", "Active", min: 500m, max: 5000m), default));
        var position = await host.CreatePositionAsync(department.Id, grade.Id);
        return new OrgSeed(company, branch, department, position, grade);
    }

    /// <summary>An employee who joined on <paramref name="joined"/> with a bank account (the account number is fictional).</summary>
    public static async Task<EmployeeDto> CreatePayrollEmployeeAsync(
        this TestHost host,
        OrgSeed org,
        string email,
        DateOnly? joined = null,
        string accountNumber = "1234567890",
        bool withBank = true)
    {
        var employee = await host.CreateEmployeeAsync(EmployeeFixtures.NewEmployee(org, email: email) with { JoiningDate = joined ?? new DateOnly(2020, 1, 1) });
        if (withBank)
        {
            ResultAssert.Succeeded(await host.Get<IUpdateHandler<BankAccountUpdateRequest, BankAccountDto>>()
                .HandleAsync(employee.Id, EmployeeFixtures.Bank(accountNumber), default));
        }

        return employee;
    }

    /// <summary>
    /// Housing (10% of basic), Transport (fixed 50), Tax (5% of gross) and Social (2% of gross), one structure with all four, one employee on
    /// a basic of 1000 from 2020-01-01 with a bank account ending 7890.
    /// </summary>
    public static async Task<PayrollSeed> SeedPayrollAsync(this TestHost host)
    {
        var org = await host.SeedPayrollOrgAsync();
        var housing = await host.CreateComponentAsync(NewComponent());
        var transport = await host.CreateComponentAsync(NewComponent("TRANSPORT", "Transport", "Earning", "Fixed", 50m));
        var tax = await host.CreateComponentAsync(NewComponent("TAX", "Tax", "Deduction", "PercentOfGross", 5m));
        var social = await host.CreateComponentAsync(NewComponent("SOCIAL", "Social", "Deduction", "PercentOfGross", 2m));
        var structure = await host.CreateStructureAsync(NewStructure("STAFF", "Staff", housing.Id, transport.Id, tax.Id, social.Id));
        var employee = await host.CreatePayrollEmployeeAsync(org, "ada.test@example.test");
        var assignment = await host.CreateAssignmentAsync(NewAssignment(employee.Id, structure.Id));
        return new PayrollSeed(org, housing, transport, tax, social, structure, employee, assignment);
    }

    /// <summary>Adds an approved overtime entry. The domain methods build it (the class has no public constructor); an employee never decides their own entry.</summary>
    public static void AddApprovedOvertime(this TestHost host, int employeeId, DateOnly date, TimeOnly start, TimeOnly end)
    {
        var overtime = ResultAssert.Succeeded(OvertimeRecord.Create(new OvertimeDetails(employeeId, date, start, end, "Month end close"), date));
        Assert.True(Decided(overtime).IsSuccess);
        host.Repository<OvertimeRecord>().Add(overtime);
    }

    /// <summary>Adds a leave application of the given type with the given status (built with the domain methods).</summary>
    public static void AddLeave(this TestHost host, int employeeId, int leaveTypeId, DateOnly start, DateOnly end, bool halfDay = false, bool approved = true)
    {
        var details = new LeaveApplicationDetails(employeeId, leaveTypeId, start, end, halfDay, "Family matter", null);
        var days = LeaveApplication.DaysFor(details, WorkingDays.Count(start, end, new HashSet<DayOfWeek> { DayOfWeek.Saturday, DayOfWeek.Sunday }, new HashSet<DateOnly>()));
        var application = ResultAssert.Succeeded(LeaveApplication.Create(details, days, $"LV-TST-{employeeId}-{start:MMdd}", start));
        Assert.True(application.Submit(start).IsSuccess);
        if (approved)
        {
            Assert.True(application.Decide(LeaveDecision.Approve, new LeaveActor(99, null, "Approver"), null, start).IsSuccess);
        }

        host.Repository<LeaveApplication>().Add(application);
    }

    private static Result Decided(OvertimeRecord overtime) =>
        overtime.Decide(ApprovalDecision.Approve, new ApprovalActor(99, null, "Approver"), null, overtime.Date);
}
