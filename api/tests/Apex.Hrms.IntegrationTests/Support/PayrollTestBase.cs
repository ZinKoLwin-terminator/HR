using Apex.Hrms.Application;
using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Payroll.Assignments;
using Apex.Hrms.Application.Payroll.Components;
using Apex.Hrms.Application.Payroll.Payslips;
using Apex.Hrms.Application.Payroll.Runs;
using Apex.Hrms.Application.Payroll.Structures;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.IntegrationTests.Support;

/// <summary>What a payroll test starts from: a fictional employee on a basic of 1000 with the four sample components in one structure.</summary>
public sealed record PayrollWorld(
    EmployeeDto Employee,
    SalaryStructureDto Structure,
    SalaryAssignmentDto Assignment,
    int DepartmentId);

/// <summary>
/// Base of the payroll tests against the real database. Every payroll run of a test lives in the reserved year
/// <see cref="TestRows.PayrollTestYear"/> (see <see cref="TestRows"/>), long before any real period, so only the test's own
/// employees (who joined before it) are employed in it. The figures are fictional DEMONSTRATION figures.
/// </summary>
public abstract class PayrollTestBase : DatabaseTest
{
    /// <summary>The fictional account number the test employee is paid to. It must never appear on a payslip.</summary>
    protected const string AccountNumber = "1234567890";

    protected const int Year = TestRows.PayrollTestYear;

    protected static DateOnly Joined { get; } = new(1998, 1, 1);

    /// <summary>
    /// Housing 10% of basic, Transport fixed 50, Tax 5% of gross and Social 2% of gross, one structure with all four, and one employee
    /// (joined 1998-01-01, bank account ending 7890) on a basic of 1000 from that day. The job grade band is 500 to 5000.
    /// </summary>
    protected async Task<PayrollWorld> SeedPayrollAsync(string local = "ada", string suffix = "1", DateOnly? joined = null, decimal basic = 1000m)
    {
        var company = await SeedCompanyAsync();
        var branch = await SeedBranchAsync(company.Id);
        var department = await SeedDepartmentAsync(company.Id, branch.Id);
        var grade = Succeeded(await Create(Requests.JobGrade("PG", "Pay Grade", 500m, 5000m)));
        var position = await SeedPositionAsync(department.Id, grade.Id);

        var housing = await CreateComponentAsync("HOUS", "Housing", "Earning", "PercentOfBasic", 10m);
        var transport = await CreateComponentAsync("TRAN", "Transport", "Earning", "Fixed", 50m);
        var tax = await CreateComponentAsync("TAX", "Tax", "Deduction", "PercentOfGross", 5m);
        var social = await CreateComponentAsync("SOC", "Social", "Deduction", "PercentOfGross", 2m);
        var structure = Succeeded(await CreateAsync<SalaryStructureCreateRequest, SalaryStructureDto>(
            new SalaryStructureCreateRequest(Code("STAFF"), Name("Staff"), null, [housing.Id, transport.Id, tax.Id, social.Id], "Active")));

        var employee = await SeedPayrollEmployeeAsync(company.Id, branch.Id, department.Id, position.Id, grade.Id, suffix, local, joined ?? Joined);
        var assignment = Succeeded(await CreateAsync<SalaryAssignmentCreateRequest, SalaryAssignmentDto>(
            new SalaryAssignmentCreateRequest(employee.Id, structure.Id, basic, joined ?? Joined, null)));
        return new PayrollWorld(employee, structure, assignment, department.Id);
    }

    /// <summary>Another employee in the same organization as <paramref name="world"/>, with a bank account, on a basic of 2000 from the start.</summary>
    protected async Task<EmployeeDto> SeedSecondEmployeeAsync(PayrollWorld world, DateOnly? joined = null, string local = "ben", string suffix = "2", bool withSalary = true)
    {
        var first = world.Employee;
        var employee = await SeedPayrollEmployeeAsync(first.CompanyId, first.BranchId, first.DepartmentId, first.PositionId, first.JobGradeId, suffix, local, joined ?? Joined);
        if (withSalary)
        {
            Succeeded(await CreateAsync<SalaryAssignmentCreateRequest, SalaryAssignmentDto>(
                new SalaryAssignmentCreateRequest(employee.Id, world.Structure.Id, 2000m, joined ?? Joined, null)));
        }

        return employee;
    }

