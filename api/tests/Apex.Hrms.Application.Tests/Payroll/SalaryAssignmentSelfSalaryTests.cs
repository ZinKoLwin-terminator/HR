using Apex.Hrms.Application.Payroll.Assignments;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Tests.Payroll;

/// <summary>Nobody changes their own salary, and a creator who may not see an employee's salary learns nothing about it.</summary>
public sealed class SalaryAssignmentSelfSalaryTests : IDisposable
{
    private const string OwnSalary = "You cannot change your own salary.";

    private readonly TestHost host = new();

    public void Dispose() => host.Dispose();

    [Fact]
    public async Task Creating_an_assignment_for_your_own_employee_is_a_conflict_even_with_payroll_approve()
    {
        var seed = await host.SeedPayrollAsync();
        host.User.EmployeeId = seed.Employee.Id;

        var result = await host.CreateAssignment(PayrollFixtures.NewAssignment(seed.Employee.Id, seed.Structure.Id, 1200m, new DateOnly(2025, 1, 1)));

        Assert.Equal(OwnSalary, ResultAssert.Failed(result, ErrorType.Conflict).Message);
    }

    [Fact]
    public async Task Updating_your_own_assignment_is_a_conflict()
    {
        var seed = await host.SeedPayrollAsync();
        host.User.EmployeeId = seed.Employee.Id;

        var result = await host.Get<IUpdateHandler<SalaryAssignmentUpdateRequest, SalaryAssignmentDto>>().HandleAsync(
            seed.Assignment.Id, PayrollFixtures.AssignmentUpdateOf(seed.Assignment, basic: 1500m), default);

        Assert.Equal(OwnSalary, ResultAssert.Failed(result, ErrorType.Conflict).Message);
    }

    [Fact]
    public async Task Deleting_your_own_assignment_is_a_conflict_with_the_own_salary_message()
    {
        var seed = await host.SeedPayrollAsync();
        var second = await host.CreateAssignmentAsync(PayrollFixtures.NewAssignment(seed.Employee.Id, seed.Structure.Id, 1200m, new DateOnly(2025, 1, 1)));
        host.User.EmployeeId = seed.Employee.Id;

        var result = await host.Get<IDeleteHandler<SalaryAssignmentDto>>().HandleAsync(second.Id, Requests.Version(second.RowVersion), default);

        Assert.Equal(OwnSalary, ResultAssert.Failed(result, ErrorType.Conflict).Message);
    }

    [Fact]
    public async Task Another_payroll_approver_can_still_change_the_salary_of_that_employee()
    {
        var seed = await host.SeedPayrollAsync();
        var officer = await host.CreatePayrollEmployeeAsync(seed.Org, "ben.test@example.test");
        host.User.EmployeeId = officer.Id;

        var result = await host.CreateAssignment(PayrollFixtures.NewAssignment(seed.Employee.Id, seed.Structure.Id, 1200m, new DateOnly(2025, 1, 1)));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task A_creator_without_payroll_approve_gets_not_found_before_any_validation_rule_answers()
    {
        var seed = await host.SeedPayrollAsync();
        var officer = await host.CreatePayrollEmployeeAsync(seed.Org, "ben.test@example.test");
        host.User.EmployeeId = officer.Id;
        host.Permissions.Only("payroll:View", "payroll:Create", "payroll:Edit");

        // Outside the grade band, on a date that clashes, and a start before joining: each would answer with a validation message.
        var band = await host.CreateAssignment(PayrollFixtures.NewAssignment(seed.Employee.Id, seed.Structure.Id, 900_000m, new DateOnly(2025, 1, 1)));
        var clash = await host.CreateAssignment(PayrollFixtures.NewAssignment(seed.Employee.Id, seed.Structure.Id, 1000m, seed.Assignment.EffectiveFrom));
        var early = await host.CreateAssignment(PayrollFixtures.NewAssignment(seed.Employee.Id, seed.Structure.Id, 1000m, new DateOnly(1990, 1, 1)));

        ResultAssert.Failed(band, ErrorType.NotFound);
        ResultAssert.Failed(clash, ErrorType.NotFound);
        ResultAssert.Failed(early, ErrorType.NotFound);
    }

    [Fact]
    public async Task A_creator_without_payroll_approve_who_targets_themselves_gets_the_own_salary_conflict()
    {
        var seed = await host.SeedPayrollAsync();
        host.User.EmployeeId = seed.Employee.Id;
        host.Permissions.Only("payroll:View", "payroll:Create", "payroll:Edit");

        var result = await host.CreateAssignment(PayrollFixtures.NewAssignment(seed.Employee.Id, seed.Structure.Id, 1200m, new DateOnly(2025, 1, 1)));

        Assert.Equal(OwnSalary, ResultAssert.Failed(result, ErrorType.Conflict).Message);
    }
}
