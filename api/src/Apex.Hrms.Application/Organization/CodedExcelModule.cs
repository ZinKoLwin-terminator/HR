using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Excel;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Organization;

/// <summary>The handlers of one organization resource that its Excel module drives (the same ones the API routes use).</summary>
internal sealed record CodedHandlers<TEntity, TDto, TFilter, TCreate, TUpdate>(
    IRepository<TEntity> Repository,
    IQueryHandler<TDto, TFilter> Query,
    IGetByIdHandler<TDto> Get,
    ICreateHandler<TCreate, TDto> Create,
    IUpdateHandler<TUpdate, TDto> Update,
    ICommandHandler<ActivateCommand, TDto> Activate,
    ICommandHandler<DeactivateCommand, TDto> Deactivate,
    ExcelEmployeeLookup Employees,
    ExcelOrganizationCache Org)
    where TEntity : CodedRecord;

/// <summary>The parent tables of the organization modules, read once per request (they are small and do not change while a file is read).</summary>
internal sealed class ExcelOrganizationCache(
    IRepository<Company> companies,
    IRepository<Branch> branches,
    IRepository<Department> departments,
    IRepository<Section> sections,
    IRepository<JobGrade> grades)
{
    private IReadOnlyList<Company>? companyRows;
    private IReadOnlyList<Branch>? branchRows;
    private IReadOnlyList<Department>? departmentRows;
    private IReadOnlyList<Section>? sectionRows;
    private IReadOnlyList<JobGrade>? gradeRows;

    public async Task<IReadOnlyList<Company>> CompaniesAsync(CancellationToken cancellationToken) =>
        companyRows ??= await companies.ListAsync(Predicates.Always<Company>(), cancellationToken);

    public async Task<IReadOnlyList<Branch>> BranchesAsync(CancellationToken cancellationToken) =>
        branchRows ??= await branches.ListAsync(Predicates.Always<Branch>(), cancellationToken);

    public async Task<IReadOnlyList<Department>> DepartmentsAsync(CancellationToken cancellationToken) =>
        departmentRows ??= await departments.ListAsync(Predicates.Always<Department>(), cancellationToken);

    public async Task<IReadOnlyList<Section>> SectionsAsync(CancellationToken cancellationToken) =>
        sectionRows ??= await sections.ListAsync(Predicates.Always<Section>(), cancellationToken);

    public async Task<IReadOnlyList<JobGrade>> GradesAsync(CancellationToken cancellationToken) =>
        gradeRows ??= await grades.ListAsync(Predicates.Always<JobGrade>(), cancellationToken);
}

/// <summary>Finds employees by code (and codes by id) inside the caller's employee scope, for the columns that point at an employee.</summary>
internal sealed class ExcelEmployeeLookup(IRepository<Employee> employees, IAccessScope<Employee> scope, ICurrentUser user)
{
    public async Task<Employee?> FindAsync(string code, CancellationToken cancellationToken)
    {
        var upper = code.Trim().ToUpperInvariant();
        var found = await employees.ListAsync(scope.PredicateFor(user).And(e => e.EmployeeCode == upper), cancellationToken);
        return found.Count > 0 ? found[0] : null;
    }

    public async Task<Dictionary<int, string>> CodesAsync(IEnumerable<int?> ids, CancellationToken cancellationToken)
    {
        var wanted = ids.Where(i => i is not null).Select(i => i!.Value).Distinct().ToList();
        if (wanted.Count == 0)
        {
            return [];
        }

        var found = await employees.ListAsync(scope.PredicateFor(user).And(e => wanted.Contains(e.Id)), cancellationToken);
        return found.ToDictionary(e => e.Id, e => e.EmployeeCode);
    }
}

