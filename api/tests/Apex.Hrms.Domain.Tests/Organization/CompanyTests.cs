using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Domain.Tests.Organization;

public sealed class CompanyTests
{
    private static readonly DateOnly Today = new(2026, 11, 9);

    private static CompanyDetails Details(
        string code = "AHT",
        string name = "Apex Holdings",
        DateOnly? founded = null) => new(
        code,
        name,
        "Apex Holdings Ltd",
        RegistrationNumber: "  ",
        TaxId: null,
        "Technology",
        "info@example.test",
        "+95 1 555 0100",
        Website: null,
        Address: null,
        "Yangon",
        "Myanmar",
        founded ?? new DateOnly(2015, 3, 1));

    [Fact]
    public void Create_trims_text_and_stores_blank_optional_values_as_null()
    {
        var result = Company.Create(Details(code: " AHT ", name: " Apex Holdings "), RecordStatus.Active, Today);

        Assert.True(result.IsSuccess);
        Assert.Equal("AHT", result.Value.Code);
        Assert.Equal("Apex Holdings", result.Value.Name);
        Assert.Null(result.Value.RegistrationNumber);
        Assert.True(result.Value.IsActive);
    }

    [Fact]
    public void Create_rejects_a_founded_date_in_the_future()
    {
        var result = Company.Create(Details(founded: Today.AddDays(1)), RecordStatus.Active, Today);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Contains("foundedDate", result.Error.FieldErrors!.Keys);
    }

    [Fact]
    public void Create_accepts_today_as_the_founded_date() =>
        Assert.True(Company.Create(Details(founded: Today), RecordStatus.Active, Today).IsSuccess);

    [Theory]
    [InlineData("aht")]
    [InlineData("A HT")]
    [InlineData("")]
    public void Create_rejects_an_invalid_code(string code)
    {
        var result = Company.Create(Details(code: code), RecordStatus.Active, Today);

        Assert.True(result.IsFailure);
        Assert.Contains("code", result.Error.FieldErrors!.Keys);
    }

    [Fact]
    public void Create_reports_every_violation_at_once()
    {
        var result = Company.Create(Details(code: "bad", name: " ", founded: Today.AddYears(1)), RecordStatus.Active, Today);

        Assert.Equal("One or more validation errors occurred.", result.Error.Message);
        Assert.Equal(3, result.Error.FieldErrors!.Count);
    }

    [Fact]
    public void Update_changes_nothing_when_a_rule_is_violated()
    {
        var company = Company.Create(Details(), RecordStatus.Active, Today).Value;

        var result = company.Update(Details(name: "Renamed", founded: Today.AddDays(5)), Today);

        Assert.True(result.IsFailure);
        Assert.Equal("Apex Holdings", company.Name);
    }

    [Fact]
    public void Update_applies_valid_changes()
    {
        var company = Company.Create(Details(), RecordStatus.Active, Today).Value;

        var result = company.Update(Details(name: "Renamed"), Today);

        Assert.True(result.IsSuccess);
        Assert.Equal("Renamed", company.Name);
    }
}
