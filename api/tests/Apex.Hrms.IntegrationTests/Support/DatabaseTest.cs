using Apex.Hrms.Application;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Leave.Applications;
using Apex.Hrms.Application.Leave.LeavePolicies;
using Apex.Hrms.Application.Leave.LeaveTypes;
using Apex.Hrms.Application.Organization;
using Apex.Hrms.Application.Organization.Branches;
using Apex.Hrms.Application.Organization.Companies;
using Apex.Hrms.Application.Organization.Departments;
using Apex.Hrms.Application.Organization.JobGrades;
using Apex.Hrms.Application.Organization.Positions;
using Apex.Hrms.Application.Organization.Sections;
using Apex.Hrms.Application.Recruitment.Applications;
using Apex.Hrms.Application.Recruitment.Candidates;
using Apex.Hrms.Application.Recruitment.Interviews;
using Apex.Hrms.Application.Recruitment.JobOpenings;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.IntegrationTests.Support;

/// <summary>
/// Base of the database tests. Nothing runs in an ambient transaction: the handlers, the API host and <see cref="Sql"/>
/// each use their own connection and every write is committed. Isolation comes from the <see cref="Tag"/>: every
/// code and name a test creates starts with it, and <see cref="DisposeAsync"/> deletes only the rows with that tag
/// (see <see cref="TestRows"/>). The handlers come from the same DI registration the API uses
/// (<c>AddApplication</c> + <c>AddInfrastructure</c>), and every call runs in a fresh scope, like a request: nothing
/// stays tracked between two calls.
/// </summary>
public abstract class DatabaseTest : IAsyncLifetime
{
    private readonly List<Action<IServiceCollection>> customizations = [];
    private ServiceProvider? provider;

    protected DatabaseTest() => Requests = new Requests(Tag);

    /// <summary>The tag of this test: <c>TST-</c> and 8 hex characters. Unique per test instance.</summary>
    protected string Tag { get; } = TestRows.NewTag();

    /// <summary>Valid requests whose codes and names carry this test's tag.</summary>
    protected Requests Requests { get; }

    /// <summary>The employee linked to the fake signed-in user of the handlers; null (the default) is the built-in administrator.</summary>
    protected int? CurrentEmployeeId { get; set; }

    /// <summary>The clock the audit stamps use; advance it to see <c>ModifiedAtUtc</c> move.</summary>
    protected TestTimeProvider Time { get; } = new(new DateTimeOffset(2026, 11, 9, 8, 0, 0, 123, TimeSpan.Zero));

    /// <summary>Runs once, just before the next save, to simulate another session writing between check and save.</summary>
    protected Func<Task>? BeforeNextSave { get; set; }

    private ServiceProvider Provider => provider ??= BuildProvider();

