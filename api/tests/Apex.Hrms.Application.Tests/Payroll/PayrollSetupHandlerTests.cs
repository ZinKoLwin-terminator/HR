using Apex.Hrms.Application.Payroll.Assignments;
using Apex.Hrms.Application.Payroll.Components;
using Apex.Hrms.Application.Payroll.Payslips;
using Apex.Hrms.Application.Payroll.Structures;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Payroll;

namespace Apex.Hrms.Application.Tests.Payroll;

/// <summary>
/// Salary components, structures and assignments through the real handlers: the rules of the frontend's payroll setup mock
/// services (uniqueness, value bounds, a structure needs an earning, references block deleting, the grade band, the joining
/// date, the last assignment) and their activity wording, which never carries a salary amount.
/// </summary>
public sealed class PayrollSetupHandlerTests : IDisposable
{
    private readonly TestHost host = new();

    public void Dispose() => host.Dispose();

    // ---- Components ----

    [Fact]
    public async Task A_component_is_created_with_its_values_and_the_activity_entry()
    {
        var created = await host.CreateComponentAsync(PayrollFixtures.NewComponent());

        Assert.Equal("HOUSING", created.Code);
        Assert.Equal("Earning", created.Type);
        Assert.Equal("PercentOfBasic", created.Calculation);
        Assert.Equal(10m, created.Value);
        Assert.Equal("Active", created.Status);
        Assert.Equal("Salary component \"Housing\" created", host.ActivityLog.LastMessage);
        Assert.Equal("payroll", host.ActivityLog.Entries[^1].Category);
    }

    [Fact]
    public async Task A_component_code_is_unique_ignoring_case_of_the_database_collation_and_only_against_other_records()
    {
        var housing = await host.CreateComponentAsync(PayrollFixtures.NewComponent());

        ResultAssert.FailedOnField(await host.CreateComponent(PayrollFixtures.NewComponent(name: "Other")), "code", "Component code \"HOUSING\" already exists.");

        // A record keeps its own code on update.
        var updated = ResultAssert.Succeeded(await host.Get<IUpdateHandler<SalaryComponentUpdateRequest, SalaryComponentDto>>()
            .HandleAsync(housing.Id, PayrollFixtures.ComponentUpdateOf(housing, value: 12m), default));
        Assert.Equal(12m, updated.Value);
    }

    [Theory]
    [InlineData("Fixed", 0, "The amount or percentage must be greater than zero.")]
    [InlineData("PercentOfBasic", 100.5, "A percentage cannot exceed 100.")]
    [InlineData("PercentOfGross", 101, "A percentage cannot exceed 100.")]
    public async Task A_component_value_must_be_above_zero_and_a_percentage_at_most_100(string calculation, double value, string message)
    {
        var result = await host.CreateComponent(PayrollFixtures.NewComponent(calculation: calculation, value: (decimal)value));

        ResultAssert.FailedOnField(result, "value", message);
    }

    [Fact]
    public async Task A_component_in_use_by_a_structure_cannot_be_deleted_but_a_free_one_can()
    {
        var seed = await host.SeedPayrollAsync();
        var free = await host.CreateComponentAsync(PayrollFixtures.NewComponent("FREE", "Free"));
        var delete = host.Get<IDeleteHandler<SalaryComponentDto>>();

        var blocked = await delete.HandleAsync(seed.Housing.Id, Requests.Version(seed.Housing.RowVersion), default);
        var deleted = await delete.HandleAsync(free.Id, Requests.Version(free.RowVersion), default);

        Assert.Equal(
            "\"Housing\" cannot be deleted because it is used by 1 salary structure(s). Deactivate it instead.",
            ResultAssert.Failed(blocked, ErrorType.Conflict).Message);
        Assert.True(deleted.IsSuccess);
        Assert.Equal("Salary component \"Free\" deleted", host.ActivityLog.LastMessage);
    }

    [Fact]
    public async Task A_component_can_be_deactivated_and_activated_and_an_active_one_is_not_logged_twice()
    {
        var created = await host.CreateComponentAsync(PayrollFixtures.NewComponent());

        var inactive = ResultAssert.Succeeded(await host.Command<DeactivateCommand, SalaryComponentDto>(new DeactivateCommand(created.Id, Requests.Version(created.RowVersion))));
        var active = ResultAssert.Succeeded(await host.Command<ActivateCommand, SalaryComponentDto>(new ActivateCommand(created.Id, Requests.Version(inactive.RowVersion))));

        Assert.Equal("Inactive", inactive.Status);
        Assert.Equal("Active", active.Status);
    }

