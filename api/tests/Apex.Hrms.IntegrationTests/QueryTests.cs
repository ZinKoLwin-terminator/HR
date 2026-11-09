using Apex.Hrms.Application;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Organization.Branches;
using Apex.Hrms.Application.Organization.Companies;
using Apex.Hrms.Application.Organization.Departments;
using Apex.Hrms.Application.Organization.JobGrades;
using Apex.Hrms.Application.Organization.Positions;
using Apex.Hrms.Application.Organization.Sections;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;
using Apex.Hrms.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.IntegrationTests;

/// <summary>Paging, sorting, search and filters, translated to SQL and evaluated by SQL Server.</summary>
[Collection(SqlServerFixtureDefinition.Name)]
[Trait("Category", "Integration")]
public sealed class QueryTests : DatabaseTest
{
    private static PageRequest<CompanyFilter> Companies(
        int pageIndex = 0,
        int pageSize = 10,
        string? sort = null,
        string direction = "asc",
        string? search = null,
        string? status = null) => new(
        pageIndex,
        pageSize,
        sort is null ? null : new SortState(sort, direction),
        search,
        new CompanyFilter(status));

    private async Task SeedCompaniesAsync(int count)
    {
        for (var i = 1; i <= count; i++)
        {
            Succeeded(await Create(Requests.Company($"C{i:00}", $"Test Company {i:00}")));
        }
    }

    // ---- Paging ----

    [IntegrationFact]
    public async Task Paging_returns_the_requested_page_and_the_total()
    {
        await SeedCompaniesAsync(25);

        var first = Succeeded(await Query<CompanyDto, CompanyFilter>(Companies(pageIndex: 0, pageSize: 10)));
        var last = Succeeded(await Query<CompanyDto, CompanyFilter>(Companies(pageIndex: 2, pageSize: 10)));

        Assert.Equal(25, first.Total);
        Assert.Equal(10, first.Items.Count);
        Assert.Equal(Code("C01"), first.Items[0].Code);
        Assert.Equal(Code("C10"), first.Items[^1].Code);
        Assert.Equal(5, last.Items.Count);
        Assert.Equal(2, last.PageIndex);
        Assert.Equal(Code("C25"), last.Items[^1].Code);
    }

    [IntegrationFact]
    public async Task A_page_index_past_the_end_is_clamped_to_the_last_page()
    {
        await SeedCompaniesAsync(25);

        var page = Succeeded(await Query<CompanyDto, CompanyFilter>(Companies(pageIndex: 99, pageSize: 10)));

        Assert.Equal(2, page.PageIndex);
        Assert.Equal(5, page.Items.Count);
    }

    [IntegrationFact]
    public async Task An_empty_table_gives_an_empty_first_page()
    {
        var page = Succeeded(await Query<CompanyDto, CompanyFilter>(Companies()));

        Assert.Equal(0, page.Total);
        Assert.Empty(page.Items);
        Assert.Equal(0, page.PageIndex);
    }

    [IntegrationFact]
    public async Task The_page_size_is_limited_to_the_maximum()
    {
        await SeedCompaniesAsync(3);

        var page = Succeeded(await Query<CompanyDto, CompanyFilter>(Companies(pageSize: 100_000)));

        Assert.Equal(Paging.MaxPageSize, page.PageSize);
        Assert.Equal(3, page.Items.Count);
    }

    [IntegrationFact]
    public async Task Pages_do_not_overlap_when_the_sort_column_has_ties()
    {
        for (var i = 1; i <= 7; i++)
        {
            Succeeded(await Create(Requests.Company($"C{i:00}", $"Test Company {i:00}", city: "Yangon")));
        }

        var seen = new List<int>();
        for (var pageIndex = 0; pageIndex < 3; pageIndex++)
        {
            var page = Succeeded(await Query<CompanyDto, CompanyFilter>(Companies(pageIndex, 3, "city")));
            seen.AddRange(page.Items.Select(company => company.Id));
        }

        Assert.Equal(7, seen.Distinct().Count());
    }

    // ---- Sorting ----

