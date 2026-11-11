using Apex.Hrms.Application;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Organization;
using Apex.Hrms.Application.Organization.Branches;
using Apex.Hrms.Application.Organization.Companies;
using Apex.Hrms.Application.Organization.Departments;
using Apex.Hrms.Application.Organization.JobGrades;
using Apex.Hrms.Application.Organization.Positions;
using Apex.Hrms.Application.Organization.Sections;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Attendance;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Leave;
using Apex.Hrms.Domain.Organization;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.Application.Tests.Fakes;

/// <summary>
/// The real Application layer wired to in-memory fakes, so tests exercise handlers, validators and rules
/// exactly as the API does without a database.
/// </summary>
internal sealed class TestHost : IDisposable
{
    private readonly ServiceProvider provider;

    public TestHost(Action<IServiceCollection>? configure = null)
    {
        UnitOfWork = new FakeUnitOfWork(Database);
        var services = new ServiceCollection();
        services.AddSingleton(Database);
        services.AddSingleton<IClock>(Clock);
        services.AddSingleton<ICurrentUser>(User);
        services.AddSingleton<IUnitOfWork>(UnitOfWork);
        services.AddSingleton(typeof(IRepository<>), typeof(InMemoryRepository<>));
        services.AddSingleton<IRecordCounter<EmployeeOrgLink>>(Employees);
        AddCounter<Company>(services);
        AddCounter<Branch>(services);
        AddCounter<Department>(services);
        AddCounter<Section>(services);
        AddCounter<JobGrade>(services);
        AddCounter<Position>(services);
        AddCounter<Employee>(services);
        AddCounter<LeaveType>(services);
        AddCounter<LeavePolicy>(services);
        AddCounter<LeaveApplication>(services);
        AddCounter<Shift>(services);
        AddCounter<AttendanceRecord>(services);
        AddCounter<AttendanceAdjustment>(services);
        AddCounter<OvertimeRecord>(services);
        services.AddSingleton<IEmployeeCodeSequence>(CodeSequence);
        services.AddSingleton<ILeaveApplicationNumberSequence>(LeaveNumbers);
        services.AddSingleton<IBusinessWeek>(Week);
        services.AddApplication();
        configure?.Invoke(services);
        provider = services.BuildServiceProvider();
    }

    public FakeClock Clock { get; } = new(new DateOnly(2026, 11, 9));

    public FakeDatabase Database { get; } = new();

    public FakeUnitOfWork UnitOfWork { get; }

    public FakeCurrentUser User { get; } = new();

    public FakeEmployees Employees { get; } = new();

    public FakeEmployeeCodeSequence CodeSequence { get; } = new();

    public FakeLeaveNumberSequence LeaveNumbers { get; } = new();

    /// <summary>Saturday and Sunday are the weekend unless a test changes it.</summary>
    public FakeBusinessWeek Week { get; } = new();

    public InMemoryRepository<TEntity> Repository<TEntity>()
        where TEntity : AuditedEntity =>
        (InMemoryRepository<TEntity>)provider.GetRequiredService<IRepository<TEntity>>();

    public T Get<T>()
        where T : notnull =>
        provider.GetRequiredService<T>();

    public T? GetOrDefault<T>()
        where T : class =>
        provider.GetService<T>();

    public void Dispose() => provider.Dispose();

    // ---- Seeding through the real create handlers: every helper returns the created DTO. ----

    public async Task<CompanyDto> CreateCompanyAsync(string code = "AHT", string name = "Apex Holdings", string status = "Active") =>
        await Succeeded(Get<ICreateHandler<CompanyCreateRequest, CompanyDto>>().HandleAsync(Requests.Company(code, name, status), default));

    public async Task<BranchDto> CreateBranchAsync(int companyId, string code = "YGN", string status = "Active") =>
        await Succeeded(Get<ICreateHandler<BranchCreateRequest, BranchDto>>().HandleAsync(Requests.Branch(companyId, code, status), default));

    public async Task<DepartmentDto> CreateDepartmentAsync(int companyId, int branchId, string code = "ENG", string status = "Active") =>
        await Succeeded(Get<ICreateHandler<DepartmentCreateRequest, DepartmentDto>>().HandleAsync(Requests.Department(companyId, branchId, code, status), default));

    public async Task<SectionDto> CreateSectionAsync(int departmentId, string code = "BE", string status = "Active") =>
        await Succeeded(Get<ICreateHandler<SectionCreateRequest, SectionDto>>().HandleAsync(Requests.Section(departmentId, code, status), default));

    public async Task<JobGradeDto> CreateJobGradeAsync(string code = "G1", string status = "Active") =>
        await Succeeded(Get<ICreateHandler<JobGradeCreateRequest, JobGradeDto>>().HandleAsync(Requests.JobGrade(code, status), default));

    public async Task<PositionDto> CreatePositionAsync(int departmentId, int jobGradeId, string code = "DEV", int? sectionId = null, int? reportsTo = null) =>
        await Succeeded(Get<ICreateHandler<PositionCreateRequest, PositionDto>>().HandleAsync(Requests.Position(departmentId, jobGradeId, code, sectionId, reportsTo), default));

    private static void AddCounter<TEntity>(IServiceCollection services)
        where TEntity : AuditedEntity =>
        services.AddSingleton<IRecordCounter<TEntity>>(sp => (InMemoryRepository<TEntity>)sp.GetRequiredService<IRepository<TEntity>>());

    private static async Task<T> Succeeded<T>(Task<Result<T>> pending)
    {
        var result = await pending;
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        return result.Value;
    }
}
