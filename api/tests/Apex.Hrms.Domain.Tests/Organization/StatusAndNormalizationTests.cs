using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Domain.Tests.Organization;

public sealed class StatusAndNormalizationTests
{
    private static readonly DateOnly Today = new(2026, 11, 9);

    private static DepartmentDetails Department(string name = "Engineering", string code = "ENG") =>
        new(CompanyId: 1, BranchId: 1, code, name, Description: " ", HeadEmployeeId: null, CostCenter: null);

    [Fact]
    public void Create_starts_with_the_given_status()
    {
        var department = Domain.Organization.Department.Create(Department(), RecordStatus.Inactive).Value;

        Assert.False(department.IsActive);
    }

    [Fact]
    public void Deactivate_and_Activate_change_the_status_and_are_idempotent()
    {
        var department = Domain.Organization.Department.Create(Department(), RecordStatus.Active).Value;

        Assert.True(department.Deactivate().IsSuccess);
        Assert.Equal(RecordStatus.Inactive, department.Status);
        Assert.True(department.Deactivate().IsSuccess);
        Assert.Equal(RecordStatus.Inactive, department.Status);
        Assert.True(department.Activate().IsSuccess);
        Assert.True(department.Activate().IsSuccess);
        Assert.Equal(RecordStatus.Active, department.Status);
    }

    [Fact]
    public void Update_never_changes_the_status()
    {
        var department = Domain.Organization.Department.Create(Department(), RecordStatus.Active).Value;
        department.Deactivate();

        var result = department.Update(Department(name: "Renamed"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Renamed", department.Name);
        Assert.Equal(RecordStatus.Inactive, department.Status);
    }

    [Fact]
    public void Normalized_trims_text_turns_blank_optionals_into_null_and_is_idempotent()
    {
        var once = Department(name: "  Engineering  ", code: " ENG ").Normalized();

        Assert.Equal("Engineering", once.Name);
        Assert.Equal("ENG", once.Code);
        Assert.Null(once.Description);
        Assert.Equal(once, once.Normalized());
    }

    [Fact]
    public void Validate_reports_violations_without_creating_anything()
    {
        var result = Domain.Organization.Department.Validate(Department(name: " ", code: "bad"));

        Assert.True(result.IsFailure);
        Assert.Contains("name", result.Error.FieldErrors!.Keys);
        Assert.Contains("code", result.Error.FieldErrors.Keys);
    }

    [Fact]
    public void Validate_of_a_position_flags_a_report_to_itself_only_for_an_existing_position()
    {
        var details = new PositionDetails(1, null, 1, "ENG-01", "Engineer", null, 1, ReportsToPositionId: 5);

        Assert.True(Position.Validate(details).IsSuccess);
        Assert.Contains("reportsToPositionId", Position.Validate(details, ownId: 5).Error.FieldErrors!.Keys);
    }

    [Fact]
    public void Company_validate_uses_the_given_today_for_the_future_date_rule()
    {
        var details = new CompanyDetails(
            "AHT", "Apex", "Apex Ltd", null, null, "Tech", "a@b.test", "+95 1 555 0100", null, null, "Yangon", "Myanmar",
            Today.AddDays(1));

        Assert.True(Company.Validate(details, Today).IsFailure);
        Assert.True(Company.Validate(details, Today.AddDays(1)).IsSuccess);
    }
}