    protected async Task<SalaryComponentDto> CreateComponentAsync(string code, string name, string type, string calculation, decimal value) =>
        Succeeded(await CreateAsync<SalaryComponentCreateRequest, SalaryComponentDto>(
            new SalaryComponentCreateRequest(Code(code), Name(name), type, calculation, value, null, "Active")));

    private async Task<EmployeeDto> SeedPayrollEmployeeAsync(
        int companyId,
        int branchId,
        int departmentId,
        int positionId,
        int gradeId,
        string suffix,
        string local,
        DateOnly joined)
    {
        var request = Requests.Employee(companyId, branchId, departmentId, positionId, gradeId, suffix, local) with
        {
            JoiningDate = joined,
            DateOfBirth = new DateOnly(1970, 1, 1),
        };
        var employee = Succeeded(await CreateAsync<EmployeeCreateRequest, EmployeeDto>(request));
        Succeeded(await InScopeAsync(sp => sp.GetRequiredService<IUpdateHandler<BankAccountUpdateRequest, BankAccountDto>>()
            .HandleAsync(employee.Id, new BankAccountUpdateRequest("Sample Bank", null, "Test Holder", AccountNumber, null), default)));
        return employee;
    }

    protected Task<Result<TDto>> CreateAsync<TRequest, TDto>(TRequest request) =>
        InScopeAsync(sp => sp.GetRequiredService<ICreateHandler<TRequest, TDto>>().HandleAsync(request, default));

    protected async Task<PayrollRunDto> CreateRunAsync(int month = 1) =>
        Succeeded(await CreateAsync<PayrollRunCreateRequest, PayrollRunDto>(new PayrollRunCreateRequest(Year, month, null)));

    protected Task<Result<PayrollRunDto>> ProcessAsync(PayrollRunDto run) =>
        RunCommand<ProcessPayrollRunCommand, PayrollRunDto>(new ProcessPayrollRunCommand(run.Id, Requests.Version(run.RowVersion)));

    protected Task<Result<PayrollRunDto>> ApproveAsync(PayrollRunDto run) =>
        RunCommand<ApprovePayrollRunCommand, PayrollRunDto>(new ApprovePayrollRunCommand(run.Id, Requests.Version(run.RowVersion)));

    protected Task<Result<PayrollRunDto>> MarkPaidAsync(PayrollRunDto run) =>
        RunCommand<MarkPayrollRunPaidCommand, PayrollRunDto>(new MarkPayrollRunPaidCommand(run.Id, Requests.Version(run.RowVersion)));

    protected Task<Result<PayrollRunDto>> ReturnToDraftAsync(PayrollRunDto run, string? reason = null) =>
        RunCommand<ReturnPayrollRunToDraftCommand, PayrollRunDto>(new ReturnPayrollRunToDraftCommand(run.Id, Requests.Version(run.RowVersion), reason));

    protected async Task<PayrollRunDto> ProcessedAsync(PayrollRunDto run) => Succeeded(await ProcessAsync(run));

    /// <summary>Acts as a second signed-in user from now on (a real TST- account, removed with the test).</summary>
    protected async Task ActAsSecondUserAsync()
    {
        var account = await TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, "approver"));
        CurrentUserId = account.AccountId;
        CurrentUserName = $"{Tag} Approver";
    }

    protected Task<Result<PagedResult<PayslipDto>>> PayslipsOfAsync(int runId) =>
        Query<PayslipDto, PayslipFilter>(new PageRequest<PayslipFilter>(0, 100, null, null, new PayslipFilter(RunId: runId)));

    protected static Task<T> ScalarAsync<T>(string sql, params (string Name, object? Value)[] parameters) => Sql.ScalarAsync<T>(sql, parameters);
}
