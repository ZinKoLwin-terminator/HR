using Apex.Hrms.Application.Organization.Branches;
using Apex.Hrms.Application.Organization.Companies;
using Apex.Hrms.Application.Organization.JobGrades;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Tests.Organization;

public sealed class QueryHandlerTests : IDisposable
{
    private readonly TestHost host = new();

    public void Dispose() => host.Dispose();

    private IQueryHandler<CompanyDto, CompanyFilter> Companies => host.Get<IQueryHandler<CompanyDto, CompanyFilter>>();

    private static PageRequest<CompanyFilter> Page(
        int pageIndex = 0,
        int pageSize = 10,
        SortState? sort = null,
        string? search = null,
        CompanyFilter? filter = null) => new(pageIndex, pageSize, sort, search, filter ?? new CompanyFilter());

    private async Task SeedCompaniesAsync()
    {
        await host.CreateCompanyAsync("CHA", "Charlie Co");
        await host.CreateCompanyAsync("ALP", "Alpha Co");
        await host.CreateCompanyAsync("BRA", "Bravo Co", status: "Inactive");
    }

    [Fact]
    public async Task Query_without_options_returns_everything_in_id_order()
    {
        await SeedCompaniesAsync();

        var page = ResultAssert.Succeeded(await Companies.HandleAsync(Page(), default));

        Assert.Equal(["CHA", "ALP", "BRA"], page.Items.Select(c => c.Code));
        Assert.Equal(3, page.Total);
    }

    [Theory]
    [InlineData("asc", new[] { "ALP", "BRA", "CHA" })]
    [InlineData("desc", new[] { "CHA", "BRA", "ALP" })]
    public async Task Query_sorts_by_the_requested_field_case_insensitively(string direction, string[] expected)
    {
        await SeedCompaniesAsync();

        var page = ResultAssert.Succeeded(await Companies.HandleAsync(Page(sort: new SortState("CODE", direction)), default));

        Assert.Equal(expected, page.Items.Select(c => c.Code));
    }

    [Fact]
    public async Task Query_rejects_an_unknown_sort_field()
    {
        var result = await Companies.HandleAsync(Page(sort: new SortState("salary", "asc")), default);

        ResultAssert.FailedOnField(result, "sort");
    }

    [Fact]
    public async Task Query_ignores_a_sort_without_a_direction()
    {
        await SeedCompaniesAsync();

        var page = ResultAssert.Succeeded(await Companies.HandleAsync(Page(sort: new SortState("code", "")), default));

        Assert.Equal(["CHA", "ALP", "BRA"], page.Items.Select(c => c.Code));
    }

    [Fact]
    public async Task Query_searches_code_name_legal_name_and_city()
    {
        await SeedCompaniesAsync();

        var page = ResultAssert.Succeeded(await Companies.HandleAsync(Page(search: "  Alpha "), default));

        Assert.Equal(["ALP"], page.Items.Select(c => c.Code));
        Assert.Equal(1, page.Total);
    }

    [Theory]
    [InlineData("Inactive", 1)]
    [InlineData("Active", 2)]
    [InlineData("all", 3)]
    [InlineData("", 3)]
    [InlineData("Bogus", 0)]
    public async Task Query_filters_by_status_treating_blank_and_all_as_no_filter(string status, int expected)
    {
        await SeedCompaniesAsync();

        var page = ResultAssert.Succeeded(await Companies.HandleAsync(Page(filter: new CompanyFilter(status)), default));

        Assert.Equal(expected, page.Total);
    }

    [Fact]
    public async Task Query_pages_and_clamps_an_index_past_the_last_page()
    {
        await SeedCompaniesAsync();

        var second = ResultAssert.Succeeded(await Companies.HandleAsync(Page(pageIndex: 1, pageSize: 2), default));
        var beyond = ResultAssert.Succeeded(await Companies.HandleAsync(Page(pageIndex: 9, pageSize: 2), default));

        Assert.Equal(["BRA"], second.Items.Select(c => c.Code));
        Assert.Equal(1, beyond.PageIndex);
        Assert.Equal(2, beyond.PageSize);
        Assert.Equal(3, beyond.Total);
    }

    [Fact]
    public async Task Query_limits_the_page_size()
    {
        var page = ResultAssert.Succeeded(await Companies.HandleAsync(Page(pageSize: 100_000), default));

        Assert.Equal(Paging.MaxPageSize, page.PageSize);
    }

    [Fact]
    public async Task Branch_query_filters_by_company_type_and_status()
    {
        var first = await host.CreateCompanyAsync("A1", "First");
        var second = await host.CreateCompanyAsync("B2", "Second");
        await host.CreateBranchAsync(first.Id, "ONE");
        await host.CreateBranchAsync(second.Id, "TWO");
        var branches = host.Get<IQueryHandler<BranchDto, BranchFilter>>();

        var byCompany = ResultAssert.Succeeded(await branches.HandleAsync(
            new PageRequest<BranchFilter>(0, 10, null, null, new BranchFilter(CompanyId: second.Id)), default));
        var byType = ResultAssert.Succeeded(await branches.HandleAsync(
            new PageRequest<BranchFilter>(0, 10, null, null, new BranchFilter(Type: "Warehouse")), default));

        Assert.Equal(["TWO"], byCompany.Items.Select(b => b.Code));
        Assert.Equal(0, byType.Total);
    }

    [Fact]
    public async Task Job_grade_query_sorts_numbers_numerically()
    {
        await host.CreateJobGradeAsync("G1");
        var update = host.Get<ICreateHandler<JobGradeCreateRequest, JobGradeDto>>();
        ResultAssert.Succeeded(await update.HandleAsync(Requests.JobGrade("G2") with { Level = 10 }, default));
        ResultAssert.Succeeded(await update.HandleAsync(Requests.JobGrade("G3") with { Level = 2 }, default));

        var page = ResultAssert.Succeeded(await host.Get<IQueryHandler<JobGradeDto, JobGradeFilter>>().HandleAsync(
            new PageRequest<JobGradeFilter>(0, 10, new SortState("level", "desc"), null, new JobGradeFilter()), default));

        Assert.Equal([10, 2, 1], page.Items.Select(g => (int)g.Level));
    }

    [Fact]
    public async Task Query_rejects_a_search_longer_than_the_limit_but_accepts_exactly_the_limit()
    {
        var tooLong = await Companies.HandleAsync(Page(search: new string('a', Paging.MaxSearchLength + 1)), default);
        var atLimit = await Companies.HandleAsync(Page(search: new string('a', Paging.MaxSearchLength)), default);

        ResultAssert.FailedOnField(tooLong, "search");
        Assert.True(atLimit.IsSuccess);
    }
}