/// <summary>
/// The shared flow of the organization modules (companies, branches, departments, sections, positions, job grades): rows are
/// matched by the record's business code and go through the create, update and activate/deactivate handlers of the API, so a
/// row is accepted only when the API would accept it. A module supplies its columns and how a row becomes a request.
/// </summary>
internal abstract class CodedExcelModule<TEntity, TDto, TFilter, TCreate, TUpdate>(
    CodedHandlers<TEntity, TDto, TFilter, TCreate, TUpdate> handlers) : IExcelModule
    where TEntity : CodedRecord
    where TDto : IIdentified
{
    public abstract ExcelModuleInfo Info { get; }

    public IReadOnlyList<string> ImportPolicies { get; } = ["organization:Create", "organization:Edit"];

    public IReadOnlyList<string> ExportPolicies { get; } = ["organization:View"];

    public IReadOnlyList<string> SensitiveExportPolicies { get; } = [];

    protected CodedHandlers<TEntity, TDto, TFilter, TCreate, TUpdate> Handlers { get; } = handlers;

    /// <summary>API field names that are not column keys (<c>companyId</c> is the column <c>companyCode</c>).</summary>
    protected abstract IReadOnlyDictionary<string, string> FieldToColumn { get; }

    protected abstract string StatusOf(TDto dto);

    protected abstract byte[] VersionOf(TDto dto);

    protected abstract Task<TCreate?> BuildCreateAsync(ParsedRow row, RowProblems problems, CancellationToken cancellationToken);

    protected abstract Task<TUpdate?> BuildUpdateAsync(ParsedRow row, TDto current, RowProblems problems, CancellationToken cancellationToken);

    protected abstract Task<IReadOnlyList<Dictionary<string, object?>>> ToRecordsAsync(IReadOnlyList<TDto> dtos, CancellationToken cancellationToken);

    public abstract Task<IReadOnlyList<ExportSheet>> ReferenceSheetsAsync(CancellationToken cancellationToken);

    public IExcelImportAdapter CreateImporter() => new Importer(this);

    public IReadOnlyList<ExportSheet> ExtraSheets(IReadOnlyList<IReadOnlyDictionary<string, object?>> rows) => [];

    public async Task<Result<IReadOnlyList<IReadOnlyDictionary<string, object?>>>> ExportRowsAsync(
        ExportRequest request,
        bool importLayout,
        int limit,
        CancellationToken cancellationToken)
    {
        var filter = ExcelModuleSupport.ParseFilter<TFilter>(request);
        if (filter.IsFailure)
        {
            return Result.Failure<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(filter.Error);
        }

        var items = await ExcelModuleSupport.CollectAsync(Handlers.Query, Handlers.Get, request, filter.Value, limit, cancellationToken);
        if (items.IsFailure)
        {
            return Result.Failure<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(items.Error);
        }

        var records = await ToRecordsAsync(items.Value, cancellationToken);
        return Result.Success(ExcelRows.Of(records));
    }

    protected async Task<TEntity?> FindByCodeAsync(string code, CancellationToken cancellationToken)
    {
        var upper = code.Trim().ToUpperInvariant();
        var found = await Handlers.Repository.ListAsync(e => e.Code == upper, cancellationToken);
        return found.Count > 0 ? found[0] : null;
    }

    /// <summary>The status a new record starts with: the Status cell, Active when blank.</summary>
    protected static string StatusOrActive(ParsedRow row) => row.Has("status") ? row.Text("status") : "Active";

    protected static string Pick(ParsedRow row, string key, string current) => row.Has(key) ? row.Text(key) : current;

    /// <summary>The employee a code cell names, or null with a problem on that column.</summary>
    protected async Task<int?> EmployeeIdAsync(ParsedRow row, string key, string label, RowProblems problems, CancellationToken cancellationToken)
    {
        if (!row.Has(key))
        {
            return null;
        }

        var found = await Handlers.Employees.FindAsync(row.Text(key), cancellationToken);
        if (found is null)
        {
            problems.Add(key, $"{label} \"{row.Text(key)}\" does not exist.");
            return null;
        }

        return found.Id;
    }

    private sealed class Importer(CodedExcelModule<TEntity, TDto, TFilter, TCreate, TUpdate> module) : IExcelImportAdapter
    {
        private string KeyColumn => module.Info.MatchKey[0];

        public Task PrepareAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public async Task<bool> ExistsAsync(ParsedRow row, CancellationToken cancellationToken) =>
            await module.FindByCodeAsync(row.Text(KeyColumn), cancellationToken) is not null;

        public async Task<IReadOnlyList<ImportIssueDto>> ApplyAsync(ParsedRow row, bool exists, CancellationToken cancellationToken)
        {
            var problems = new RowProblems(module.Info.Columns, row);
            var failure = exists
                ? await UpdateAsync(row, problems, cancellationToken)
                : await CreateAsync(row, problems, cancellationToken);
            if (failure is not null)
            {
                problems.Issues.AddRange(ExcelModuleSupport.ToIssues(failure, row, module.Info.Columns, module.FieldToColumn, KeyColumn));
            }

            return problems.Issues;
        }

        private async Task<AppError?> CreateAsync(ParsedRow row, RowProblems problems, CancellationToken cancellationToken)
        {
            var request = await module.BuildCreateAsync(row, problems, cancellationToken);
            if (request is null || problems.Any)
            {
                return null;
            }

            var created = await module.Handlers.Create.HandleAsync(request, cancellationToken);
            return created.IsFailure ? created.Error : null;
        }

        private async Task<AppError?> UpdateAsync(ParsedRow row, RowProblems problems, CancellationToken cancellationToken)
        {
            var existing = await module.FindByCodeAsync(row.Text(KeyColumn), cancellationToken);
            if (existing is null)
            {
                return AppError.NotFound("The record no longer exists.");
            }

            var current = await module.Handlers.Get.HandleAsync(existing.Id, cancellationToken);
            if (current.IsFailure)
            {
                return current.Error;
            }

            var request = await module.BuildUpdateAsync(row, current.Value, problems, cancellationToken);
            if (request is null || problems.Any)
            {
                return null;
            }

            var updated = await module.Handlers.Update.HandleAsync(current.Value.Id, request, cancellationToken);
            if (updated.IsFailure)
            {
                return updated.Error;
            }

            var wanted = row.Text("status");
            if (wanted.Length == 0 || wanted == module.StatusOf(updated.Value))
            {
                return null;
            }

            // The row version is read again, so the status change always works on the saved state.
            var reloaded = await module.Handlers.Get.HandleAsync(updated.Value.Id, cancellationToken);
            if (reloaded.IsFailure)
            {
                return reloaded.Error;
            }

            var version = Convert.ToBase64String(module.VersionOf(reloaded.Value));
            if (wanted == "Inactive")
            {
                var off = await module.Handlers.Deactivate.HandleAsync(new DeactivateCommand(reloaded.Value.Id, version), cancellationToken);
                return off.IsFailure ? off.Error : null;
            }

            var on = await module.Handlers.Activate.HandleAsync(new ActivateCommand(reloaded.Value.Id, version), cancellationToken);
            return on.IsFailure ? on.Error : null;
        }
    }
}