    [IntegrationFact]
    public async Task Sorting_by_name_follows_the_case_insensitive_collation_of_the_database()
    {
        Succeeded(await Create(Requests.Company("1", "bravo Test")));
        Succeeded(await Create(Requests.Company("2", "Alpha Test")));
        Succeeded(await Create(Requests.Company("3", "Charlie Test")));

        var ascending = Succeeded(await Query<CompanyDto, CompanyFilter>(Companies(sort: "name")));
        var descending = Succeeded(await Query<CompanyDto, CompanyFilter>(Companies(sort: "NAME", direction: "DESC")));

        Assert.Equal([Name("Alpha Test"), Name("bravo Test"), Name("Charlie Test")], ascending.Items.Select(c => c.Name));
        Assert.Equal([Name("Charlie Test"), Name("bravo Test"), Name("Alpha Test")], descending.Items.Select(c => c.Name));
    }

    [IntegrationFact]
    public async Task Sorting_by_status_orders_the_stored_status_text()
    {
        Succeeded(await Create(Requests.Company("1", "Test One", status: "Inactive")));
        Succeeded(await Create(Requests.Company("2", "Test Two", status: "Active")));

        var page = Succeeded(await Query<CompanyDto, CompanyFilter>(Companies(sort: "status")));

        Assert.Equal(["Active", "Inactive"], page.Items.Select(c => c.Status));
    }

    [IntegrationFact]
    public async Task An_unknown_sort_key_is_a_validation_error_on_sort()
    {
        var result = await Query<CompanyDto, CompanyFilter>(Companies(sort: "salary"));

        FailedOnField(result, "sort");
    }

    [IntegrationTheory]
    [InlineData("id")]
    [InlineData("code")]
    [InlineData("name")]
    [InlineData("legalName")]
    [InlineData("industry")]
    [InlineData("city")]
    [InlineData("country")]
    [InlineData("foundedDate")]
    [InlineData("status")]
    public async Task Every_company_sort_key_runs_in_both_directions(string key) =>
        await AssertSortsAsync<CompanyDto, CompanyFilter>(key, new CompanyFilter(), SeedTwoOfEachAsync, 2);

    [IntegrationTheory]
    [InlineData("id")]
    [InlineData("code")]
    [InlineData("name")]
    [InlineData("companyId")]
    [InlineData("type")]
    [InlineData("city")]
    [InlineData("country")]
    [InlineData("openedDate")]
    [InlineData("status")]
    public async Task Every_branch_sort_key_runs_in_both_directions(string key) =>
        await AssertSortsAsync<BranchDto, BranchFilter>(key, new BranchFilter(), SeedTwoOfEachAsync, 2);

    [IntegrationTheory]
    [InlineData("id")]
    [InlineData("code")]
    [InlineData("name")]
    [InlineData("companyId")]
    [InlineData("branchId")]
    [InlineData("costCenter")]
    [InlineData("status")]
    public async Task Every_department_sort_key_runs_in_both_directions(string key) =>
        await AssertSortsAsync<DepartmentDto, DepartmentFilter>(key, new DepartmentFilter(), SeedTwoOfEachAsync, 2);

    [IntegrationTheory]
    [InlineData("id")]
    [InlineData("code")]
    [InlineData("name")]
    [InlineData("departmentId")]
    [InlineData("status")]
    public async Task Every_section_sort_key_runs_in_both_directions(string key) =>
        await AssertSortsAsync<SectionDto, SectionFilter>(key, new SectionFilter(), SeedTwoOfEachAsync, 2);

    [IntegrationTheory]
    [InlineData("id")]
    [InlineData("code")]
    [InlineData("name")]
    [InlineData("level")]
    [InlineData("minSalary")]
    [InlineData("maxSalary")]
    [InlineData("status")]
    public async Task Every_job_grade_sort_key_runs_in_both_directions(string key) =>
        await AssertSortsAsync<JobGradeDto, JobGradeFilter>(key, new JobGradeFilter(), SeedTwoOfEachAsync, 2);

    [IntegrationTheory]
    [InlineData("id")]
    [InlineData("code")]
    [InlineData("title")]
    [InlineData("departmentId")]
    [InlineData("jobGradeId")]
    [InlineData("headcount")]
    [InlineData("status")]
    public async Task Every_position_sort_key_runs_in_both_directions(string key) =>
        await AssertSortsAsync<PositionDto, PositionFilter>(key, new PositionFilter(), SeedTwoOfEachAsync, 2);

    // ---- Search ----

