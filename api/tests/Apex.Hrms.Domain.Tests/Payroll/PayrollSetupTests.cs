using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Payroll;

namespace Apex.Hrms.Domain.Tests.Payroll;

/// <summary>Salary components, structures, assignments and the masked account of a payslip: the rules that need no other record.</summary>
public sealed class PayrollSetupTests
{
    private static SalaryComponentDetails Details(
        string code = "HOUSING",
        CalculationMethod calculation = CalculationMethod.PercentOfBasic,
        decimal value = 10m,
        string name = "Housing") => new(code, name, ComponentType.Earning, calculation, value, null);

    private static void AssertField(Result result, string field, string? message = null)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Contains(field, result.Error.FieldErrors!.Keys);
        if (message is not null)
        {
            Assert.Equal(message, result.Error.Message);
        }
    }

    // ---- Components ----

    [Fact]
    public void A_component_trims_its_text_and_keeps_the_values()
    {
        var component = SalaryComponent.Create(Details(code: " HOUSING ", name: " Housing ") with { Description = "  " }, RecordStatus.Active).Value;

        Assert.Equal("HOUSING", component.Code);
        Assert.Equal("Housing", component.Name);
        Assert.Null(component.Description);
        Assert.Equal(10m, component.Value);
        Assert.True(component.IsActive);
    }

    [Theory]
    [InlineData(0, "The amount or percentage must be greater than zero.")]
    [InlineData(-5, "The amount or percentage must be greater than zero.")]
    public void A_component_value_must_be_above_zero(int value, string message) =>
        AssertField(SalaryComponent.Validate(Details(value: value)), "value", message);

    [Theory]
    [InlineData(CalculationMethod.PercentOfBasic)]
    [InlineData(CalculationMethod.PercentOfGross)]
    public void A_percentage_cannot_exceed_100(CalculationMethod calculation)
    {
        AssertField(SalaryComponent.Validate(Details(calculation: calculation, value: 100.01m)), "value", "A percentage cannot exceed 100.");
        Assert.True(SalaryComponent.Validate(Details(calculation: calculation, value: 100m)).IsSuccess);
    }

    [Fact]
    public void A_fixed_amount_can_exceed_100()
    {
        Assert.True(SalaryComponent.Validate(Details(calculation: CalculationMethod.Fixed, value: 2500m)).IsSuccess);
    }

    [Fact]
    public void A_component_needs_a_valid_code_and_a_name()
    {
        AssertField(SalaryComponent.Validate(Details(code: "bad code")), "code");
        AssertField(SalaryComponent.Validate(Details(name: " ")), "name");
    }

    // ---- Structures ----

    private static SalaryStructureDetails Structure(params int[] ids) => new("STAFF", "Staff", null, ids);

    [Fact]
    public void A_structure_needs_at_least_one_component()
    {
        AssertField(SalaryStructure.Validate(Structure()), "componentIds", "Choose at least one component.");
    }

    [Fact]
    public void A_structure_chooses_each_component_once_and_at_most_fifty()
    {
        AssertField(SalaryStructure.Validate(Structure(1, 2, 1)), "componentIds", "Choose each component once.");
        AssertField(SalaryStructure.Validate(Structure([.. Enumerable.Range(1, 51)])), "componentIds");
        Assert.True(SalaryStructure.Validate(Structure([.. Enumerable.Range(1, 50)])).IsSuccess);
    }

    [Fact]
    public void A_structure_keeps_its_components_in_the_chosen_order()
    {
        var structure = SalaryStructure.Create(Structure(3, 1, 2), RecordStatus.Active).Value;

        Assert.Equal([3, 1, 2], structure.ComponentIds);
    }

    [Fact]
    public void Changing_the_components_reorders_removes_and_adds_without_two_rows_ever_sharing_a_sort_order()
    {
        var structure = SalaryStructure.Create(Structure(1, 2, 3), RecordStatus.Active).Value;
        var before = structure.Components.ToDictionary(link => link.SalaryComponentId, link => link.SortOrder);

        Assert.True(structure.Update(Structure(3, 4, 1)).IsSuccess);

        Assert.Equal([3, 4, 1], structure.ComponentIds);
        Assert.Equal(3, structure.Components.Count);
        Assert.Equal(structure.Components.Count, structure.Components.Select(link => link.SortOrder).Distinct().Count());

        // Every row of the new order is in the other half of the range, so none of them takes a value an old row still held
        // while the rows are being saved (the table has a unique key on the sort order).
        var oldValues = before.Values.ToHashSet();
        Assert.All(structure.Components, link => Assert.DoesNotContain(link.SortOrder, oldValues));

        Assert.True(structure.Update(Structure(1, 3)).IsSuccess);
        Assert.Equal([1, 3], structure.ComponentIds);
    }

    // ---- Assignments ----

    private static SalaryAssignmentDetails Assignment(decimal basic = 1000m, string? remarks = null, int employeeId = 7) =>
        new(employeeId, 2, basic, new DateOnly(2026, 1, 1), remarks);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_basic_salary_must_be_above_zero(int basic) =>
        AssertField(SalaryAssignment.Validate(Assignment(basic)), "basicSalary");

    [Fact]
    public void Remarks_are_limited_to_120_characters_and_blank_remarks_become_null()
    {
        AssertField(SalaryAssignment.Validate(Assignment(remarks: new string('x', 121))), "remarks");
        Assert.Null(SalaryAssignment.Create(Assignment(remarks: "   ")).Value.Remarks);
    }

    [Fact]
    public void The_employee_of_an_assignment_never_changes()
    {
        var assignment = SalaryAssignment.Create(Assignment()).Value;

        AssertField(assignment.Update(Assignment(employeeId: 8)), "employeeId");

        Assert.Equal(7, assignment.EmployeeId);
        Assert.True(assignment.Update(Assignment(basic: 1200m)).IsSuccess);
        Assert.Equal(1200m, assignment.BasicSalary);
    }

    // ---- The masked account of a payslip ----

    [Fact]
    public void An_account_is_masked_to_four_bullets_a_space_and_the_last_four_digits()
    {
        var masked = Payslip.MaskAccount("1234567890").Value;

        Assert.Equal("•••• 7890", masked);
        Assert.Equal(9, masked.Length);
        Assert.DoesNotContain("123456", masked, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("")]
    [InlineData("12ab")]
    public void A_number_without_four_trailing_digits_cannot_be_masked(string number) =>
        Assert.True(Payslip.MaskAccount(number).IsFailure);
}
