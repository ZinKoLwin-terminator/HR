using Apex.Hrms.Application.Organization;
using Apex.Hrms.Application.Organization.Branches;
using Apex.Hrms.Application.Organization.Departments;
using Apex.Hrms.Application.Organization.JobGrades;
using Apex.Hrms.Application.Organization.Positions;
using Apex.Hrms.Application.Organization.Sections;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Application.UseCases;

namespace Apex.Hrms.Application.Tests.Organization;

/// <summary>
/// The organization lookup: every record, active and inactive, each list by name (positions by title) then id, reduced to the
/// contract fields (no employee ids, contact, legal, tax or cost-centre data, salary bands or row versions).
/// </summary>
public sealed class OrganizationLookupTests : IDisposable
{
    private readonly TestHost host = new();

    public void Dispose() => host.Dispose();

    private IOrganizationLookupHandler Lookup => host.Get<IOrganizationLookupHandler>();

    private async Task<TDto> CreateAsync<TRequest, TDto>(TRequest request) =>
        ResultAssert.Succeeded(await host.Get<ICreateHandler<TRequest, TDto>>().HandleAsync(request, default));

    private static string[] PropertiesOf<T>() => [.. typeof(T).GetProperties().Select(property => property.Name)];

    [Fact]
    public async Task An_empty_organization_gives_six_empty_lists()
    {
        var lookup = ResultAssert.Succeeded(await Lookup.HandleAsync(default));

        Assert.Empty(lookup.Companies);
        Assert.Empty(lookup.Branches);
        Assert.Empty(lookup.Departments);
        Assert.Empty(lookup.Sections);
        Assert.Empty(lookup.Positions);
        Assert.Empty(lookup.JobGrades);
    }

    [Fact]
    public async Task Every_record_is_listed_with_inactive_ones_by_name_then_id()
    {
        var charlie = await host.CreateCompanyAsync("CHA", "Charlie Co");
        var alpha = await host.CreateCompanyAsync("ALP", "Alpha Co");
        var bravo = await host.CreateCompanyAsync("BRA", "Bravo Co", status: "Inactive");

        var yangon = await host.CreateBranchAsync(alpha.Id);
        var mandalay = await CreateAsync<BranchCreateRequest, BranchDto>(Requests.Branch(alpha.Id, "MDY", "Inactive") with { Name = "Mandalay Office" });

        var engineering = await host.CreateDepartmentAsync(alpha.Id, yangon.Id);
        var finance = await CreateAsync<DepartmentCreateRequest, DepartmentDto>(Requests.Department(alpha.Id, yangon.Id, "FIN", "Inactive") with { Name = "Finance" });
        var engineeringTwo = await host.CreateDepartmentAsync(alpha.Id, yangon.Id, "ENX");

        var backend = await host.CreateSectionAsync(engineering.Id);
        var analytics = await CreateAsync<SectionCreateRequest, SectionDto>(Requests.Section(engineering.Id, "AN", "Inactive") with { Name = "Analytics" });

        var junior = await host.CreateJobGradeAsync();
        var senior = await CreateAsync<JobGradeCreateRequest, JobGradeDto>(Requests.JobGrade("G3", "Inactive") with { Name = "Senior", Level = 3 });

        var developer = await host.CreatePositionAsync(engineering.Id, junior.Id, sectionId: backend.Id);
        var analyst = await CreateAsync<PositionCreateRequest, PositionDto>(
            Requests.Position(engineering.Id, junior.Id, "ANL") with { Title = "Analyst", Status = "Inactive" });

        var lookup = ResultAssert.Succeeded(await Lookup.HandleAsync(default));

        Assert.Equal(
            [
                new CompanyLookupDto(alpha.Id, "ALP", "Alpha Co", "Active"),
                new CompanyLookupDto(bravo.Id, "BRA", "Bravo Co", "Inactive"),
                new CompanyLookupDto(charlie.Id, "CHA", "Charlie Co", "Active"),
            ],
            lookup.Companies);
        Assert.Equal(
            [
                new BranchLookupDto(mandalay.Id, alpha.Id, "MDY", "Mandalay Office", "Inactive"),
                new BranchLookupDto(yangon.Id, alpha.Id, "YGN", "Yangon Office", "Active"),
            ],
            lookup.Branches);
        Assert.Equal(
            [
                new DepartmentLookupDto(engineering.Id, alpha.Id, yangon.Id, "ENG", "Engineering", "Active"),
                new DepartmentLookupDto(engineeringTwo.Id, alpha.Id, yangon.Id, "ENX", "Engineering", "Active"),
                new DepartmentLookupDto(finance.Id, alpha.Id, yangon.Id, "FIN", "Finance", "Inactive"),
            ],
            lookup.Departments);
        Assert.Equal(
            [
                new SectionLookupDto(analytics.Id, engineering.Id, "AN", "Analytics", "Inactive"),
                new SectionLookupDto(backend.Id, engineering.Id, "BE", "Backend", "Active"),
            ],
            lookup.Sections);
        Assert.Equal(
            [
                new PositionLookupDto(analyst.Id, engineering.Id, null, junior.Id, "ANL", "Analyst", "Inactive"),
                new PositionLookupDto(developer.Id, engineering.Id, backend.Id, junior.Id, "DEV", "Developer", "Active"),
            ],
            lookup.Positions);
        Assert.Equal(
            [
                new JobGradeLookupDto(junior.Id, "G1", "Junior", 1, "Active"),
                new JobGradeLookupDto(senior.Id, "G3", "Senior", 3, "Inactive"),
            ],
            lookup.JobGrades);
        Assert.True(engineering.Id < engineeringTwo.Id);
    }

    [Fact]
    public void The_lookup_rows_carry_only_the_contract_fields()
    {
        Assert.Equal(["Companies", "Branches", "Departments", "Sections", "Positions", "JobGrades"], PropertiesOf<OrganizationLookupDto>());
        Assert.Equal(["Id", "Code", "Name", "Status"], PropertiesOf<CompanyLookupDto>());
        Assert.Equal(["Id", "CompanyId", "Code", "Name", "Status"], PropertiesOf<BranchLookupDto>());
        Assert.Equal(["Id", "CompanyId", "BranchId", "Code", "Name", "Status"], PropertiesOf<DepartmentLookupDto>());
        Assert.Equal(["Id", "DepartmentId", "Code", "Name", "Status"], PropertiesOf<SectionLookupDto>());
        Assert.Equal(["Id", "DepartmentId", "SectionId", "JobGradeId", "Code", "Title", "Status"], PropertiesOf<PositionLookupDto>());
        Assert.Equal(["Id", "Code", "Name", "Level", "Status"], PropertiesOf<JobGradeLookupDto>());
    }
}