    [IntegrationFact]
    public async Task Search_is_case_insensitive_and_looks_in_code_name_legal_name_and_city()
    {
        Succeeded(await Create(Requests.Company("ALP", "Alpha Test", city: "Yangon")));
        Succeeded(await Create(Requests.Company("BET", "Beta Test", city: "Mandalay")));

        Assert.Equal([Code("ALP")], await CodesAsync(search: "alpha"));
        Assert.Equal([Code("ALP")], await CodesAsync(search: "ALPHA"));
        Assert.Equal([Code("BET")], await CodesAsync(search: Code("BET").ToLowerInvariant()));
        Assert.Equal([Code("BET")], await CodesAsync(search: "mandalay"));
        Assert.Equal([Code("BET")], await CodesAsync(search: "Beta Test Ltd"));
        Assert.Equal([Code("ALP"), Code("BET")], await CodesAsync(search: "test"));
        Assert.Empty(await CodesAsync(search: "nothing like this"));
    }

    [IntegrationFact]
    public async Task Search_treats_percent_and_underscore_as_plain_characters()
    {
        Succeeded(await Create(Requests.Company("PCT", "Alpha 100% Test")));
        Succeeded(await Create(Requests.Company("UND", "Beta_Test")));
        Succeeded(await Create(Requests.Company("PLN", "Gamma Test")));

        Assert.Equal([Code("PCT")], await CodesAsync(search: "%"));
        Assert.Equal([Code("PCT")], await CodesAsync(search: "100%"));
        Assert.Equal([Code("UND")], await CodesAsync(search: "_"));
        Assert.Equal([Code("UND")], await CodesAsync(search: "a_T"));
        Assert.Empty(await CodesAsync(search: "100%x"));
    }

    [IntegrationFact]
    public async Task Search_treats_brackets_and_backslashes_as_plain_characters()
    {
        Succeeded(await Create(Requests.Company("BRK", "Delta [Z] Test")));
        Succeeded(await Create(Requests.Company("PLN", "Zeta Test")));

        Assert.Equal([Code("BRK")], await CodesAsync(search: "[Z]"));
        Assert.Empty(await CodesAsync(search: "[A-Z]"));
        Assert.Empty(await CodesAsync(search: "\\"));
    }

