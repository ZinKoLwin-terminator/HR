using System.Linq.Expressions;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Organization.Branches;
using Apex.Hrms.Application.Organization.Companies;
using Apex.Hrms.Application.Organization.Departments;
using Apex.Hrms.Application.Organization.JobGrades;
using Apex.Hrms.Application.Organization.Positions;
using Apex.Hrms.Application.Organization.Sections;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.Application.Tests.Organization;

/// <summary>Activate and deactivate commands, and the access scope that every use case applies.</summary>
public sealed class CommandAndScopeTests : IDisposable
{
    private readonly List<IDisposable> hosts = [];

    public void Dispose() => hosts.ForEach(host => host.Dispose());

    private TestHost NewHost(Action<IServiceCollection>? configure = null)
    {
        var host = new TestHost(configure);
        hosts.Add(host);
        return host;
    }

    // ---- Commands ----

    [Fact]
    public async Task Company_can_be_deactivated_and_activated_again()
    {
        var host = NewHost();
        var company = await host.CreateCompanyAsync();

        await AssertTogglesAsync<CompanyDto>(host, company.Id, company.RowVersion, dto => dto.Status, dto => dto.RowVersion);
    }

    [Fact]
    public async Task Branch_can_be_deactivated_and_activated_again()
    {
        var host = NewHost();
        var company = await host.CreateCompanyAsync();
        var branch = await host.CreateBranchAsync(company.Id);

        await AssertTogglesAsync<BranchDto>(host, branch.Id, branch.RowVersion, dto => dto.Status, dto => dto.RowVersion);
    }

    [Fact]
    public async Task Department_can_be_deactivated_and_activated_again()
    {
        var host = NewHost();
        var company = await host.CreateCompanyAsync();
        var branch = await host.CreateBranchAsync(company.Id);
        var department = await host.CreateDepartmentAsync(company.Id, branch.Id);

        await AssertTogglesAsync<DepartmentDto>(host, department.Id, department.RowVersion, dto => dto.Status, dto => dto.RowVersion);
    }

    [Fact]
    public async Task Section_can_be_deactivated_and_activated_again()
    {
        var host = NewHost();
        var company = await host.CreateCompanyAsync();
        var branch = await host.CreateBranchAsync(company.Id);
        var department = await host.CreateDepartmentAsync(company.Id, branch.Id);
        var section = await host.CreateSectionAsync(department.Id);

        await AssertTogglesAsync<SectionDto>(host, section.Id, section.RowVersion, dto => dto.Status, dto => dto.RowVersion);
    }

    [Fact]
    public async Task Job_grade_can_be_deactivated_and_activated_again()
    {
        var host = NewHost();
        var grade = await host.CreateJobGradeAsync();

        await AssertTogglesAsync<JobGradeDto>(host, grade.Id, grade.RowVersion, dto => dto.Status, dto => dto.RowVersion);
    }

    [Fact]
    public async Task Position_can_be_deactivated_and_activated_again()
    {
        var host = NewHost();
        var company = await host.CreateCompanyAsync();
        var branch = await host.CreateBranchAsync(company.Id);
        var department = await host.CreateDepartmentAsync(company.Id, branch.Id);
        var grade = await host.CreateJobGradeAsync();
        var position = await host.CreatePositionAsync(department.Id, grade.Id);

        await AssertTogglesAsync<PositionDto>(host, position.Id, position.RowVersion, dto => dto.Status, dto => dto.RowVersion);
    }