    public virtual Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            Cleanup();
            provider?.Dispose();
        }
        finally
        {
            await TestRows.DeleteAsync(Tag);
        }
    }

    /// <summary>The code a request built with suffix <paramref name="suffix"/> gets, for assertions and raw SQL.</summary>
    protected string Code(string suffix) => $"{Tag}-{suffix}";

    /// <summary>The name a request built with <paramref name="text"/> gets, for assertions and raw SQL.</summary>
    protected string Name(string text) => $"{Tag} {text}";

    /// <summary>Releases what a derived test holds on to (hosts, clients) before the rows are deleted.</summary>
    protected virtual void Cleanup()
    {
    }

    /// <summary>Changes the registrations before the first use, for example to remove a pre-check.</summary>
    protected void Customize(Action<IServiceCollection> change)
    {
        Assert.Null(provider);
        customizations.Add(change);
    }

    protected async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = Provider.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    // ---- The handlers, one call per request. ----

    protected Task<Result<CompanyDto>> Create(CompanyCreateRequest request) => CreateAsync<CompanyCreateRequest, CompanyDto>(request);

    protected Task<Result<BranchDto>> Create(BranchCreateRequest request) => CreateAsync<BranchCreateRequest, BranchDto>(request);

    protected Task<Result<DepartmentDto>> Create(DepartmentCreateRequest request) => CreateAsync<DepartmentCreateRequest, DepartmentDto>(request);

    protected Task<Result<SectionDto>> Create(SectionCreateRequest request) => CreateAsync<SectionCreateRequest, SectionDto>(request);

    protected Task<Result<JobGradeDto>> Create(JobGradeCreateRequest request) => CreateAsync<JobGradeCreateRequest, JobGradeDto>(request);

    protected Task<Result<PositionDto>> Create(PositionCreateRequest request) => CreateAsync<PositionCreateRequest, PositionDto>(request);

    protected Task<Result<EmployeeDto>> Create(EmployeeCreateRequest request) => CreateAsync<EmployeeCreateRequest, EmployeeDto>(request);

    protected Task<Result<EmployeeDto>> Update(int id, EmployeeUpdateRequest request) => UpdateAsync<EmployeeUpdateRequest, EmployeeDto>(id, request);

    protected Task<Result<CompanyDto>> Update(int id, CompanyUpdateRequest request) => UpdateAsync<CompanyUpdateRequest, CompanyDto>(id, request);

    protected Task<Result<BranchDto>> Update(int id, BranchUpdateRequest request) => UpdateAsync<BranchUpdateRequest, BranchDto>(id, request);

    protected Task<Result<DepartmentDto>> Update(int id, DepartmentUpdateRequest request) => UpdateAsync<DepartmentUpdateRequest, DepartmentDto>(id, request);

    protected Task<Result<SectionDto>> Update(int id, SectionUpdateRequest request) => UpdateAsync<SectionUpdateRequest, SectionDto>(id, request);

    protected Task<Result<JobGradeDto>> Update(int id, JobGradeUpdateRequest request) => UpdateAsync<JobGradeUpdateRequest, JobGradeDto>(id, request);

    protected Task<Result<PositionDto>> Update(int id, PositionUpdateRequest request) => UpdateAsync<PositionUpdateRequest, PositionDto>(id, request);

    protected Task<Result<TDto>> Get<TDto>(int id) =>
        InScopeAsync(sp => sp.GetRequiredService<IGetByIdHandler<TDto>>().HandleAsync(id, default));

    protected Task<Result> Delete<TDto>(int id, string? rowVersion) =>
        InScopeAsync(sp => sp.GetRequiredService<IDeleteHandler<TDto>>().HandleAsync(id, rowVersion, default));

    protected Task<Result<TDto>> Activate<TDto>(int id, string? rowVersion) =>
        InScopeAsync(sp => sp.GetRequiredService<ICommandHandler<ActivateCommand, TDto>>()
            .HandleAsync(new ActivateCommand(id, rowVersion), default));

    protected Task<Result<TDto>> Deactivate<TDto>(int id, string? rowVersion) =>
        InScopeAsync(sp => sp.GetRequiredService<ICommandHandler<DeactivateCommand, TDto>>()
            .HandleAsync(new DeactivateCommand(id, rowVersion), default));

    protected Task<Result<TDto>> RunCommand<TCommand, TDto>(TCommand command)
        where TCommand : IEntityCommand =>
        InScopeAsync(sp => sp.GetRequiredService<ICommandHandler<TCommand, TDto>>().HandleAsync(command, default));

    protected Task<Result<LeaveTypeDto>> Create(LeaveTypeCreateRequest request) => CreateAsync<LeaveTypeCreateRequest, LeaveTypeDto>(request);

    protected Task<Result<LeavePolicyDto>> Create(LeavePolicyCreateRequest request) => CreateAsync<LeavePolicyCreateRequest, LeavePolicyDto>(request);

    protected Task<Result<LeaveApplicationDto>> Create(LeaveApplicationCreateRequest request) => CreateAsync<LeaveApplicationCreateRequest, LeaveApplicationDto>(request);

    protected Task<Result<JobOpeningDto>> Create(JobOpeningCreateRequest request) => CreateAsync<JobOpeningCreateRequest, JobOpeningDto>(request);

    protected Task<Result<CandidateDto>> Create(CandidateCreateRequest request) => CreateAsync<CandidateCreateRequest, CandidateDto>(request);

    protected Task<Result<JobApplicationDto>> Create(JobApplicationCreateRequest request) => CreateAsync<JobApplicationCreateRequest, JobApplicationDto>(request);

    protected Task<Result<InterviewDto>> Create(InterviewCreateRequest request) => CreateAsync<InterviewCreateRequest, InterviewDto>(request);

    protected Task<Result<JobOpeningDto>> Update(int id, JobOpeningUpdateRequest request) => UpdateAsync<JobOpeningUpdateRequest, JobOpeningDto>(id, request);

    protected Task<Result<CandidateDto>> Update(int id, CandidateUpdateRequest request) => UpdateAsync<CandidateUpdateRequest, CandidateDto>(id, request);

    protected Task<Result<LeaveTypeDto>> Update(int id, LeaveTypeUpdateRequest request) => UpdateAsync<LeaveTypeUpdateRequest, LeaveTypeDto>(id, request);

    protected Task<Result<LeavePolicyDto>> Update(int id, LeavePolicyUpdateRequest request) => UpdateAsync<LeavePolicyUpdateRequest, LeavePolicyDto>(id, request);

    protected Task<Result<LeaveApplicationDto>> Update(int id, LeaveApplicationUpdateRequest request) => UpdateAsync<LeaveApplicationUpdateRequest, LeaveApplicationDto>(id, request);

    protected Task<Result<PagedResult<TDto>>> Query<TDto, TFilter>(PageRequest<TFilter> request) =>
        InScopeAsync(sp => sp.GetRequiredService<IQueryHandler<TDto, TFilter>>().HandleAsync(request, default));

    // ---- Seeding through the real create handlers; each helper fails the test when the create is rejected. ----

    protected async Task<CompanyDto> SeedCompanyAsync(string code = "A", string name = "Test Company A") =>
        Succeeded(await Create(Requests.Company(code, name)));

    protected async Task<BranchDto> SeedBranchAsync(int companyId, string code = "A-YGN", string name = "Yangon Test Office") =>
        Succeeded(await Create(Requests.Branch(companyId, code, name)));

    protected async Task<DepartmentDto> SeedDepartmentAsync(int companyId, int branchId, string code = "A-ENG", string name = "Engineering") =>
        Succeeded(await Create(Requests.Department(companyId, branchId, code, name)));

    protected async Task<SectionDto> SeedSectionAsync(int departmentId, string code = "A-BE", string name = "Backend") =>
        Succeeded(await Create(Requests.Section(departmentId, code, name)));

    protected async Task<JobGradeDto> SeedJobGradeAsync(string code = "G1", string name = "Test Grade") =>
        Succeeded(await Create(Requests.JobGrade(code, name)));

    protected async Task<PositionDto> SeedPositionAsync(int departmentId, int jobGradeId, string code = "A-DEV", int? sectionId = null) =>
        Succeeded(await Create(Requests.Position(departmentId, jobGradeId, code, sectionId: sectionId)));

    /// <summary>A company with a branch, a department, a job grade and a position: the organization an employee needs.</summary>
    protected async Task<(CompanyDto Company, BranchDto Branch, DepartmentDto Department, JobGradeDto Grade, PositionDto Position)> SeedOrganizationAsync()
    {
        var company = await SeedCompanyAsync();
        var branch = await SeedBranchAsync(company.Id);
        var department = await SeedDepartmentAsync(company.Id, branch.Id);
        var grade = await SeedJobGradeAsync();
        var position = await SeedPositionAsync(department.Id, grade.Id);
        return (company, branch, department, grade, position);
    }

    protected async Task<EmployeeDto> SeedEmployeeAsync(
        (CompanyDto Company, BranchDto Branch, DepartmentDto Department, JobGradeDto Grade, PositionDto Position) org,
        string? codeSuffix = "1",
        string local = "mia") =>
        Succeeded(await Create(Requests.Employee(org.Company.Id, org.Branch.Id, org.Department.Id, org.Position.Id, org.Grade.Id, codeSuffix, local)));

    protected async Task<LeaveTypeDto> SeedLeaveTypeAsync(string code = "AL", string name = "Annual Leave", decimal days = 15m, short maxConsecutive = 10, bool requiresDocument = false, string? gender = null) =>
        Succeeded(await Create(Requests.LeaveType(code, name, days, maxConsecutive, requiresDocument, gender)));

    protected async Task<LeavePolicyDto> SeedLeavePolicyAsync(int leaveTypeId, string name = "Standard", int? jobGradeId = null, string? employmentType = null, decimal days = 15m, string accrual = "Upfront", short minServiceMonths = 0) =>
        Succeeded(await Create(Requests.LeavePolicy(leaveTypeId, name, jobGradeId, employmentType, days, accrual, minServiceMonths)));

    protected async Task<LeaveApplicationDto> SeedLeaveApplicationAsync(int employeeId, int leaveTypeId, DateOnly start, DateOnly? end = null, bool halfDay = false) =>
        Succeeded(await Create(Requests.LeaveApplication(employeeId, leaveTypeId, start, end, halfDay)));

    /// <summary>Creates a draft and submits it.</summary>
    protected async Task<LeaveApplicationDto> SeedSubmittedLeaveAsync(int employeeId, int leaveTypeId, DateOnly start, DateOnly? end = null)
    {
        var draft = await SeedLeaveApplicationAsync(employeeId, leaveTypeId, start, end);
        return Succeeded(await RunCommand<SubmitLeaveCommand, LeaveApplicationDto>(new SubmitLeaveCommand(draft.Id, Requests.Version(draft.RowVersion))));
    }

    protected static T Succeeded<T>(Result<T> result)
    {
        Assert.True(result.IsSuccess, result.IsFailure ? $"{result.Error.Type}: {result.Error.Message}" : null);
        return result.Value;
    }

    protected static AppError Failed(Result result, ErrorType type)
    {
        Assert.True(result.IsFailure, "Expected a failure but the operation succeeded.");
        Assert.Equal(type, result.Error.Type);
        return result.Error;
    }

    /// <summary>A validation failure that names <paramref name="field"/>; returns the field's message.</summary>
    protected static string FailedOnField(Result result, string field)
    {
        var error = Failed(result, ErrorType.Validation);
        Assert.NotNull(error.FieldErrors);
        Assert.True(error.FieldErrors.TryGetValue(field, out var messages), $"No error on '{field}'; got {string.Join(", ", error.FieldErrors.Keys)}.");
        return Assert.Single(messages);
    }

    private Task<Result<TDto>> CreateAsync<TRequest, TDto>(TRequest request) =>
        InScopeAsync(sp => sp.GetRequiredService<ICreateHandler<TRequest, TDto>>().HandleAsync(request, default));

    private Task<Result<TDto>> UpdateAsync<TRequest, TDto>(int id, TRequest request) =>
        InScopeAsync(sp => sp.GetRequiredService<IUpdateHandler<TRequest, TDto>>().HandleAsync(id, request, default));

    private ServiceProvider BuildProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{Infrastructure.DependencyInjection.ConnectionStringName}"] = SqlServerEnvironment.ConnectionString,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(Time);
        services.AddApplication().AddInfrastructure(configuration);

        // No request here, so there is no cookie: a fake signed-in TST- user stands in (the audit rule rejects anonymous writes).
        services.AddScoped<ICurrentUser>(_ => new TestCurrentUser(TestAdministrator.AccountId, $"{Tag} Test User", employeeId: CurrentEmployeeId));
        DecorateUnitOfWork(services);
        foreach (var change in customizations)
        {
            change(services);
        }

        return services.BuildServiceProvider();
    }

    private void DecorateUnitOfWork(ServiceCollection services)
    {
        var original = services.Single(descriptor => descriptor.ServiceType == typeof(IUnitOfWork));
        services.Remove(original);
        services.AddScoped<IUnitOfWork>(sp => new HookedUnitOfWork(
            (IUnitOfWork)ActivatorUtilities.CreateInstance(sp, original.ImplementationType!),
            this));
    }

    private sealed class HookedUnitOfWork(IUnitOfWork inner, DatabaseTest owner) : IUnitOfWork
    {
        public async Task<Result> SaveChangesAsync(CancellationToken cancellationToken)
        {
            if (owner.BeforeNextSave is { } hook)
            {
                owner.BeforeNextSave = null;
                await hook();
            }

            return await inner.SaveChangesAsync(cancellationToken);
        }
    }
}