    [IntegrationFact]
    public async Task Search_with_a_quote_is_a_parameter_not_sql()
    {
        Succeeded(await Create(Requests.Company("QUO", "O'Brien Test")));

        Assert.Equal([Code("QUO")], await CodesAsync(search: "O'Brien"));
        Assert.Empty(await CodesAsync(search: "'; DROP TABLE org.Company; --"));
        Assert.Equal(1, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM org.Company"));
    }

    [IntegrationFact]
    public async Task A_blank_search_means_no_search_and_a_too_long_one_is_a_validation_error()
    {
        await SeedCompaniesAsync(3);

        Assert.Equal(3, (await CodesAsync(search: "   ")).Count);
        FailedOnField(
            await Query<CompanyDto, CompanyFilter>(Companies(search: new string('x', Paging.MaxSearchLength + 1))),
            "search");
    }

    [IntegrationFact]
    public async Task Search_and_paging_count_only_the_matching_rows()
    {
        await SeedCompaniesAsync(12);

        var page = Succeeded(await Query<CompanyDto, CompanyFilter>(Companies(pageSize: 5, search: "Company 1")));

        // Names are zero-padded ("Test Company 01".."12"), so "Company 1" matches only 10, 11 and 12.
        Assert.Equal(3, page.Total);
        Assert.Equal(3, page.Items.Count);
    }

    // ---- Filters ----

    [IntegrationFact]
    public async Task The_status_filter_keeps_matching_rows_and_all_or_blank_keeps_everything()
    {
        Succeeded(await Create(Requests.Company("1", "Test One", status: "Active")));
        Succeeded(await Create(Requests.Company("2", "Test Two", status: "Inactive")));

        Assert.Equal([Code("1")], await CodesAsync(status: "Active"));
        // Status values are exact and case-sensitive by design, so a wrong-case value matches nothing.
        Assert.Empty(await CodesAsync(status: "inactive"));
        Assert.Equal(2, (await CodesAsync(status: "all")).Count);
        Assert.Equal(2, (await CodesAsync(status: "")).Count);
        Assert.Empty(await CodesAsync(status: "Retired"));
    }

    [IntegrationFact]
    public async Task Branch_filters_by_company_type_and_status_combine()
    {
        var companyA = await SeedCompanyAsync("A", "Test Company A");
        var companyB = await SeedCompanyAsync("B", "Test Company B");
        Succeeded(await Create(Requests.Branch(companyA.Id, "A-HO", "Head Test Office", type: "Head Office")));
        Succeeded(await Create(Requests.Branch(companyA.Id, "A-BR", "Branch Test Office")));
        Succeeded(await Create(Requests.Branch(companyA.Id, "A-OLD", "Closed Test Office", status: "Inactive")));
        Succeeded(await Create(Requests.Branch(companyB.Id, "B-BR", "Other Test Office")));

        Assert.Equal([Code("A-HO"), Code("A-BR"), Code("A-OLD")], await BranchCodesAsync(new BranchFilter(CompanyId: companyA.Id)));
        Assert.Equal([Code("A-HO")], await BranchCodesAsync(new BranchFilter(Type: "Head Office")));
        Assert.Equal([Code("A-BR"), Code("B-BR")], await BranchCodesAsync(new BranchFilter(Type: "Branch", Status: "Active")));
        Assert.Equal([Code("A-BR")], await BranchCodesAsync(new BranchFilter(companyA.Id, "Branch", "Active")));
        Assert.Empty(await BranchCodesAsync(new BranchFilter(Type: "Spaceport")));
    }

    [IntegrationFact]
    public async Task Department_section_and_position_filters_follow_their_parents()
    {
        var company = await SeedCompanyAsync();
        var north = await SeedBranchAsync(company.Id, "A-N", "North Test Office");
        var south = await SeedBranchAsync(company.Id, "A-S", "South Test Office");
        var engineering = await SeedDepartmentAsync(company.Id, north.Id, "A-ENG", "Engineering");
        var finance = await SeedDepartmentAsync(company.Id, south.Id, "A-FIN", "Finance");
        await SeedSectionAsync(engineering.Id, "A-BE", "Backend");
        await SeedSectionAsync(finance.Id, "A-ACC", "Accounting");
        var junior = await SeedJobGradeAsync("G1", "Junior");
        var senior = await SeedJobGradeAsync("G2", "Senior");
        await SeedPositionAsync(engineering.Id, junior.Id, "A-DEV");
        await SeedPositionAsync(finance.Id, senior.Id, "A-CFO");

        var departments = await Query<DepartmentDto, DepartmentFilter>(new PageRequest<DepartmentFilter>(0, 10, null, null, new DepartmentFilter(BranchId: south.Id)));
        var sections = await Query<SectionDto, SectionFilter>(new PageRequest<SectionFilter>(0, 10, null, null, new SectionFilter(DepartmentId: engineering.Id)));
        var byDepartment = await Query<PositionDto, PositionFilter>(new PageRequest<PositionFilter>(0, 10, null, null, new PositionFilter(DepartmentId: finance.Id)));
        var byGrade = await Query<PositionDto, PositionFilter>(new PageRequest<PositionFilter>(0, 10, null, null, new PositionFilter(JobGradeId: junior.Id)));

        Assert.Equal([Code("A-FIN")], Succeeded(departments).Items.Select(d => d.Code));
        Assert.Equal([Code("A-BE")], Succeeded(sections).Items.Select(s => s.Code));
        Assert.Equal([Code("A-CFO")], Succeeded(byDepartment).Items.Select(p => p.Code));
        Assert.Equal([Code("A-DEV")], Succeeded(byGrade).Items.Select(p => p.Code));
    }

    // ---- Access scope ----

    [IntegrationFact]
    public async Task The_default_access_scope_translates_to_sql_for_every_resource()
    {
        await SeedTwoOfEachAsync();

        await InScopeAsync(async services =>
        {
            var user = services.GetRequiredService<ICurrentUser>();
            Assert.Equal(2, (await services.GetRequiredService<IRepository<Company>>().ListAsync(services.GetRequiredService<IAccessScope<Company>>().PredicateFor(user), default)).Count);
            Assert.Equal(2, (await services.GetRequiredService<IRepository<Branch>>().ListAsync(services.GetRequiredService<IAccessScope<Branch>>().PredicateFor(user), default)).Count);
            Assert.Equal(2, (await services.GetRequiredService<IRepository<Department>>().ListAsync(services.GetRequiredService<IAccessScope<Department>>().PredicateFor(user), default)).Count);
            Assert.Equal(2, (await services.GetRequiredService<IRepository<Section>>().ListAsync(services.GetRequiredService<IAccessScope<Section>>().PredicateFor(user), default)).Count);
            Assert.Equal(2, (await services.GetRequiredService<IRepository<JobGrade>>().ListAsync(services.GetRequiredService<IAccessScope<JobGrade>>().PredicateFor(user), default)).Count);
            Assert.Equal(2, (await services.GetRequiredService<IRepository<Position>>().ListAsync(services.GetRequiredService<IAccessScope<Position>>().PredicateFor(user), default)).Count);
            return true;
        });
    }

    [IntegrationFact]
    public async Task The_default_access_scope_combines_with_filter_and_search_in_one_query()
    {
        await SeedCompaniesAsync(4);

        var page = Succeeded(await Query<CompanyDto, CompanyFilter>(Companies(search: "company 0", status: "Active", sort: "code", direction: "desc")));

        Assert.Equal(4, page.Total);
        Assert.Equal(Code("C04"), page.Items[0].Code);
    }

    [IntegrationFact]
    public async Task A_narrowed_access_scope_is_applied_in_sql_and_hides_records_like_missing_ones()
    {
        var visiblePrefix = Code("A");
        Customize(services => services.AddScoped<IAccessScope<Company>>(_ => new CompaniesStartingWith(visiblePrefix)));
        var visible = await SeedCompanyAsync("A", "Test Company A");
        var hidden = await SeedCompanyAsync("B", "Test Company B");

        Assert.Equal([Code("A")], await CodesAsync());
        Assert.Equal(visible.Id, Succeeded(await Get<CompanyDto>(visible.Id)).Id);
        Failed(await Get<CompanyDto>(hidden.Id), ErrorType.NotFound);
        Failed(await Update(hidden.Id, Requests.CompanyUpdate(hidden, city: "Bago")), ErrorType.NotFound);
        Failed(await Delete<CompanyDto>(hidden.Id, Requests.Version(hidden.RowVersion)), ErrorType.NotFound);
        Failed(await Deactivate<CompanyDto>(hidden.Id, Requests.Version(hidden.RowVersion)), ErrorType.NotFound);
        Assert.Equal(1, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM org.Company WHERE Id = @id", ("@id", hidden.Id)));
    }

    // ---- Helpers ----

    private async Task<IReadOnlyList<string>> CodesAsync(string? search = null, string? status = null) =>
        [.. Succeeded(await Query<CompanyDto, CompanyFilter>(Companies(search: search, status: status))).Items.Select(c => c.Code)];

    private async Task<IReadOnlyList<string>> BranchCodesAsync(BranchFilter filter) =>
        [.. Succeeded(await Query<BranchDto, BranchFilter>(new PageRequest<BranchFilter>(0, 50, null, null, filter))).Items.Select(b => b.Code)];

    /// <summary>Two rows of every resource, so each sort key has something to order.</summary>
    private async Task SeedTwoOfEachAsync()
    {
        var first = await SeedCompanyAsync("A", "Test Company A");
        var second = await SeedCompanyAsync("B", "Test Company B");
        var branchA = await SeedBranchAsync(first.Id, "A-YGN", "Yangon Test Office");
        var branchB = await SeedBranchAsync(second.Id, "B-YGN", "Yangon Second Office");
        var departmentA = await SeedDepartmentAsync(first.Id, branchA.Id, "A-ENG", "Engineering");
        var departmentB = await SeedDepartmentAsync(second.Id, branchB.Id, "B-ENG", "Engineering B");
        await SeedSectionAsync(departmentA.Id, "A-BE", "Backend");
        await SeedSectionAsync(departmentB.Id, "B-BE", "Backend B");
        var gradeA = await SeedJobGradeAsync("G1", "Junior");
        var gradeB = await SeedJobGradeAsync("G2", "Senior");
        await SeedPositionAsync(departmentA.Id, gradeA.Id, "A-DEV");
        await SeedPositionAsync(departmentB.Id, gradeB.Id, "B-DEV");
    }

    private async Task AssertSortsAsync<TDto, TFilter>(string key, TFilter filter, Func<Task> seed, int expected)
    {
        await seed();
        foreach (var direction in new[] { "asc", "desc" })
        {
            var page = Succeeded(await Query<TDto, TFilter>(new PageRequest<TFilter>(0, 10, new SortState(key, direction), null, filter)));
            Assert.Equal(expected, page.Total);
            Assert.Equal(expected, page.Items.Count);
        }
    }

    private sealed class CompaniesStartingWith(string prefix) : IAccessScope<Company>
    {
        public System.Linq.Expressions.Expression<Func<Company, bool>> PredicateFor(ICurrentUser user) =>
            company => company.Code.StartsWith(prefix);
    }
}
