using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Domain.Tests.Organization;

public sealed class BranchTests
{
    private static readonly DateOnly Today = new(2026, 11, 9);

    private static BranchDetails Details(DateOnly? opened = null) => new(
        CompanyId: 1,
        "YGN-01",
        "Yangon Office",
        BranchType.HeadOffice,
        Email: null,
        Phone: null,
        Address: null,
        "Yangon",
        "Myanmar",
        ManagerEmployeeId: null,
        opened ?? new DateOnly(2020, 1, 1),
        RecordStatus.Active);

    [Theory]
    [InlineData(BranchType.HeadOffice, "Head Office")]
    [InlineData(BranchType.Branch, "Branch")]
    [InlineData(BranchType.RegionalOffice, "Regional Office")]
    [InlineData(BranchType.Warehouse, "Warehouse")]
    public void BranchTypeText_round_trips_the_api_texts(BranchType type, string text)
    {
        Assert.Equal(text, BranchTypeText.ToText(type));
        Assert.True(BranchTypeText.TryParse(text, out var parsed));
        Assert.Equal(type, parsed);
    }

    [Theory]
    [InlineData("HeadOffice")]
    [InlineData("head office")]
    [InlineData(null)]
    public void BranchTypeText_rejects_unknown_texts(string? text) =>
        Assert.False(BranchTypeText.TryParse(text, out _));

    [Fact]
    public void Create_rejects_an_opened_date_in_the_future()
    {
        var result = Branch.Create(Details(opened: Today.AddDays(1)), Today);

        Assert.True(result.IsFailure);
        Assert.Contains("openedDate", result.Error.FieldErrors!.Keys);
    }

    [Fact]
    public void Create_keeps_the_company_and_type()
    {
        var branch = Branch.Create(Details(), Today).Value;

        Assert.Equal(1, branch.CompanyId);
        Assert.Equal(BranchType.HeadOffice, branch.Type);
    }
}