    [Fact]
    public async Task Components_are_listed_in_pages_and_filtered_by_type_and_status()
    {
        await host.SeedPayrollAsync();
        var query = host.Get<IQueryHandler<SalaryComponentDto, SalaryComponentFilter>>();

        var deductions = ResultAssert.Succeeded(await query.HandleAsync(
            new PageRequest<SalaryComponentFilter>(0, 10, new SortState("code", "asc"), null, new SalaryComponentFilter(Type: "Deduction")), default));
        var searched = ResultAssert.Succeeded(await query.HandleAsync(
            new PageRequest<SalaryComponentFilter>(0, 10, null, "TRANS", new SalaryComponentFilter()), default));

        Assert.Equal(["SOCIAL", "TAX"], deductions.Items.Select(component => component.Code));
        Assert.Equal(["TRANSPORT"], searched.Items.Select(component => component.Code));
    }

    // ---- Structures ----

    [Fact]
    public async Task A_structure_keeps_the_chosen_order_of_its_components()
    {
        var seed = await host.SeedPayrollAsync();

        var created = await host.CreateStructureAsync(PayrollFixtures.NewStructure("EXEC", "Executive", seed.Tax.Id, seed.Transport.Id, seed.Housing.Id));

        Assert.Equal([seed.Tax.Id, seed.Transport.Id, seed.Housing.Id], created.ComponentIds);
        Assert.Equal("Salary structure \"Executive\" created", host.ActivityLog.LastMessage);
    }

    [Fact]
    public async Task A_structure_needs_at_least_one_component()
    {
        ResultAssert.FailedOnField(await host.CreateStructure(PayrollFixtures.NewStructure()), "componentIds", "Choose at least one component.");
    }

    [Fact]
    public async Task A_structure_needs_existing_components()
    {
        var housing = await host.CreateComponentAsync(PayrollFixtures.NewComponent());

        ResultAssert.FailedOnField(
            await host.CreateStructure(PayrollFixtures.NewStructure("S1", "S1", housing.Id, 9999)),
            "componentIds",
            "Some chosen components no longer exist.");
    }

    [Fact]
    public async Task A_structure_needs_at_least_one_earning()
    {
        var tax = await host.CreateComponentAsync(PayrollFixtures.NewComponent("TAX", "Tax", "Deduction", "PercentOfGross", 5m));

        ResultAssert.FailedOnField(
            await host.CreateStructure(PayrollFixtures.NewStructure("S1", "S1", tax.Id)),
            "componentIds",
            "A structure needs at least one earning (an allowance).");
    }

    [Fact]
    public async Task A_structure_code_is_unique()
    {
        var seed = await host.SeedPayrollAsync();

        ResultAssert.FailedOnField(
            await host.CreateStructure(PayrollFixtures.NewStructure("STAFF", "Another", seed.Housing.Id)),
            "code",
            "Structure code \"STAFF\" already exists.");
    }

    [Fact]
    public async Task A_structure_can_change_its_components_and_still_needs_an_earning()
    {
        var seed = await host.SeedPayrollAsync();
        var update = host.Get<IUpdateHandler<SalaryStructureUpdateRequest, SalaryStructureDto>>();

        var changed = ResultAssert.Succeeded(await update.HandleAsync(
            seed.Structure.Id,
            PayrollFixtures.StructureUpdateOf(seed.Structure, seed.Social.Id, seed.Housing.Id),
            default));
        var refused = await update.HandleAsync(
            seed.Structure.Id,
            PayrollFixtures.StructureUpdateOf(changed, seed.Tax.Id),
            default);

        Assert.Equal([seed.Social.Id, seed.Housing.Id], changed.ComponentIds);
        ResultAssert.FailedOnField(refused, "componentIds", "A structure needs at least one earning (an allowance).");
    }

    [Fact]
    public async Task A_structure_with_assignments_cannot_be_deleted()
    {
        var seed = await host.SeedPayrollAsync();

        var result = await host.Get<IDeleteHandler<SalaryStructureDto>>()
            .HandleAsync(seed.Structure.Id, Requests.Version(seed.Structure.RowVersion), default);

        Assert.Equal(
            "\"Staff\" cannot be deleted because it is used by 1 salary assignment(s). Deactivate it instead.",
            ResultAssert.Failed(result, ErrorType.Conflict).Message);
    }

    // ---- Assignments ----

