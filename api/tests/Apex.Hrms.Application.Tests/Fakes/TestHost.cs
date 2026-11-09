using Apex.Hrms.Application;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Organization.Branches;
using Apex.Hrms.Application.Organization.Companies;
using Apex.Hrms.Application.Organization.Departments;
using Apex.Hrms.Application.Organization.JobGrades;
using Apex.Hrms.Application.Organization.Positions;
using Apex.Hrms.Application.Organization.Sections;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.Application.Tests.Fakes;

/// <summary>
/// The real Application layer wired to in-memory fakes, so tests exercise handlers, validators and rules
/// exactly as the API does without a database.
/// </summary>
internal sealed class TestHost : IDisposable
{
    private readonly ServiceProvider provider;

    public TestHost()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddSingleton<IClock>(Clock);
        services.AddSingleton<IUnitOfWork>(UnitOfWork);
        services.AddSingleton<IReferenceUsageReader>(References);
        services.AddSingleton(typeof(IRepository<>), typeof(InMemoryRepository<>));
        provider = services.BuildServiceProvider();
    }

    public FakeClock Clock { get; } = new(new DateOnly(2026, 11, 9));

    public FakeUnitOfWork UnitOfWork { get; } = new();

    public FakeReferenceUsageReader References { get; } = new();

    public InMemoryRepository<TEntity> Repository<TEntity>()
        where TEntity : Entity =>
        (InMemoryRepository<TEntity>)provider.GetRequiredService<IRepository<TEntity>>();

    public T Get<T>()
        where T : notnull =>
        provider.GetRequiredService<T>();

    public void Dispose() => provider.Dispose();

    // ---- Seeding through the real create handlers: every helper returns the created DTO. ----

    public async Task<CompanyDto> CreateCompanyAsync(string code = "AHT", string name = "Apex Holdings", string status = "Active") =>
        await Succeeded(Get<ICreateHandler<CompanyRequest, CompanyDto>>().HandleAsync(Requests.Company(code, name, status), default));

    public async Task<BranchDto> CreateBranchAsync(int companyId, string code = "YGN", string status = "Active") =>
        await Succeeded(Get<ICreateHandler<BranchRequest, BranchDto>>().HandleAsync(Requests.Branch(companyId, code, status), default));

    public async Task<DepartmentDto> CreateDepartmentAsync(int companyId, int branchId, string code = "ENG", string status = "Active") =>
        await Succeeded(Get<ICreateHandler<DepartmentRequest, DepartmentDto>>().HandleAsync(Requests.Department(companyId, branchId, code, status), default));

    public async Task<SectionDto> CreateSectionAsync(int departmentId, string code = "BE", string status = "Active") =>
        await Succeeded(Get<ICreateHandler<SectionRequest, SectionDto>>().HandleAsync(Requests.Section(departmentId, code, status), default));

    public async Task<JobGradeDto> CreateJobGradeAsync(string code = "G1", string status = "Active") =>
        await Succeeded(Get<ICreateHandler<JobGradeRequest, JobGradeDto>>().HandleAsync(Requests.JobGrade(code, status), default));

    public async Task<PositionDto> CreatePositionAsync(int departmentId, int jobGradeId, string code = "DEV", int? sectionId = null, int? reportsTo = null) =>
        await Succeeded(Get<ICreateHandler<PositionRequest, PositionDto>>().HandleAsync(Requests.Position(departmentId, jobGradeId, code, sectionId, reportsTo), default));

    private static async Task<T> Succeeded<T>(Task<Result<T>> pending)
    {
        var result = await pending;
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        return result.Value;
    }
}