    [Fact]
    public async Task A_command_with_a_stale_row_version_is_a_conflict_and_changes_nothing()
    {
        var host = NewHost();
        var company = await host.CreateCompanyAsync();
        var deactivate = host.Get<ICommandHandler<DeactivateCommand, CompanyDto>>();
        ResultAssert.Succeeded(await deactivate.HandleAsync(new DeactivateCommand(company.Id, Requests.Version(company.RowVersion)), default));

        var stale = await host.Get<ICommandHandler<ActivateCommand, CompanyDto>>()
            .HandleAsync(new ActivateCommand(company.Id, Requests.Version(company.RowVersion)), default);

        ResultAssert.Failed(stale, ErrorType.Conflict, RowVersionToken.Stale.Message);
        Assert.Equal(RecordStatus.Inactive, host.Repository<Company>().Items.Single().Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not base64!")]
    public async Task A_command_requires_a_valid_row_version(string? rowVersion)
    {
        var host = NewHost();
        var company = await host.CreateCompanyAsync();

        var result = await host.Get<ICommandHandler<DeactivateCommand, CompanyDto>>()
            .HandleAsync(new DeactivateCommand(company.Id, rowVersion), default);

        ResultAssert.FailedOnField(result, "rowVersion");
        Assert.True(host.Repository<Company>().Items.Single().IsActive);
    }

    [Fact]
    public async Task A_command_on_a_missing_record_is_not_found()
    {
        var host = NewHost();

        var result = await host.Get<ICommandHandler<ActivateCommand, BranchDto>>()
            .HandleAsync(new ActivateCommand(12, Requests.Version([1, 2, 3, 4, 5, 6, 7, 8])), default);

        ResultAssert.Failed(result, ErrorType.NotFound, "Branch #12 was not found.");
    }

    [Fact]
    public async Task A_command_surfaces_a_conflict_raised_by_the_database_save()
    {
        var host = NewHost();
        var company = await host.CreateCompanyAsync();
        host.UnitOfWork.FailNextSaveWith = RowVersionToken.Stale;

        var result = await host.Get<ICommandHandler<DeactivateCommand, CompanyDto>>()
            .HandleAsync(new DeactivateCommand(company.Id, Requests.Version(company.RowVersion)), default);

        ResultAssert.Failed(result, ErrorType.Conflict);
    }

    // ---- Access scope ----

    private sealed class CodeScope(string code) : IAccessScope<Company>
    {
        public int? SeenUserId { get; private set; } = -1;

        public Expression<Func<Company, bool>> PredicateFor(ICurrentUser user)
        {
            SeenUserId = user.UserId;
            return company => company.Code == code;
        }
    }

    private async Task<(TestHost Host, CompanyDto Visible, CompanyDto Hidden, CodeScope Scope)> ScopedHostAsync()
    {
        var scope = new CodeScope("VIS");
        var host = NewHost(services => services.AddSingleton<IAccessScope<Company>>(scope));
        host.User.UserId = 7;
        var visible = await host.CreateCompanyAsync("VIS", "Visible");
        var hidden = await host.CreateCompanyAsync("HID", "Hidden");
        return (host, visible, hidden, scope);
    }

    [Fact]
    public async Task Scope_filters_get_all_and_query_and_receives_the_current_user()
    {
        var (host, _, _, scope) = await ScopedHostAsync();

        var all = ResultAssert.Succeeded(await host.Get<IGetAllHandler<CompanyDto>>().HandleAsync(default));
        var page = ResultAssert.Succeeded(await host.Get<IQueryHandler<CompanyDto, CompanyFilter>>()
            .HandleAsync(new PageRequest<CompanyFilter>(0, 10, null, null, new CompanyFilter()), default));

        Assert.Equal(["VIS"], all.Select(c => c.Code));
        Assert.Equal(["VIS"], page.Items.Select(c => c.Code));
        Assert.Equal(1, page.Total);
        Assert.Equal(7, scope.SeenUserId);
    }

    [Fact]
    public async Task A_record_outside_the_scope_behaves_like_a_missing_one_for_get_update_delete_and_commands()
    {
        var (host, visible, hidden, _) = await ScopedHostAsync();
        var version = Requests.Version(hidden.RowVersion);

        var get = await host.Get<IGetByIdHandler<CompanyDto>>().HandleAsync(hidden.Id, default);
        var update = await host.Get<IUpdateHandler<CompanyUpdateRequest, CompanyDto>>()
            .HandleAsync(hidden.Id, Requests.CompanyUpdate(hidden.RowVersion, "HID", "Hidden"), default);
        var delete = await host.Get<IDeleteHandler<CompanyDto>>().HandleAsync(hidden.Id, version, default);
        var command = await host.Get<ICommandHandler<DeactivateCommand, CompanyDto>>()
            .HandleAsync(new DeactivateCommand(hidden.Id, version), default);

        var expected = $"Company #{hidden.Id} was not found.";
        ResultAssert.Failed(get, ErrorType.NotFound, expected);
        ResultAssert.Failed(update, ErrorType.NotFound, expected);
        ResultAssert.Failed(delete, ErrorType.NotFound, expected);
        ResultAssert.Failed(command, ErrorType.NotFound, expected);
        Assert.Equal(2, host.Repository<Company>().Items.Count);
        Assert.True(ResultAssert.Succeeded(await host.Get<IGetByIdHandler<CompanyDto>>().HandleAsync(visible.Id, default)).Code == "VIS");
    }

    [Fact]
    public async Task Without_a_registered_scope_every_record_is_visible()
    {
        var host = NewHost();
        await host.CreateCompanyAsync("A1", "One");
        await host.CreateCompanyAsync("B2", "Two");

        var all = ResultAssert.Succeeded(await host.Get<IGetAllHandler<CompanyDto>>().HandleAsync(default));

        Assert.Equal(2, all.Count);
    }

    private static async Task AssertTogglesAsync<TDto>(
        TestHost host,
        int id,
        byte[] rowVersion,
        Func<TDto, string> status,
        Func<TDto, byte[]> version)
    {
        var deactivate = host.Get<ICommandHandler<DeactivateCommand, TDto>>();
        var activate = host.Get<ICommandHandler<ActivateCommand, TDto>>();

        var inactive = ResultAssert.Succeeded(await deactivate.HandleAsync(new DeactivateCommand(id, Requests.Version(rowVersion)), default));
        Assert.Equal("Inactive", status(inactive));
        Assert.NotEqual(rowVersion, version(inactive));

        ResultAssert.Failed(
            await activate.HandleAsync(new ActivateCommand(id, Requests.Version(rowVersion)), default),
            ErrorType.Conflict);

        var active = ResultAssert.Succeeded(await activate.HandleAsync(new ActivateCommand(id, Requests.Version(version(inactive))), default));
        Assert.Equal("Active", status(active));
    }
}
