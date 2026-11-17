using Apex.Hrms.Domain.Common;
using Apex.Hrms.Infrastructure.Persistence;

namespace Apex.Hrms.Infrastructure.Tests;

/// <summary>
/// Uses the message formats SQL Server produces. The messages here are fictional; the tests also prove that no part
/// of the raw message (which can hold the duplicate value) reaches the result.
/// </summary>
public sealed class SqlConflictsTests
{
    private const string DuplicateKey = "Violation of UNIQUE KEY constraint '{0}'. Cannot insert duplicate key in object 'org.X'. The duplicate key value is (SECRET-VALUE).";
    private const string DuplicateIndex = "Cannot insert duplicate key row in object 'org.X' with unique index '{0}'. The duplicate key value is (SECRET-VALUE).";

    [Theory]
    [InlineData(2627, "UQ_Company_Code", "code", true)]
    [InlineData(2627, "UQ_Branch_Code", "code", true)]
    [InlineData(2627, "UQ_Position_Code", "code", true)]
    [InlineData(2601, "UQ_Company_Name", "name", false)]
    [InlineData(2627, "UQ_Category_Code", "code", true)]
    [InlineData(2627, "UQ_Category_Name", "name", true)]
    [InlineData(2627, "UQ_Template_Code", "code", true)]
    [InlineData(2627, "UQ_RatingBand_Label", "label", true)]
    public void A_unique_violation_is_a_validation_error_on_the_field_named_by_the_constraint(
        int number,
        string constraint,
        string field,
        bool keyConstraint)
    {
        var message = (keyConstraint ? DuplicateKey : DuplicateIndex).Replace("{0}", constraint, StringComparison.Ordinal);

        var error = SqlConflicts.Translate(number, message)!;

        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal([field], error.FieldErrors!.Keys);
        Assert.Equal([$"{char.ToUpperInvariant(field[0])}{field[1..]} must be unique."], error.FieldErrors[field]);
        Assert.DoesNotContain("SECRET-VALUE", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET-VALUE", error.FieldErrors[field][0], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("UQ_EvaluationPeriod_CompanyId_Name", "name", "Period name must be unique within the company.")]
    [InlineData("UQ_RatingBand_MinScore", "minScore", "Minimum score must be unique.")]
    public void Kpi_unique_constraints_with_their_own_wording_are_validation_errors_on_their_field(string constraint, string field, string message)
    {
        var error = SqlConflicts.Translate(2627, DuplicateKey.Replace("{0}", constraint, StringComparison.Ordinal))!;

        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal([field], error.FieldErrors!.Keys);
        Assert.Equal([message], error.FieldErrors[field]);
        Assert.DoesNotContain("SECRET-VALUE", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("UQ_Scorecard_EmployeeId_PeriodId", "already has a scorecard")]
    [InlineData("UQ_ScorecardItem_ScorecardId_TemplateId", "was just assigned")]
    public void The_scorecard_races_are_conflicts_that_never_show_the_value(string constraint, string wording)
    {
        var error = SqlConflicts.Translate(2627, DuplicateKey.Replace("{0}", constraint, StringComparison.Ordinal))!;

        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Contains(wording, error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET-VALUE", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_scorecard_whose_reviewer_is_the_employee_is_a_conflict()
    {
        var error = SqlConflicts.Translate(547, "The INSERT statement conflicted with the CHECK constraint \"CK_Scorecard_NotOwnReviewer\".")!;

        Assert.Equal(ErrorType.Conflict, error.Type);
    }

    [Theory]
    [InlineData("UQ_Branch_Id_CompanyId")]
    [InlineData("something unexpected")]
    public void A_unique_violation_without_a_single_field_is_a_generic_conflict(string constraint)
    {
        var error = SqlConflicts.Translate(2627, DuplicateKey.Replace("{0}", constraint, StringComparison.Ordinal))!;

        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Null(error.FieldErrors);
        Assert.DoesNotContain("SECRET-VALUE", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(51001, "A paid payroll run is final, and an approved one can only be marked as paid or returned to draft.")]
    [InlineData(51002, "Payslips of an approved or paid payroll run are locked.")]
    [InlineData(51003, "Payslip lines of an approved or paid payroll run are locked.")]
    [InlineData(51004, "Warnings of an approved or paid payroll run are locked.")]
    public void Payroll_finality_triggers_are_conflicts(int number, string message)
    {
        var error = SqlConflicts.Translate(number, "raw trigger text with PII")!;

        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Equal(message, error.Message);
    }

    [Theory]
    [InlineData("The UPDATE statement conflicted with the FOREIGN KEY constraint \"FK_Department_Branch\". The conflict occurred in database \"HR\", table \"org.Branch\", column 'Id'.")]
    [InlineData("The DELETE statement conflicted with the REFERENCE constraint \"FK_Position_Department\". The conflict occurred in database \"HR\", table \"org.Position\", column 'DepartmentId'.")]
    [InlineData("The statement conflicted with a constraint.")]
    public void Foreign_key_and_reference_violations_are_conflicts(string message)
    {
        var error = SqlConflicts.Translate(547, message)!;

        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Null(error.FieldErrors);
    }

    [Theory]
    [InlineData("CK_Branch_Code", "code")]
    [InlineData("CK_Position_Headcount", "headcount")]
    [InlineData("CK_JobGrade_SalaryBand", "maxSalary")]
    [InlineData("CK_Position_ReportsToNotSelf", "reportsToPositionId")]
    [InlineData("CK_Template_LowerCapped", "maxAchievementPct")]
    [InlineData("CK_Template_Capped", "maxAchievementPct")]
    [InlineData("CK_Template_Rating", "maxRating")]
    [InlineData("CK_Template_Range", "rangeMax")]
    [InlineData("CK_Template_DefaultTarget", "defaultTarget")]
    [InlineData("CK_EvaluationPeriod_DateOrder", "endDate")]
    [InlineData("CK_RatingBand_MinScore", "minScore")]
    public void A_check_violation_is_a_validation_error_on_the_guarded_field(string constraint, string field)
    {
        var error = SqlConflicts.Translate(
            547,
            $"The INSERT statement conflicted with the CHECK constraint \"{constraint}\". The conflict occurred in database \"HR\", table \"org.X\".")!;

        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Contains(field, error.FieldErrors!.Keys);
    }

    [Fact]
    public void A_check_violation_with_an_unknown_name_is_a_validation_error_without_a_field()
    {
        var error = SqlConflicts.Translate(547, "conflicted with the CHECK constraint \"CK_Some_Odd_Rule\".")!;

        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Null(error.FieldErrors);
    }

    [Theory]
    [InlineData(1205)]
    [InlineData(-2)]
    [InlineData(8152)]
    public void Other_sql_errors_are_not_translated(int number) =>
        Assert.Null(SqlConflicts.Translate(number, "UQ_Company_Code"));
}
