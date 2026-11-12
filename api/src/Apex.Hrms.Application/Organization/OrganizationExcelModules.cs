using Apex.Hrms.Application.Excel;
using Apex.Hrms.Application.Organization.Branches;
using Apex.Hrms.Application.Organization.Companies;
using Apex.Hrms.Application.Organization.Departments;
using Apex.Hrms.Application.Organization.JobGrades;
using Apex.Hrms.Application.Organization.Positions;
using Apex.Hrms.Application.Organization.Sections;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;
using Microsoft.Extensions.DependencyInjection;
using static Apex.Hrms.Application.Excel.ExcelModuleSupport;
using CompanyHandlers = Apex.Hrms.Application.Organization.CodedHandlers<Apex.Hrms.Domain.Organization.Company, Apex.Hrms.Application.Organization.Companies.CompanyDto, Apex.Hrms.Application.Organization.Companies.CompanyFilter, Apex.Hrms.Application.Organization.Companies.CompanyCreateRequest, Apex.Hrms.Application.Organization.Companies.CompanyUpdateRequest>;
using BranchHandlers = Apex.Hrms.Application.Organization.CodedHandlers<Apex.Hrms.Domain.Organization.Branch, Apex.Hrms.Application.Organization.Branches.BranchDto, Apex.Hrms.Application.Organization.Branches.BranchFilter, Apex.Hrms.Application.Organization.Branches.BranchCreateRequest, Apex.Hrms.Application.Organization.Branches.BranchUpdateRequest>;
using DepartmentHandlers = Apex.Hrms.Application.Organization.CodedHandlers<Apex.Hrms.Domain.Organization.Department, Apex.Hrms.Application.Organization.Departments.DepartmentDto, Apex.Hrms.Application.Organization.Departments.DepartmentFilter, Apex.Hrms.Application.Organization.Departments.DepartmentCreateRequest, Apex.Hrms.Application.Organization.Departments.DepartmentUpdateRequest>;
using JobGradeHandlers = Apex.Hrms.Application.Organization.CodedHandlers<Apex.Hrms.Domain.Organization.JobGrade, Apex.Hrms.Application.Organization.JobGrades.JobGradeDto, Apex.Hrms.Application.Organization.JobGrades.JobGradeFilter, Apex.Hrms.Application.Organization.JobGrades.JobGradeCreateRequest, Apex.Hrms.Application.Organization.JobGrades.JobGradeUpdateRequest>;
using PositionHandlers = Apex.Hrms.Application.Organization.CodedHandlers<Apex.Hrms.Domain.Organization.Position, Apex.Hrms.Application.Organization.Positions.PositionDto, Apex.Hrms.Application.Organization.Positions.PositionFilter, Apex.Hrms.Application.Organization.Positions.PositionCreateRequest, Apex.Hrms.Application.Organization.Positions.PositionUpdateRequest>;
using SectionHandlers = Apex.Hrms.Application.Organization.CodedHandlers<Apex.Hrms.Domain.Organization.Section, Apex.Hrms.Application.Organization.Sections.SectionDto, Apex.Hrms.Application.Organization.Sections.SectionFilter, Apex.Hrms.Application.Organization.Sections.SectionCreateRequest, Apex.Hrms.Application.Organization.Sections.SectionUpdateRequest>;

namespace Apex.Hrms.Application.Organization;

// Excel modules of the organization resources. There is no frontend mock to port for them (the mock only has employees), so the
// columns follow the employee module's conventions: business codes for every reference, a Status column, the same shape rules.
// Permissions: export needs organization:View; import needs organization:Create and organization:Edit.

internal static class OrganizationExcelRegistration
{
    public static IServiceCollection AddOrganizationExcel(this IServiceCollection services)
    {
        services.AddScoped<ExcelEmployeeLookup>();
        services.AddScoped<ExcelOrganizationCache>();
        services.AddScoped<CompanyHandlers>();
        services.AddScoped<BranchHandlers>();
        services.AddScoped<DepartmentHandlers>();
        services.AddScoped<SectionHandlers>();
        services.AddScoped<PositionHandlers>();
        services.AddScoped<JobGradeHandlers>();
        services.AddScoped<IExcelModule, CompanyExcelModule>();
        services.AddScoped<IExcelModule, BranchExcelModule>();
        services.AddScoped<IExcelModule, DepartmentExcelModule>();
        services.AddScoped<IExcelModule, SectionExcelModule>();
        services.AddScoped<IExcelModule, PositionExcelModule>();
        services.AddScoped<IExcelModule, JobGradeExcelModule>();
        return services;
    }
}

internal static class OrganizationExcelColumns
{
    public static readonly string[] Statuses = ["Active", "Inactive"];

    public static readonly string[] BranchTypes = ["Head Office", "Branch", "Regional Office", "Warehouse"];

    public static ExcelColumn CodeColumn(string label, string example) =>
        Col("code", $"{label} Code", $"Unique code of the {label.ToLowerInvariant()}. Existing records are matched by this code.", example, c => c with
        {
            Required = true,
            MaxLength = 20,
            Uppercase = true,
            Pattern = Code,
            PatternMessage = $"{label} Code must use capital letters, numbers and dashes only.",
            Aliases = ["Code"],
        });

