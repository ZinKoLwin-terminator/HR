using Apex.Hrms.Application.Organization.Companies;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Tests.Organization;

public sealed class CompanyHandlerTests : IDisposable
{
    private readonly TestHost host = new();

    public void Dispose() => host.Dispose();

    private ICreateHandler<CompanyCreateRequest, CompanyDto> Create => host.Get<ICreateHandler<CompanyCreateRequest, CompanyDto>>();

    private IUpdateHandler<CompanyUpdateRequest, CompanyDto> Update => host.Get<IUpdateHandler<CompanyUpdateRequest, CompanyDto>>();

    private IDeleteHandler<CompanyDto> Delete => host.Get<IDeleteHandler<CompanyDto>>();

    [Fact]
    public async Task Create_saves_the_company_and_returns_it_with_empty_text_for_missing_optionals_and_a_row_version()
    {
        var dto = ResultAssert.Succeeded(await Create.HandleAsync(Requests.Company(), default));

        Assert.Equal(1, dto.Id);
        Assert.Equal("AHT", dto.Code);
        Assert.Equal("Active", dto.Status);
        Assert.Equal(string.Empty, dto.Website);
        Assert.Equal(8, dto.RowVersion.Length);
        Assert.Equal(1, host.UnitOfWork.SaveCount);
        Assert.Single(host.Repository<Company>().Items);
    }

    [Fact]
    public async Task Create_uses_the_status_of_the_request()
    {
        var dto = ResultAssert.Succeeded(await Create.HandleAsync(Requests.Company(status: "Inactive"), default));

        Assert.Equal("Inactive", dto.Status);
    }

    [Fact]
    public async Task Create_trims_text_before_the_rules_run_so_a_padded_duplicate_is_still_a_duplicate()
    {
        await host.CreateCompanyAsync(code: "AHT", name: "First");

        var result = await Create.HandleAsync(Requests.Company(code: "  AHT  ", name: "Second"), default);

        ResultAssert.FailedOnField(result, "code", "Company code \"AHT\" already exists.");
    }

    [Fact]
    public async Task Create_rejects_a_duplicate_code_with_the_mock_wording()
    {
        await host.CreateCompanyAsync(code: "AHT", name: "First");

        var result = await Create.HandleAsync(Requests.Company(code: "AHT", name: "Second"), default);

        var error = ResultAssert.FailedOnField(result, "code", "Company code \"AHT\" already exists.");
        Assert.Equal(["Company code must be unique."], error.FieldErrors!["code"]);
        Assert.Single(host.Repository<Company>().Items);
    }

    [Fact]
    public async Task Create_rejects_a_duplicate_name()
    {
        await host.CreateCompanyAsync(code: "AHT", name: "Apex Holdings");

        var result = await Create.HandleAsync(Requests.Company(code: "XYZ", name: "Apex Holdings"), default);

        ResultAssert.FailedOnField(result, "name", "Company name \"Apex Holdings\" already exists.");
    }

