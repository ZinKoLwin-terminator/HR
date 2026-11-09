using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Domain.Tests.Organization;

public sealed class JobGradeAndPositionTests
{
    private static JobGradeDetails Grade(short level = 1, decimal min = 100m, decimal max = 200m) =>
        new("G1", "Junior", level, min, max, Description: null, RecordStatus.Active);

    private static PositionDetails PositionDetails(short headcount = 1, int? reportsTo = null) => new(
        DepartmentId: 1,
        SectionId: null,
        JobGradeId: 1,
        "ENG-01",
        "Engineer",
        Description: null,
        headcount,
        reportsTo,
        RecordStatus.Active);

    [Fact]
    public void JobGrade_accepts_an_equal_minimum_and_maximum() =>
        Assert.True(JobGrade.Create(Grade(min: 100m, max: 100m)).IsSuccess);

    [Fact]
    public void JobGrade_rejects_a_maximum_below_the_minimum()
    {
        var result = JobGrade.Create(Grade(min: 200m, max: 100m));

        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Maximum salary must be greater than or equal to minimum salary.", result.Error.Message);
        Assert.Equal(["Must be ≥ minimum salary."], result.Error.FieldErrors!["maxSalary"]);
    }

    [Fact]
    public void JobGrade_rejects_a_level_below_one() =>
        Assert.Contains("level", JobGrade.Create(Grade(level: 0)).Error.FieldErrors!.Keys);

    [Fact]
    public void JobGrade_rejects_a_negative_minimum() =>
        Assert.Contains("minSalary", JobGrade.Create(Grade(min: -1m, max: 10m)).Error.FieldErrors!.Keys);

    [Fact]
    public void Position_rejects_a_headcount_below_one() =>
        Assert.Contains("headcount", Position.Create(PositionDetails(headcount: 0)).Error.FieldErrors!.Keys);

    [Fact]
    public void Position_can_report_to_another_position() =>
        Assert.Equal(7, Position.Create(PositionDetails(reportsTo: 7)).Value.ReportsToPositionId);
}