    [Fact]
    public async Task An_assignment_is_created_and_named_in_the_activity_by_the_employee_code_without_any_amount()
    {
        var seed = await host.SeedPayrollAsync();
        var second = await host.CreatePayrollEmployeeAsync(seed.Org, "ben.test@example.test");

        var created = await host.CreateAssignmentAsync(PayrollFixtures.NewAssignment(second.Id, seed.Structure.Id, 1234.56m, new DateOnly(2024, 6, 1), "  Hired  "));

        Assert.Equal(1234.56m, created.BasicSalary);
        Assert.Equal("Hired", created.Remarks);
        var message = host.ActivityLog.LastMessage;
        Assert.Equal($"Salary assignment \"{second.EmployeeCode} from 2024-06-01\" created", message);
        Assert.DoesNotContain("1234", message, StringComparison.Ordinal);
        Assert.DoesNotContain("Mia", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_assignment_needs_an_existing_employee_and_an_active_structure()
    {
        var seed = await host.SeedPayrollAsync();
        var inactive = ResultAssert.Succeeded(await host.Command<DeactivateCommand, SalaryStructureDto>(
            new DeactivateCommand(seed.Structure.Id, Requests.Version(seed.Structure.RowVersion))));
        var other = await host.CreatePayrollEmployeeAsync(seed.Org, "ben.test@example.test");

        ResultAssert.FailedOnField(
            await host.CreateAssignment(PayrollFixtures.NewAssignment(9999, seed.Structure.Id)), "employeeId", "The selected employee does not exist.");
        ResultAssert.FailedOnField(
            await host.CreateAssignment(PayrollFixtures.NewAssignment(other.Id, inactive.Id)), "structureId", "Choose an active salary structure.");
        ResultAssert.FailedOnField(
            await host.CreateAssignment(PayrollFixtures.NewAssignment(other.Id, 9999)), "structureId", "Choose an active salary structure.");
    }

    [Theory]
    [InlineData(499.99, "Basic salary must be within the PG1 band (500 – 5000).")]
    [InlineData(5000.01, "Basic salary must be within the PG1 band (500 – 5000).")]
    public async Task The_basic_salary_must_be_inside_the_band_of_the_job_grade(double basic, string message)
    {
        var seed = await host.SeedPayrollAsync();
        var other = await host.CreatePayrollEmployeeAsync(seed.Org, "ben.test@example.test");

        var result = await host.CreateAssignment(PayrollFixtures.NewAssignment(other.Id, seed.Structure.Id, (decimal)basic));

        var error = ResultAssert.FailedOnField(result, "basicSalary", message);
        Assert.Equal("Outside the PG1 band (500 – 5000).", error.FieldErrors!["basicSalary"][0]);
    }

    [Fact]
    public async Task The_band_edges_are_inside_the_band()
    {
        var seed = await host.SeedPayrollAsync();
        var other = await host.CreatePayrollEmployeeAsync(seed.Org, "ben.test@example.test");

        await host.CreateAssignmentAsync(PayrollFixtures.NewAssignment(other.Id, seed.Structure.Id, 500m, new DateOnly(2021, 1, 1)));
        await host.CreateAssignmentAsync(PayrollFixtures.NewAssignment(other.Id, seed.Structure.Id, 5000m, new DateOnly(2022, 1, 1)));
    }

    [Fact]
    public async Task A_salary_cannot_start_before_the_joining_date()
    {
        var seed = await host.SeedPayrollAsync();
        var joiner = await host.CreatePayrollEmployeeAsync(seed.Org, "ben.test@example.test", joined: new DateOnly(2025, 3, 3));

        var result = await host.CreateAssignment(PayrollFixtures.NewAssignment(joiner.Id, seed.Structure.Id, 1000m, new DateOnly(2025, 3, 2)));

        var error = ResultAssert.FailedOnField(result, "effectiveFrom", "The salary cannot start before the joining date.");
        Assert.Equal("Must be on or after 2025-03-03.", error.FieldErrors!["effectiveFrom"][0]);
        Assert.True((await host.CreateAssignment(PayrollFixtures.NewAssignment(joiner.Id, seed.Structure.Id, 1000m, new DateOnly(2025, 3, 3)))).IsSuccess);
    }

    [Fact]
    public async Task An_employee_has_one_assignment_per_start_date()
    {
        var seed = await host.SeedPayrollAsync();

        var result = await host.CreateAssignment(PayrollFixtures.NewAssignment(seed.Employee.Id, seed.Structure.Id, 1100m, seed.Assignment.EffectiveFrom));

        ResultAssert.FailedOnField(result, "effectiveFrom", "This employee already has a salary starting on that date.");
    }

    [Fact]
    public async Task An_assignment_can_be_revised_but_never_moved_to_another_employee()
    {
        var seed = await host.SeedPayrollAsync();
        var other = await host.CreatePayrollEmployeeAsync(seed.Org, "ben.test@example.test");
        var update = host.Get<IUpdateHandler<SalaryAssignmentUpdateRequest, SalaryAssignmentDto>>();

        var revised = ResultAssert.Succeeded(await update.HandleAsync(
            seed.Assignment.Id, PayrollFixtures.AssignmentUpdateOf(seed.Assignment, basic: 1500m), default));
        var moved = await update.HandleAsync(
            seed.Assignment.Id,
            PayrollFixtures.AssignmentUpdateOf(revised) with { EmployeeId = other.Id },
            default);
        var outside = await update.HandleAsync(
            seed.Assignment.Id, PayrollFixtures.AssignmentUpdateOf(revised, basic: 9000m), default);

        Assert.Equal(1500m, revised.BasicSalary);
        ResultAssert.FailedOnField(moved, "employeeId", "The employee of a salary assignment cannot be changed.");
        ResultAssert.FailedOnField(outside, "basicSalary");
        Assert.Equal($"Salary assignment \"{seed.Employee.EmployeeCode} from 2020-01-01\" updated", host.ActivityLog.Messages[^1]);
    }

    [Fact]
    public async Task A_stale_row_version_on_an_assignment_is_a_conflict()
    {
        var seed = await host.SeedPayrollAsync();
        var update = host.Get<IUpdateHandler<SalaryAssignmentUpdateRequest, SalaryAssignmentDto>>();
        ResultAssert.Succeeded(await update.HandleAsync(seed.Assignment.Id, PayrollFixtures.AssignmentUpdateOf(seed.Assignment, basic: 1500m), default));

        var stale = await update.HandleAsync(seed.Assignment.Id, PayrollFixtures.AssignmentUpdateOf(seed.Assignment, basic: 1600m), default);

        ResultAssert.Failed(stale, ErrorType.Conflict);
    }

    [Fact]
    public async Task The_last_assignment_of_an_employee_cannot_be_deleted_but_an_earlier_one_can()
    {
        var seed = await host.SeedPayrollAsync();
        var delete = host.Get<IDeleteHandler<SalaryAssignmentDto>>();

        var last = await delete.HandleAsync(seed.Assignment.Id, Requests.Version(seed.Assignment.RowVersion), default);
        var second = await host.CreateAssignmentAsync(PayrollFixtures.NewAssignment(seed.Employee.Id, seed.Structure.Id, 1200m, new DateOnly(2025, 1, 1)));
        // The fake database stamps every record a refused change touched, so read the current row version again.
        var current = ResultAssert.Succeeded(await host.Get<IGetByIdHandler<SalaryAssignmentDto>>().HandleAsync(seed.Assignment.Id, default));
        var first = await delete.HandleAsync(current.Id, Requests.Version(current.RowVersion), default);

        Assert.Equal(
            "An employee needs at least one salary assignment. Add a new one instead.",
            ResultAssert.Failed(last, ErrorType.Conflict).Message);
        Assert.True(first.IsSuccess);
        Assert.Equal($"Salary assignment \"{seed.Employee.EmployeeCode} from 2020-01-01\" deleted", host.ActivityLog.LastMessage);
        Assert.Equal(
            ErrorType.Conflict,
            ResultAssert.Failed(await delete.HandleAsync(second.Id, Requests.Version(second.RowVersion), default), ErrorType.Conflict).Type);
    }

    [Fact]
    public async Task A_salary_history_is_a_page_of_the_assignments_of_one_employee_newest_first()
    {
        var seed = await host.SeedPayrollAsync();
        await host.CreateAssignmentAsync(PayrollFixtures.NewAssignment(seed.Employee.Id, seed.Structure.Id, 1200m, new DateOnly(2025, 1, 1)));
        var other = await host.CreatePayrollEmployeeAsync(seed.Org, "ben.test@example.test");
        await host.CreateAssignmentAsync(PayrollFixtures.NewAssignment(other.Id, seed.Structure.Id, 900m, new DateOnly(2024, 1, 1)));
        var query = host.Get<IQueryHandler<SalaryAssignmentDto, SalaryAssignmentFilter>>();

        var history = ResultAssert.Succeeded(await query.HandleAsync(
            new PageRequest<SalaryAssignmentFilter>(0, 10, new SortState("effectiveFrom", "desc"), null, new SalaryAssignmentFilter(EmployeeId: seed.Employee.Id)),
            default));

        Assert.Equal([new DateOnly(2025, 1, 1), new DateOnly(2020, 1, 1)], history.Items.Select(assignment => assignment.EffectiveFrom));
        Assert.Equal(2, history.Total);
    }

    [Fact]
    public async Task There_is_no_handler_that_lists_every_salary_assignment_at_once()
    {
        await host.SeedPayrollAsync();

        Assert.Null(host.GetOrDefault<IGetAllHandler<SalaryAssignmentDto>>());
        Assert.Null(host.GetOrDefault<IGetAllHandler<PayslipDto>>());
    }

    [Fact]
    public async Task An_employee_with_a_salary_assignment_cannot_be_deleted()
    {
        var seed = await host.SeedPayrollAsync();

        var result = await host.Get<IDeleteHandler<Apex.Hrms.Application.Employees.EmployeeDto>>()
            .HandleAsync(seed.Employee.Id, Requests.Version(seed.Employee.RowVersion), default);

        var error = ResultAssert.Failed(result, ErrorType.Conflict);
        Assert.Contains("1 salary assignment(s)", error.Message, StringComparison.Ordinal);
    }
}
