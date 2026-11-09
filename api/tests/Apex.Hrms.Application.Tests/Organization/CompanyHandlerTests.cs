using Apex.Hrms.Application.Abstractions;
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

    private ICreateHandler<CompanyRequest, CompanyDto> Create => host.Get<ICreateHandler<CompanyRequest, CompanyDto>>();

    private IUpdateHandler<CompanyRequest, CompanyDto> Update => host.Get<IUpdateHandler<CompanyRequest, CompanyDto>>();

    private IDeleteHandler<CompanyDto> Delete => host.Get<IDeleteHandler<CompanyDto>>();

    [Fact]
    public async Task Create_saves_the_company_and_returns_it_with_empty_text_for_missing_optionals()
    {
        var dto = ResultAssert.Succeeded(await Create.HandleAsync(Requests.Company(), default));

        Assert.Equal(1, dto.Id);
        Assert.Equal("AHT", dto.Code);
        Assert.Equal("Active", dto.Status);
        Assert.Equal(string.Empty, dto.Website);
        Assert.Equal(1, host.UnitOfWork.SaveCount);
        Assert.Single(host.Repository<Company>().Items);
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

        var result = await Update.HandleAsync(company.Id, Requests.Company() with { City = "Mandalay" }, default);

        Assert.Equal("Mandalay", ResultAssert.Succeeded(result).City);
    }

    [Fact]
    public async Task Update_rejects_a_code_used_by_another_company()
    {
        await host.CreateCompanyAsync(code: "AHT", name: "First");
        var second = await host.CreateCompanyAsync(code: "XYZ", name: "Second");

        var result = await Update.HandleAsync(second.Id, Requests.Company(code: "AHT", name: "Second"), default);

        ResultAssert.FailedOnField(result, "code");
    }

    [Fact]
    public async Task Update_of_a_missing_company_is_not_found_with_the_mock_wording()
    {
        var result = await Update.HandleAsync(42, Requests.Company(), default);

        ResultAssert.Failed(result, ErrorType.NotFound, "Company #42 was not found.");
    }

    [Fact]
    public async Task Update_passes_the_client_row_version_to_the_repository()
    {
        var company = await host.CreateCompanyAsync();
        byte[] version = [1, 2, 3, 4, 5, 6, 7, 8];

        await Update.HandleAsync(company.Id, Requests.Company() with { RowVersion = version }, default);

        Assert.Equal(version, host.Repository<Company>().ExpectedRowVersions.Values.Single());
    }

    [Fact]
    public async Task Update_without_a_row_version_does_not_check_concurrency()
    {
        var company = await host.CreateCompanyAsync();

        await Update.HandleAsync(company.Id, Requests.Company(), default);

        Assert.Empty(host.Repository<Company>().ExpectedRowVersions);
    }

    [Fact]
    public async Task Update_surfaces_a_concurrency_conflict()
    {
        var company = await host.CreateCompanyAsync();
        host.UnitOfWork.FailNextSaveWith = AppError.Conflict("The record was changed or deleted by someone else.");

        ResultAssert.Failed(await Update.HandleAsync(company.Id, Requests.Company(), default), ErrorType.Conflict);
    }

    [Fact]
    public async Task Delete_removes_an_unreferenced_company()
    {
        var company = await host.CreateCompanyAsync();

        Assert.True((await Delete.HandleAsync(company.Id, default)).IsSuccess);
        Assert.Empty(host.Repository<Company>().Items);
    }

    [Fact]
    public async Task Delete_is_blocked_while_branches_departments_or_employees_use_the_company()
    {
        var company = await host.CreateCompanyAsync();
        host.References.Set(ReferenceQuery.BranchesOfCompany, company.Id, 2);
        host.References.Set(ReferenceQuery.EmployeesOfCompany, company.Id, 5);

        var result = await Delete.HandleAsync(company.Id, default);

        ResultAssert.Failed(
            result,
            ErrorType.Conflict,
            "\"Apex Holdings\" cannot be deleted because it is used by 2 branch(es), 5 employee(s). Deactivate it instead.");
        Assert.Single(host.Repository<Company>().Items);
    }

    [Fact]
    public async Task Delete_of_a_missing_company_is_not_found() =>
        ResultAssert.Failed(await Delete.HandleAsync(9, default), ErrorType.NotFound, "Company #9 was not found.");

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