    public static ExcelColumn Reference(string key, string header, string description, object? example, bool required = true) =>
        Col(key, header, description, example, c => c with { Required = required, MaxLength = 20 });

    public static ExcelColumn Status() =>
        Col("status", "Status", "Active or Inactive. Blank means Active for a new record and no change for an existing one.", "Active", c => c with { Type = ExcelColumnType.Enum, Allowed = Statuses });

    public static ExcelModuleInfo Module(string code, string label, string description, IReadOnlyList<ExcelColumn> columns, IReadOnlyList<ExcelColumn>? exportColumns, string keyLabel) =>
        new(code, label, label, description, columns, exportColumns, ["code"], keyLabel, CanImport: true, CanExport: true, ExportDateLabel: null);

    public static IReadOnlyDictionary<string, object?> Names(params (string Code, string Name)[] rows) =>
        rows.ToDictionary(r => r.Code, r => (object?)r.Name);
}

// ---- Companies ----

internal sealed class CompanyExcelModule(CompanyHandlers handlers)
    : CodedExcelModule<Company, CompanyDto, CompanyFilter, CompanyCreateRequest, CompanyUpdateRequest>(handlers)
{
    private static readonly IReadOnlyList<ExcelColumn> Columns =
    [
        OrganizationExcelColumns.CodeColumn("Company", "AHT"),
        Col("name", "Company Name", "Short name of the company (must be unique).", "Apex Horizon", c => c with { Required = true, MaxLength = 100 }),
        Col("legalName", "Legal Name", "Registered legal name.", "Apex Horizon Trading Co., Ltd.", c => c with { Required = true, MaxLength = 150 }),
        Col("registrationNumber", "Registration Number", "Company registration number (optional).", "REG-0001", c => c with { MaxLength = 40 }),
        Col("taxId", "Tax ID", "Tax identification number (optional).", "TAX-0001", c => c with { MaxLength = 40 }),
        Col("industry", "Industry", "Line of business.", "Trading", c => c with { Required = true, MaxLength = 80 }),
        Col("email", "Email", "Company email address.", "info@apexhorizon.example", c => c with { Required = true, MaxLength = 120, Type = ExcelColumnType.Email }),
        Col("phone", "Phone", "Company phone number.", "+95 1 234 567", c => c with { Required = true, Type = ExcelColumnType.Phone }),
        Col("website", "Website", "Website address (optional).", "www.apexhorizon.example", c => c with { MaxLength = 150 }),
        Col("address", "Address", "Street address (optional).", "12 Park Road", c => c with { MaxLength = 200 }),
        Col("city", "City", "City.", "Yangon", c => c with { Required = true, MaxLength = 60 }),
        Col("country", "Country", "Country.", "Myanmar", c => c with { Required = true, MaxLength = 60 }),
        Col("foundedDate", "Founded Date", "Date the company was founded.", "2015-06-01", c => c with { Required = true, Type = ExcelColumnType.Date }),
        OrganizationExcelColumns.Status(),
    ];

    public static ExcelModuleInfo Definition { get; } = OrganizationExcelColumns.Module(
        "companies", "Companies", "Import company records. Existing companies are matched by Company Code.", Columns, null, "Company Code");

    public override ExcelModuleInfo Info => Definition;

    protected override IReadOnlyDictionary<string, string> FieldToColumn { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["code"] = "code",
    };

    protected override string StatusOf(CompanyDto dto) => dto.Status;

    protected override byte[] VersionOf(CompanyDto dto) => dto.RowVersion;

    public override Task<IReadOnlyList<ExportSheet>> ReferenceSheetsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ExportSheet>>([]);

    protected override Task<CompanyCreateRequest?> BuildCreateAsync(ParsedRow row, RowProblems problems, CancellationToken cancellationToken) =>
        Task.FromResult<CompanyCreateRequest?>(new CompanyCreateRequest(
            row.Text("code"),
            row.Text("name"),
            row.Text("legalName"),
            Opt(row.Text("registrationNumber")),
            Opt(row.Text("taxId")),
            row.Text("industry"),
            row.Text("email"),
            row.Text("phone"),
            Opt(row.Text("website")),
            Opt(row.Text("address")),
            row.Text("city"),
            row.Text("country"),
            Date(row.Text("foundedDate")),
            StatusOrActive(row)));

    protected override Task<CompanyUpdateRequest?> BuildUpdateAsync(ParsedRow row, CompanyDto current, RowProblems problems, CancellationToken cancellationToken) =>
        Task.FromResult<CompanyUpdateRequest?>(new CompanyUpdateRequest(
            current.Code,
            row.Text("name"),
            row.Text("legalName"),
            Opt(Pick(row, "registrationNumber", current.RegistrationNumber)),
            Opt(Pick(row, "taxId", current.TaxId)),
            row.Text("industry"),
            row.Text("email"),
            row.Text("phone"),
            Opt(Pick(row, "website", current.Website)),
            Opt(Pick(row, "address", current.Address)),
            row.Text("city"),
            row.Text("country"),
            Date(row.Text("foundedDate")),
            Convert.ToBase64String(current.RowVersion)));

    protected override Task<IReadOnlyList<Dictionary<string, object?>>> ToRecordsAsync(IReadOnlyList<CompanyDto> dtos, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Dictionary<string, object?>>>([.. dtos.Select(c => new Dictionary<string, object?>
        {
            ["code"] = c.Code, ["name"] = c.Name, ["legalName"] = c.LegalName, ["registrationNumber"] = c.RegistrationNumber, ["taxId"] = c.TaxId,
            ["industry"] = c.Industry, ["email"] = c.Email, ["phone"] = c.Phone, ["website"] = c.Website, ["address"] = c.Address,
            ["city"] = c.City, ["country"] = c.Country, ["foundedDate"] = c.FoundedDate, ["status"] = c.Status,
        })]);
}

// ---- Branches ----

internal sealed class BranchExcelModule(BranchHandlers handlers)
    : CodedExcelModule<Branch, BranchDto, BranchFilter, BranchCreateRequest, BranchUpdateRequest>(handlers)
{
    private static readonly IReadOnlyList<ExcelColumn> Columns =
    [
        OrganizationExcelColumns.CodeColumn("Branch", "YGN-HQ"),
        Col("name", "Branch Name", "Name of the branch (must be unique).", "Yangon Head Office", c => c with { Required = true, MaxLength = 100 }),
        OrganizationExcelColumns.Reference("companyCode", "Company Code", "Code of the company the branch belongs to (see the \"Company codes\" sheet).", "AHT"),
        Col("type", "Branch Type", "Kind of branch.", "Head Office", c => c with { Required = true, Type = ExcelColumnType.Enum, Allowed = OrganizationExcelColumns.BranchTypes }),
        Col("email", "Email", "Branch email address (optional).", "yangon@apexhorizon.example", c => c with { MaxLength = 120, Type = ExcelColumnType.Email }),
        Col("phone", "Phone", "Branch phone number (optional).", "+95 1 234 567", c => c with { Type = ExcelColumnType.Phone }),
        Col("address", "Address", "Street address (optional).", "12 Park Road", c => c with { MaxLength = 200 }),
        Col("city", "City", "City.", "Yangon", c => c with { Required = true, MaxLength = 60 }),
        Col("country", "Country", "Country.", "Myanmar", c => c with { Required = true, MaxLength = 60 }),
        Col("managerCode", "Manager Employee Code", "Employee Code of the branch manager (optional).", "EMP003", c => c with { MaxLength = 12, Example = null }),
        Col("openedDate", "Opened Date", "Date the branch opened.", "2018-01-15", c => c with { Required = true, Type = ExcelColumnType.Date }),
        OrganizationExcelColumns.Status(),
    ];

    private static readonly IReadOnlyList<ExcelColumn> ExportColumns = [.. Columns, Out("company", "Company", width: 28)];

    public static ExcelModuleInfo Definition { get; } = OrganizationExcelColumns.Module(
        "branches", "Branches", "Import branch records. Existing branches are matched by Branch Code; the company is given by its code.", Columns, ExportColumns, "Branch Code");

    public override ExcelModuleInfo Info => Definition;

    protected override IReadOnlyDictionary<string, string> FieldToColumn { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["code"] = "code",
        ["companyId"] = "companyCode",
        ["managerEmployeeId"] = "managerCode",
        ["type"] = "type",
    };

    protected override string StatusOf(BranchDto dto) => dto.Status;

    protected override byte[] VersionOf(BranchDto dto) => dto.RowVersion;

    public override async Task<IReadOnlyList<ExportSheet>> ReferenceSheetsAsync(CancellationToken cancellationToken)
    {
        var companies = await Handlers.Org.CompaniesAsync(cancellationToken);
        return [CompanyCodes(companies)];
    }

    internal static ExportSheet CompanyCodes(IReadOnlyList<Company> companies) => new(
        "Company codes",
        [Plain("code", "Company Code", 16), Plain("name", "Company", 30)],
        ExcelRows.Of(companies.Select(c => new Dictionary<string, object?> { ["code"] = c.Code, ["name"] = c.Name })));

    private async Task<Company?> CompanyAsync(ParsedRow row, RowProblems problems, CancellationToken cancellationToken)
    {
        var code = row.Text("companyCode").Trim().ToUpperInvariant();
        var found = (await Handlers.Org.CompaniesAsync(cancellationToken)).FirstOrDefault(c => c.Code == code);
        if (found is null)
        {
            problems.Add("companyCode", $"Company \"{row.Text("companyCode")}\" does not exist.");
        }

        return found;
    }

    protected override async Task<BranchCreateRequest?> BuildCreateAsync(ParsedRow row, RowProblems problems, CancellationToken cancellationToken)
    {
        var company = await CompanyAsync(row, problems, cancellationToken);
        var manager = await EmployeeIdAsync(row, "managerCode", "Manager", problems, cancellationToken);
        return company is null || problems.Any
            ? null
            : new BranchCreateRequest(
                company.Id, row.Text("code"), row.Text("name"), row.Text("type"), Opt(row.Text("email")), Opt(row.Text("phone")),
                Opt(row.Text("address")), row.Text("city"), row.Text("country"), manager, Date(row.Text("openedDate")), StatusOrActive(row));
    }

    protected override async Task<BranchUpdateRequest?> BuildUpdateAsync(ParsedRow row, BranchDto current, RowProblems problems, CancellationToken cancellationToken)
    {
        var company = await CompanyAsync(row, problems, cancellationToken);
        var manager = await EmployeeIdAsync(row, "managerCode", "Manager", problems, cancellationToken);
        return company is null || problems.Any
            ? null
            : new BranchUpdateRequest(
                company.Id, current.Code, row.Text("name"), row.Text("type"), Opt(Pick(row, "email", current.Email)), Opt(Pick(row, "phone", current.Phone)),
                Opt(Pick(row, "address", current.Address)), row.Text("city"), row.Text("country"),
                row.Has("managerCode") ? manager : current.ManagerEmployeeId, Date(row.Text("openedDate")), Convert.ToBase64String(current.RowVersion));
    }

    protected override async Task<IReadOnlyList<Dictionary<string, object?>>> ToRecordsAsync(IReadOnlyList<BranchDto> dtos, CancellationToken cancellationToken)
    {
        var companies = (await Handlers.Org.CompaniesAsync(cancellationToken)).ToDictionary(c => c.Id);
        var managers = await Handlers.Employees.CodesAsync(dtos.Select(b => b.ManagerEmployeeId), cancellationToken);
        return [.. dtos.Select(b => new Dictionary<string, object?>
        {
            ["code"] = b.Code, ["name"] = b.Name, ["companyCode"] = companies.GetValueOrDefault(b.CompanyId)?.Code ?? string.Empty,
            ["company"] = companies.GetValueOrDefault(b.CompanyId)?.Name ?? string.Empty, ["type"] = b.Type, ["email"] = b.Email, ["phone"] = b.Phone,
            ["address"] = b.Address, ["city"] = b.City, ["country"] = b.Country,
            ["managerCode"] = b.ManagerEmployeeId is { } id && managers.TryGetValue(id, out var code) ? code : string.Empty,
            ["openedDate"] = b.OpenedDate, ["status"] = b.Status,
        })];
    }
}

// ---- Departments ----

internal sealed class DepartmentExcelModule(DepartmentHandlers handlers)
    : CodedExcelModule<Department, DepartmentDto, DepartmentFilter, DepartmentCreateRequest, DepartmentUpdateRequest>(handlers)
{
    private static readonly IReadOnlyList<ExcelColumn> Columns =
    [
        OrganizationExcelColumns.CodeColumn("Department", "ENG"),
        Col("name", "Department Name", "Name of the department.", "Engineering", c => c with { Required = true, MaxLength = 100 }),
        OrganizationExcelColumns.Reference("companyCode", "Company Code", "Code of the company (see the \"Organization codes\" sheet).", "AHT"),
        OrganizationExcelColumns.Reference("branchCode", "Branch Code", "Code of a branch of that company.", "YGN-HQ"),
        Col("description", "Description", "What the department does (optional).", "Software development", c => c with { MaxLength = 300 }),
        Col("headCode", "Head Employee Code", "Employee Code of the department head (optional).", "EMP003", c => c with { MaxLength = 12, Example = null }),
        Col("costCenter", "Cost Center", "Cost center code (optional).", "CC-100", c => c with { MaxLength = 20 }),
        OrganizationExcelColumns.Status(),
    ];

    private static readonly IReadOnlyList<ExcelColumn> ExportColumns = [.. Columns, Out("company", "Company", width: 28), Out("branch", "Branch", width: 24)];

    public static ExcelModuleInfo Definition { get; } = OrganizationExcelColumns.Module(
        "departments", "Departments", "Import department records. Existing departments are matched by Department Code; company and branch are given by their codes.", Columns, ExportColumns, "Department Code");

    public override ExcelModuleInfo Info => Definition;

    protected override IReadOnlyDictionary<string, string> FieldToColumn { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["code"] = "code",
        ["companyId"] = "companyCode",
        ["branchId"] = "branchCode",
        ["headEmployeeId"] = "headCode",
    };

    protected override string StatusOf(DepartmentDto dto) => dto.Status;

    protected override byte[] VersionOf(DepartmentDto dto) => dto.RowVersion;

    public override async Task<IReadOnlyList<ExportSheet>> ReferenceSheetsAsync(CancellationToken cancellationToken) =>
        [await OrganizationCodesAsync(Handlers, cancellationToken)];

    internal static async Task<ExportSheet> OrganizationCodesAsync<TE, TD, TF, TC, TU>(
        CodedHandlers<TE, TD, TF, TC, TU> handlers,
        CancellationToken cancellationToken)
        where TE : CodedRecord
    {
        var companies = (await handlers.Org.CompaniesAsync(cancellationToken)).ToDictionary(c => c.Id);
        var rows = (await handlers.Org.BranchesAsync(cancellationToken))
            .Where(b => companies.ContainsKey(b.CompanyId))
            .Select(b => new Dictionary<string, object?>
            {
                ["companyCode"] = companies[b.CompanyId].Code, ["company"] = companies[b.CompanyId].Name, ["branchCode"] = b.Code, ["branch"] = b.Name,
            });
        return new ExportSheet(
            "Organization codes",
            [Plain("companyCode", "Company Code", 16), Plain("company", "Company", 28), Plain("branchCode", "Branch Code", 16), Plain("branch", "Branch", 28)],
            ExcelRows.Of(rows));
    }

    private async Task<(Company Company, Branch Branch)?> ParentsAsync(ParsedRow row, RowProblems problems, CancellationToken cancellationToken)
    {
        var companyCode = row.Text("companyCode").Trim().ToUpperInvariant();
        var company = (await Handlers.Org.CompaniesAsync(cancellationToken)).FirstOrDefault(c => c.Code == companyCode);
        if (company is null)
        {
            problems.Add("companyCode", $"Company \"{row.Text("companyCode")}\" does not exist.");
            return null;
        }

        var branchCode = row.Text("branchCode").Trim().ToUpperInvariant();
        var branch = (await Handlers.Org.BranchesAsync(cancellationToken)).FirstOrDefault(b => b.CompanyId == company.Id && b.Code == branchCode);
        if (branch is null)
        {
            problems.Add("branchCode", $"Branch \"{row.Text("branchCode")}\" does not exist in company {company.Name}.");
            return null;
        }

        return (company, branch);
    }

    protected override async Task<DepartmentCreateRequest?> BuildCreateAsync(ParsedRow row, RowProblems problems, CancellationToken cancellationToken)
    {
        var parents = await ParentsAsync(row, problems, cancellationToken);
        var head = await EmployeeIdAsync(row, "headCode", "Head employee", problems, cancellationToken);
        return parents is null || problems.Any
            ? null
            : new DepartmentCreateRequest(
                parents.Value.Company.Id, parents.Value.Branch.Id, row.Text("code"), row.Text("name"), Opt(row.Text("description")), head,
                Opt(row.Text("costCenter")), StatusOrActive(row));
    }

    protected override async Task<DepartmentUpdateRequest?> BuildUpdateAsync(ParsedRow row, DepartmentDto current, RowProblems problems, CancellationToken cancellationToken)
    {
        var parents = await ParentsAsync(row, problems, cancellationToken);
        var head = await EmployeeIdAsync(row, "headCode", "Head employee", problems, cancellationToken);
        return parents is null || problems.Any
            ? null
            : new DepartmentUpdateRequest(
                parents.Value.Company.Id, parents.Value.Branch.Id, current.Code, row.Text("name"), Opt(Pick(row, "description", current.Description)),
                row.Has("headCode") ? head : current.HeadEmployeeId, Opt(Pick(row, "costCenter", current.CostCenter)), Convert.ToBase64String(current.RowVersion));
    }

    protected override async Task<IReadOnlyList<Dictionary<string, object?>>> ToRecordsAsync(IReadOnlyList<DepartmentDto> dtos, CancellationToken cancellationToken)
    {
        var companies = (await Handlers.Org.CompaniesAsync(cancellationToken)).ToDictionary(c => c.Id);
        var branches = (await Handlers.Org.BranchesAsync(cancellationToken)).ToDictionary(b => b.Id);
        var heads = await Handlers.Employees.CodesAsync(dtos.Select(d => d.HeadEmployeeId), cancellationToken);
        return [.. dtos.Select(d => new Dictionary<string, object?>
        {
            ["code"] = d.Code, ["name"] = d.Name, ["companyCode"] = companies.GetValueOrDefault(d.CompanyId)?.Code ?? string.Empty,
            ["company"] = companies.GetValueOrDefault(d.CompanyId)?.Name ?? string.Empty, ["branchCode"] = branches.GetValueOrDefault(d.BranchId)?.Code ?? string.Empty,
            ["branch"] = branches.GetValueOrDefault(d.BranchId)?.Name ?? string.Empty, ["description"] = d.Description,
            ["headCode"] = d.HeadEmployeeId is { } id && heads.TryGetValue(id, out var code) ? code : string.Empty,
            ["costCenter"] = d.CostCenter, ["status"] = d.Status,
        })];
    }
}

// ---- Sections ----

internal sealed class SectionExcelModule(SectionHandlers handlers)
    : CodedExcelModule<Section, SectionDto, SectionFilter, SectionCreateRequest, SectionUpdateRequest>(handlers)
{
    private static readonly IReadOnlyList<ExcelColumn> Columns =
    [
        OrganizationExcelColumns.CodeColumn("Section", "ENG-BE"),
        Col("name", "Section Name", "Name of the section.", "Back-end", c => c with { Required = true, MaxLength = 100 }),
        OrganizationExcelColumns.Reference("departmentCode", "Department Code", "Code of the department the section belongs to (see the \"Department codes\" sheet).", "ENG"),
        Col("description", "Description", "What the section does (optional).", "Server-side services", c => c with { MaxLength = 300 }),
        Col("supervisorCode", "Supervisor Employee Code", "Employee Code of the section supervisor (optional).", "EMP003", c => c with { MaxLength = 12, Example = null }),
        OrganizationExcelColumns.Status(),
    ];

    private static readonly IReadOnlyList<ExcelColumn> ExportColumns = [.. Columns, Out("department", "Department", width: 28)];

    public static ExcelModuleInfo Definition { get; } = OrganizationExcelColumns.Module(
        "sections", "Sections", "Import section records. Existing sections are matched by Section Code; the department is given by its code.", Columns, ExportColumns, "Section Code");

    public override ExcelModuleInfo Info => Definition;

    protected override IReadOnlyDictionary<string, string> FieldToColumn { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["code"] = "code",
        ["departmentId"] = "departmentCode",
        ["supervisorEmployeeId"] = "supervisorCode",
    };

    protected override string StatusOf(SectionDto dto) => dto.Status;

    protected override byte[] VersionOf(SectionDto dto) => dto.RowVersion;

    public override async Task<IReadOnlyList<ExportSheet>> ReferenceSheetsAsync(CancellationToken cancellationToken) =>
        [await DepartmentCodesAsync(Handlers, cancellationToken)];

    internal static async Task<ExportSheet> DepartmentCodesAsync<TE, TD, TF, TC, TU>(
        CodedHandlers<TE, TD, TF, TC, TU> handlers,
        CancellationToken cancellationToken)
        where TE : CodedRecord
    {
        var branches = (await handlers.Org.BranchesAsync(cancellationToken)).ToDictionary(b => b.Id);
        var rows = (await handlers.Org.DepartmentsAsync(cancellationToken)).Select(d => new Dictionary<string, object?>
        {
            ["branchCode"] = branches.GetValueOrDefault(d.BranchId)?.Code ?? string.Empty, ["departmentCode"] = d.Code, ["department"] = d.Name,
        });
        return new ExportSheet(
            "Department codes",
            [Plain("branchCode", "Branch Code", 16), Plain("departmentCode", "Department Code", 18), Plain("department", "Department", 30)],
            ExcelRows.Of(rows));
    }

    private async Task<Department?> DepartmentAsync(ParsedRow row, RowProblems problems, CancellationToken cancellationToken)
    {
        var code = row.Text("departmentCode").Trim().ToUpperInvariant();
        var found = (await Handlers.Org.DepartmentsAsync(cancellationToken)).FirstOrDefault(d => d.Code == code);
        if (found is null)
        {
            problems.Add("departmentCode", $"Department \"{row.Text("departmentCode")}\" does not exist.");
        }

        return found;
    }

    protected override async Task<SectionCreateRequest?> BuildCreateAsync(ParsedRow row, RowProblems problems, CancellationToken cancellationToken)
    {
        var department = await DepartmentAsync(row, problems, cancellationToken);
        var supervisor = await EmployeeIdAsync(row, "supervisorCode", "Supervisor", problems, cancellationToken);
        return department is null || problems.Any
            ? null
            : new SectionCreateRequest(department.Id, row.Text("code"), row.Text("name"), Opt(row.Text("description")), supervisor, StatusOrActive(row));
    }

    protected override async Task<SectionUpdateRequest?> BuildUpdateAsync(ParsedRow row, SectionDto current, RowProblems problems, CancellationToken cancellationToken)
    {
        var department = await DepartmentAsync(row, problems, cancellationToken);
        var supervisor = await EmployeeIdAsync(row, "supervisorCode", "Supervisor", problems, cancellationToken);
        return department is null || problems.Any
            ? null
            : new SectionUpdateRequest(
                department.Id, current.Code, row.Text("name"), Opt(Pick(row, "description", current.Description)),
                row.Has("supervisorCode") ? supervisor : current.SupervisorEmployeeId, Convert.ToBase64String(current.RowVersion));
    }

    protected override async Task<IReadOnlyList<Dictionary<string, object?>>> ToRecordsAsync(IReadOnlyList<SectionDto> dtos, CancellationToken cancellationToken)
    {
        var departments = (await Handlers.Org.DepartmentsAsync(cancellationToken)).ToDictionary(d => d.Id);
        var supervisors = await Handlers.Employees.CodesAsync(dtos.Select(s => s.SupervisorEmployeeId), cancellationToken);
        return [.. dtos.Select(s => new Dictionary<string, object?>
        {
            ["code"] = s.Code, ["name"] = s.Name, ["departmentCode"] = departments.GetValueOrDefault(s.DepartmentId)?.Code ?? string.Empty,
            ["department"] = departments.GetValueOrDefault(s.DepartmentId)?.Name ?? string.Empty, ["description"] = s.Description,
            ["supervisorCode"] = s.SupervisorEmployeeId is { } id && supervisors.TryGetValue(id, out var code) ? code : string.Empty,
            ["status"] = s.Status,
        })];
    }
}

// ---- Job grades ----

internal sealed class JobGradeExcelModule(JobGradeHandlers handlers)
    : CodedExcelModule<JobGrade, JobGradeDto, JobGradeFilter, JobGradeCreateRequest, JobGradeUpdateRequest>(handlers)
{
    private static readonly IReadOnlyList<ExcelColumn> Columns =
    [
        OrganizationExcelColumns.CodeColumn("Grade", "G3"),
        Col("name", "Grade Name", "Name of the job grade.", "Senior", c => c with { Required = true, MaxLength = 60 }),
        Col("level", "Level", "Grade level, 1 or higher.", 3, c => c with { Required = true, Type = ExcelColumnType.Integer, Min = 1, Max = 32767 }),
        Col("minSalary", "Minimum Salary", "Lowest monthly salary of the band (sample data).", 1000, c => c with { Required = true, Type = ExcelColumnType.Decimal, Min = 0, Decimals = 2 }),
        Col("maxSalary", "Maximum Salary", "Highest monthly salary of the band (not below the minimum).", 2500, c => c with { Required = true, Type = ExcelColumnType.Decimal, Min = 0, Decimals = 2 }),
        Col("description", "Description", "Description of the grade (optional).", "Senior individual contributor", c => c with { MaxLength = 300 }),
        OrganizationExcelColumns.Status(),
    ];

    public static ExcelModuleInfo Definition { get; } = OrganizationExcelColumns.Module(
        "job-grades", "Job grades", "Import job grades with their salary bands. Existing grades are matched by Grade Code.", Columns, null, "Grade Code");

    public override ExcelModuleInfo Info => Definition;

    protected override IReadOnlyDictionary<string, string> FieldToColumn { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["code"] = "code",
    };

    protected override string StatusOf(JobGradeDto dto) => dto.Status;

    protected override byte[] VersionOf(JobGradeDto dto) => dto.RowVersion;

    public override Task<IReadOnlyList<ExportSheet>> ReferenceSheetsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ExportSheet>>([]);

    protected override Task<JobGradeCreateRequest?> BuildCreateAsync(ParsedRow row, RowProblems problems, CancellationToken cancellationToken) =>
        Task.FromResult<JobGradeCreateRequest?>(new JobGradeCreateRequest(
            row.Text("code"), row.Text("name"), (short)Number(row.Text("level")), Number(row.Text("minSalary")), Number(row.Text("maxSalary")),
            Opt(row.Text("description")), StatusOrActive(row)));

    protected override Task<JobGradeUpdateRequest?> BuildUpdateAsync(ParsedRow row, JobGradeDto current, RowProblems problems, CancellationToken cancellationToken) =>
        Task.FromResult<JobGradeUpdateRequest?>(new JobGradeUpdateRequest(
            current.Code, row.Text("name"), (short)Number(row.Text("level")), Number(row.Text("minSalary")), Number(row.Text("maxSalary")),
            Opt(Pick(row, "description", current.Description)), Convert.ToBase64String(current.RowVersion)));

    protected override Task<IReadOnlyList<Dictionary<string, object?>>> ToRecordsAsync(IReadOnlyList<JobGradeDto> dtos, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Dictionary<string, object?>>>([.. dtos.Select(g => new Dictionary<string, object?>
        {
            ["code"] = g.Code, ["name"] = g.Name, ["level"] = (int)g.Level, ["minSalary"] = g.MinSalary, ["maxSalary"] = g.MaxSalary,
            ["description"] = g.Description, ["status"] = g.Status,
        })]);
}

// ---- Positions ----

internal sealed class PositionExcelModule(PositionHandlers handlers)
    : CodedExcelModule<Position, PositionDto, PositionFilter, PositionCreateRequest, PositionUpdateRequest>(handlers)
{
    private static readonly IReadOnlyList<ExcelColumn> Columns =
    [
        OrganizationExcelColumns.CodeColumn("Position", "ENG-SE"),
        Col("title", "Position Title", "Title of the position.", "Software Engineer", c => c with { Required = true, MaxLength = 100, Aliases = ["Job Title", "Title"] }),
        OrganizationExcelColumns.Reference("departmentCode", "Department Code", "Code of the department (see the \"Department codes\" sheet).", "ENG"),
        OrganizationExcelColumns.Reference("sectionCode", "Section Code", "Code of a section of that department (optional).", null, required: false),
        OrganizationExcelColumns.Reference("jobGradeCode", "Job Grade Code", "Code of the job grade (see the \"Grade codes\" sheet).", "G3"),
        Col("description", "Description", "What the position does (optional).", "Builds and maintains services", c => c with { MaxLength = 300 }),
        Col("headcount", "Headcount", "Number of people the position can hold, 1 or more.", 2, c => c with { Required = true, Type = ExcelColumnType.Integer, Min = 1, Max = 32767 }),
        OrganizationExcelColumns.Reference("reportsToCode", "Reports To Position Code", "Code of the position this one reports to (optional).", null, required: false),
        OrganizationExcelColumns.Status(),
    ];

    private static readonly IReadOnlyList<ExcelColumn> ExportColumns = [.. Columns, Out("department", "Department", width: 28), Out("jobGrade", "Job Grade")];

    public static ExcelModuleInfo Definition { get; } = OrganizationExcelColumns.Module(
        "positions", "Positions", "Import job positions (titles). Existing positions are matched by Position Code; department, section and grade are given by their codes.", Columns, ExportColumns, "Position Code");

    public override ExcelModuleInfo Info => Definition;

    protected override IReadOnlyDictionary<string, string> FieldToColumn { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["code"] = "code",
        ["departmentId"] = "departmentCode",
        ["sectionId"] = "sectionCode",
        ["jobGradeId"] = "jobGradeCode",
        ["reportsToPositionId"] = "reportsToCode",
    };

    protected override string StatusOf(PositionDto dto) => dto.Status;

    protected override byte[] VersionOf(PositionDto dto) => dto.RowVersion;

    public override async Task<IReadOnlyList<ExportSheet>> ReferenceSheetsAsync(CancellationToken cancellationToken)
    {
        var grades = (await Handlers.Org.GradesAsync(cancellationToken)).Select(g => new Dictionary<string, object?> { ["code"] = g.Code, ["name"] = g.Name });
        return
        [
            await SectionExcelModule.DepartmentCodesAsync(Handlers, cancellationToken),
            new ExportSheet("Grade codes", [Plain("code", "Job Grade Code", 16), Plain("name", "Job Grade", 30)], ExcelRows.Of(grades)),
        ];
    }

    private sealed record Parents(Department Department, int? SectionId, JobGrade Grade, int? ReportsToId);

    private async Task<Parents?> ParentsAsync(ParsedRow row, RowProblems problems, CancellationToken cancellationToken)
    {
        var departmentCode = row.Text("departmentCode").Trim().ToUpperInvariant();
        var department = (await Handlers.Org.DepartmentsAsync(cancellationToken)).FirstOrDefault(d => d.Code == departmentCode);
        if (department is null)
        {
            problems.Add("departmentCode", $"Department \"{row.Text("departmentCode")}\" does not exist.");
        }

        int? sectionId = null;
        if (department is not null && row.Has("sectionCode"))
        {
            var sectionCode = row.Text("sectionCode").Trim().ToUpperInvariant();
            var section = (await Handlers.Org.SectionsAsync(cancellationToken)).FirstOrDefault(s => s.DepartmentId == department.Id && s.Code == sectionCode);
            if (section is null)
            {
                problems.Add("sectionCode", $"Section \"{row.Text("sectionCode")}\" does not exist in department {department.Name}.");
            }
            else
            {
                sectionId = section.Id;
            }
        }

        var gradeCode = row.Text("jobGradeCode").Trim().ToUpperInvariant();
        var grade = (await Handlers.Org.GradesAsync(cancellationToken)).FirstOrDefault(g => g.Code == gradeCode);
        if (grade is null)
        {
            problems.Add("jobGradeCode", $"Job grade \"{row.Text("jobGradeCode")}\" does not exist.");
        }

        // Positions are read live: one created earlier in the same file can be a reporting line.
        int? reportsTo = null;
        if (row.Has("reportsToCode"))
        {
            var reportsToCode = row.Text("reportsToCode").Trim().ToUpperInvariant();
            var parent = await FindByCodeAsync(reportsToCode, cancellationToken);
            if (parent is null)
            {
                problems.Add("reportsToCode", $"Position \"{row.Text("reportsToCode")}\" does not exist.");
            }
            else
            {
                reportsTo = parent.Id;
            }
        }

        return department is null || grade is null || problems.Any ? null : new Parents(department, sectionId, grade, reportsTo);
    }

    protected override async Task<PositionCreateRequest?> BuildCreateAsync(ParsedRow row, RowProblems problems, CancellationToken cancellationToken)
    {
        var parents = await ParentsAsync(row, problems, cancellationToken);
        return parents is null
            ? null
            : new PositionCreateRequest(
                parents.Department.Id, parents.SectionId, parents.Grade.Id, row.Text("code"), row.Text("title"), Opt(row.Text("description")),
                (short)Number(row.Text("headcount")), parents.ReportsToId, StatusOrActive(row));
    }

    protected override async Task<PositionUpdateRequest?> BuildUpdateAsync(ParsedRow row, PositionDto current, RowProblems problems, CancellationToken cancellationToken)
    {
        var parents = await ParentsAsync(row, problems, cancellationToken);
        if (parents is null)
        {
            return null;
        }

        // A blank section or reporting line keeps the current one (a section is kept only while the department stays the same).
        var section = row.Has("sectionCode") ? parents.SectionId : current.DepartmentId == parents.Department.Id ? current.SectionId : null;
        return new PositionUpdateRequest(
            parents.Department.Id, section, parents.Grade.Id, current.Code, row.Text("title"), Opt(Pick(row, "description", current.Description)),
            (short)Number(row.Text("headcount")), row.Has("reportsToCode") ? parents.ReportsToId : current.ReportsToPositionId,
            Convert.ToBase64String(current.RowVersion));
    }

    protected override async Task<IReadOnlyList<Dictionary<string, object?>>> ToRecordsAsync(IReadOnlyList<PositionDto> dtos, CancellationToken cancellationToken)
    {
        var departments = (await Handlers.Org.DepartmentsAsync(cancellationToken)).ToDictionary(d => d.Id);
        var sections = (await Handlers.Org.SectionsAsync(cancellationToken)).ToDictionary(s => s.Id);
        var grades = (await Handlers.Org.GradesAsync(cancellationToken)).ToDictionary(g => g.Id);
        var positions = (await Handlers.Repository.ListAsync(Predicates.Always<Position>(), cancellationToken)).ToDictionary(p => p.Id);
        return [.. dtos.Select(p => new Dictionary<string, object?>
        {
            ["code"] = p.Code, ["title"] = p.Title, ["departmentCode"] = departments.GetValueOrDefault(p.DepartmentId)?.Code ?? string.Empty,
            ["department"] = departments.GetValueOrDefault(p.DepartmentId)?.Name ?? string.Empty,
            ["sectionCode"] = p.SectionId is { } s ? sections.GetValueOrDefault(s)?.Code ?? string.Empty : string.Empty,
            ["jobGradeCode"] = grades.GetValueOrDefault(p.JobGradeId)?.Code ?? string.Empty,
            ["jobGrade"] = grades.GetValueOrDefault(p.JobGradeId)?.Name ?? string.Empty, ["description"] = p.Description,
            ["headcount"] = (int)p.Headcount,
            ["reportsToCode"] = p.ReportsToPositionId is { } r ? positions.GetValueOrDefault(r)?.Code ?? string.Empty : string.Empty,
            ["status"] = p.Status,
        })];
    }
}