    [Fact]
    public async Task Create_reports_invalid_input_per_field_without_saving()
    {
        var request = Requests.Company(code: "bad code", status: "Pending") with { Name = "", Email = "not-an-email", Phone = "12" };

        var result = await Create.HandleAsync(request, default);

        var error = ResultAssert.Failed(result, ErrorType.Validation);
        Assert.Equal(["code", "email", "name", "phone", "status"], error.FieldErrors!.Keys.Order());
        Assert.Equal(0, host.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task Validators_measure_length_after_trimming_and_name_fields_like_the_domain()
    {
        var padded = Requests.Company() with { Name = "  " + new string('A', 100) + "  ", LegalName = new string('L', 151) };

        var error = ResultAssert.Failed(await Create.HandleAsync(padded, default), ErrorType.Validation);

        Assert.DoesNotContain("name", error.FieldErrors!.Keys);
        Assert.Equal(["Legal name must be at most 150 characters."], error.FieldErrors["legalName"]);
    }

    [Fact]
    public async Task A_blank_name_uses_the_domain_label_in_the_message()
    {
        var error = ResultAssert.Failed(await Create.HandleAsync(Requests.Company() with { Name = "   " }, default), ErrorType.Validation);

        Assert.Equal(["Company name is required."], error.FieldErrors!["name"]);
    }

    [Fact]
    public async Task Create_rejects_a_founded_date_after_today()
    {
        var request = Requests.Company() with { FoundedDate = host.Clock.Today.AddDays(1) };

        ResultAssert.FailedOnField(await Create.HandleAsync(request, default), "foundedDate");
    }

    [Fact]
    public async Task Create_returns_the_conflict_when_the_database_rejects_the_save()
    {
        host.UnitOfWork.FailNextSaveWith = AppError.Conflict("A record with the same unique value already exists.");

        var result = await Create.HandleAsync(Requests.Company(), default);

        ResultAssert.Failed(result, ErrorType.Conflict);
    }

    [Fact]
    public async Task Update_changes_the_company_and_may_keep_its_own_code_and_name()
    {
        var company = await host.CreateCompanyAsync();

        var result = await Update.HandleAsync(company.Id, Requests.CompanyUpdate(company.RowVersion) with { City = "Mandalay" }, default);

        var updated = ResultAssert.Succeeded(result);
        Assert.Equal("Mandalay", updated.City);
        Assert.NotEqual(company.RowVersion, updated.RowVersion);
    }

    [Fact]
    public async Task Update_does_not_change_the_status()
    {
        var company = await host.CreateCompanyAsync(status: "Inactive");

        var updated = ResultAssert.Succeeded(await Update.HandleAsync(company.Id, Requests.CompanyUpdate(company.RowVersion), default));

        Assert.Equal("Inactive", updated.Status);
    }

    [Fact]
    public async Task Update_rejects_a_code_used_by_another_company()
    {
        await host.CreateCompanyAsync(code: "AHT", name: "First");
        var second = await host.CreateCompanyAsync(code: "XYZ", name: "Second");

        var result = await Update.HandleAsync(second.Id, Requests.CompanyUpdate(second.RowVersion, code: "AHT", name: "Second"), default);

        ResultAssert.FailedOnField(result, "code");
    }

    [Fact]
    public async Task Update_of_a_missing_company_is_not_found_with_the_mock_wording()
    {
        var result = await Update.HandleAsync(42, Requests.CompanyUpdate([1, 2, 3, 4, 5, 6, 7, 8]), default);

        ResultAssert.Failed(result, ErrorType.NotFound, "Company #42 was not found.");
    }

    [Fact]
    public async Task Update_requires_the_row_version()
    {
        var company = await host.CreateCompanyAsync();

        var result = await Update.HandleAsync(company.Id, Requests.CompanyUpdate(company.RowVersion) with { RowVersion = null }, default);

        ResultAssert.FailedOnField(result, "rowVersion");
        Assert.Equal(1, host.UnitOfWork.SaveCount);
    }

    [Theory]
    [InlineData("not base64!")]
    [InlineData("AQID")]
    public async Task Update_rejects_a_malformed_row_version(string rowVersion)
    {
        var company = await host.CreateCompanyAsync();

        var result = await Update.HandleAsync(company.Id, Requests.CompanyUpdate(company.RowVersion) with { RowVersion = rowVersion }, default);

        ResultAssert.FailedOnField(result, "rowVersion");
    }

    [Fact]
    public async Task Update_with_a_stale_row_version_is_a_conflict_and_changes_nothing()
    {
        var company = await host.CreateCompanyAsync();
        ResultAssert.Succeeded(await Update.HandleAsync(company.Id, Requests.CompanyUpdate(company.RowVersion) with { City = "Mandalay" }, default));

        var stale = await Update.HandleAsync(company.Id, Requests.CompanyUpdate(company.RowVersion) with { City = "Bago" }, default);

        ResultAssert.Failed(stale, ErrorType.Conflict, RowVersionToken.Stale.Message);
        Assert.Equal("Mandalay", host.Repository<Company>().Items.Single().City);
    }

    [Fact]
    public async Task Update_passes_the_row_version_to_the_repository_for_the_database_check()
    {
        var company = await host.CreateCompanyAsync();

        await Update.HandleAsync(company.Id, Requests.CompanyUpdate(company.RowVersion), default);

        Assert.Equal(company.RowVersion, host.Repository<Company>().ExpectedRowVersions.Values.Single());
    }

    [Fact]
    public async Task Update_surfaces_a_concurrency_conflict_raised_by_the_database()
    {
        var company = await host.CreateCompanyAsync();
        host.UnitOfWork.FailNextSaveWith = RowVersionToken.Stale;

        ResultAssert.Failed(await Update.HandleAsync(company.Id, Requests.CompanyUpdate(company.RowVersion), default), ErrorType.Conflict);
    }

    [Fact]
    public async Task Delete_removes_an_unreferenced_company()
    {
        var company = await host.CreateCompanyAsync();

        Assert.True((await Delete.HandleAsync(company.Id, Requests.Version(company.RowVersion), default)).IsSuccess);
        Assert.Empty(host.Repository<Company>().Items);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("%%%")]
    public async Task Delete_requires_a_valid_row_version(string? rowVersion)
    {
        var company = await host.CreateCompanyAsync();

        var result = await Delete.HandleAsync(company.Id, rowVersion, default);

        ResultAssert.FailedOnField(result, "rowVersion");
        Assert.Single(host.Repository<Company>().Items);
    }

    [Fact]
    public async Task Delete_with_a_stale_row_version_is_a_conflict()
    {
        var company = await host.CreateCompanyAsync();
        ResultAssert.Succeeded(await Update.HandleAsync(company.Id, Requests.CompanyUpdate(company.RowVersion), default));

        var result = await Delete.HandleAsync(company.Id, Requests.Version(company.RowVersion), default);

        ResultAssert.Failed(result, ErrorType.Conflict);
        Assert.Single(host.Repository<Company>().Items);
    }

    [Fact]
    public async Task Delete_is_blocked_while_branches_departments_or_employees_use_the_company()
    {
        var company = await host.CreateCompanyAsync();
        await host.CreateBranchAsync(company.Id, "B1");
        await host.CreateBranchAsync(company.Id, "B2");
        host.Employees.Add(companyId: company.Id);
        host.Employees.Add(companyId: company.Id);

        var result = await Delete.HandleAsync(company.Id, Requests.Version(company.RowVersion), default);

        ResultAssert.Failed(
            result,
            ErrorType.Conflict,
            "\"Apex Holdings\" cannot be deleted because it is used by 2 branch(es), 2 employee(s). Deactivate it instead.");
        Assert.Single(host.Repository<Company>().Items);
    }

    [Fact]
    public async Task Delete_of_a_missing_company_is_not_found() =>
        ResultAssert.Failed(await Delete.HandleAsync(9, Requests.Version([1, 2, 3, 4, 5, 6, 7, 8]), default), ErrorType.NotFound, "Company #9 was not found.");

    [Fact]
    public async Task GetById_returns_the_company_or_not_found()
    {
        var company = await host.CreateCompanyAsync();
        var byId = host.Get<IGetByIdHandler<CompanyDto>>();

        Assert.Equal("AHT", ResultAssert.Succeeded(await byId.HandleAsync(company.Id, default)).Code);
        ResultAssert.Failed(await byId.HandleAsync(77, default), ErrorType.NotFound, "Company #77 was not found.");
    }

    [Fact]
    public async Task GetAll_returns_every_company()
    {
        await host.CreateCompanyAsync(code: "A1", name: "One");
        await host.CreateCompanyAsync(code: "B2", name: "Two");

        var all = ResultAssert.Succeeded(await host.Get<IGetAllHandler<CompanyDto>>().HandleAsync(default));

        Assert.Equal(["A1", "B2"], all.Select(c => c.Code));
    }
}
