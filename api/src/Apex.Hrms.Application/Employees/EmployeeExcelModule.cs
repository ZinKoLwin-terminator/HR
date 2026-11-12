using System.Globalization;
using System.Text.Json;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Excel;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Attendance;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Excel;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Employees;

/// <summary>Everything the employee import and export need from the rest of the application.</summary>
internal sealed record EmployeeExcelDependencies(
    IRepository<Employee> Employees,
    IRepository<Company> Companies,
    IRepository<Branch> Branches,
    IRepository<Department> Departments,
    IRepository<Section> Sections,
    IRepository<Position> Positions,
    IRepository<JobGrade> JobGrades,
    IRepository<Shift> Shifts,
    IAccessScope<Employee> Scope,
    ICurrentUser User,
    ICreateHandler<EmployeeCreateRequest, EmployeeDto> Create,
    IUpdateHandler<EmployeeUpdateRequest, EmployeeDto> Update,
    IUpdateHandler<EmployeePersonalUpdateRequest, EmployeePersonalDto> UpdatePersonal,
    IUpdateHandler<BankAccountUpdateRequest, BankAccountDto> UpdateBank,
    IGetByIdHandler<EmployeeDto> Get,
    IGetByIdHandler<EmployeePersonalDto> GetPersonal,
    IGetByIdHandler<BankAccountDto> GetBank,
    ICommandHandler<ExitEmployeeCommand, EmployeeDto> Exit,
    ICommandHandler<ReinstateEmployeeCommand, EmployeeDto> Reinstate,
    ICommandHandler<ChangeEmployeeStatusCommand, EmployeeDto> ChangeStatus);

/// <summary>
/// The employee module of the Excel import and export. Imported rows go through the same create and update handlers as
/// the API, so a row is accepted only when a create or an update through the API would be accepted. The permissions
/// listed are the ones those calls need; the Excel endpoints enforce them by name through the same permission check as the routes.
/// </summary>
internal sealed class EmployeeExcelModule(EmployeeExcelDependencies deps) : IExcelModule
{
    public ExcelModuleInfo Info => EmployeeExcelColumns.Info;

    public IReadOnlyList<string> ImportPolicies { get; } =
        ["employees:Create", "employees:Edit", "employees:EditSensitive", "employees:EditBank"];

    public IReadOnlyList<string> ExportPolicies { get; } = ["employees:View"];

    public IReadOnlyList<string> SensitiveExportPolicies { get; } = ["employees:ViewSensitive", "employees:ViewBank"];

    public IExcelImportAdapter CreateImporter() => new EmployeeImporter(deps);

    public async Task<IReadOnlyList<ExportSheet>> ReferenceSheetsAsync(CancellationToken cancellationToken)
    {
        var lookups = await OrganizationLookups.LoadAsync(deps, cancellationToken);
        var orgRows = lookups.Positions.Select(position =>
        {
            var department = lookups.Departments.FirstOrDefault(d => d.Id == position.DepartmentId);
            var branch = department is null ? null : lookups.Branches.FirstOrDefault(b => b.Id == department.BranchId);
            var company = branch is null ? null : lookups.Companies.FirstOrDefault(c => c.Id == branch.CompanyId);
            return department is null || branch is null || company is null
                ? null
                : (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>
                {
                    ["companyCode"] = company.Code,
                    ["company"] = company.Name,
                    ["branchCode"] = branch.Code,
                    ["branch"] = branch.Name,
                    ["departmentCode"] = department.Code,
                    ["department"] = department.Name,
                    ["positionCode"] = position.Code,
                    ["position"] = position.Title,
                };
        }).OfType<IReadOnlyDictionary<string, object?>>().ToList();

        IReadOnlyDictionary<string, object?> Pair(string type, string code, string name) =>
            new Dictionary<string, object?> { ["type"] = type, ["code"] = code, ["name"] = name };

        return
        [
            new ExportSheet(
                "Organization codes",
                [Plain("companyCode", "Company Code", 14), Plain("company", "Company", 28), Plain("branchCode", "Branch Code", 14), Plain("branch", "Branch", 28), Plain("departmentCode", "Department Code", 16), Plain("department", "Department", 28), Plain("positionCode", "Position Code", 16), Plain("position", "Position", 32)],
                orgRows),
            new ExportSheet(
                "Grade and shift codes",
                [Plain("type", "Type", 12), Plain("code", "Code", 14), Plain("name", "Name", 32)],
                [.. lookups.Grades.Select(g => Pair("Job grade", g.Code, g.Name)), .. lookups.Shifts.Select(s => Pair("Shift", s.Code, s.Name))]),
        ];
    }

    public async Task<Result<IReadOnlyList<IReadOnlyDictionary<string, object?>>>> ExportRowsAsync(
        ExportRequest request,
        bool importLayout,
        int limit,
        CancellationToken cancellationToken)
    {
        var filters = ParseFilters(request);
        if (filters.IsFailure)
        {
            return Result.Failure<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(filters.Error);
        }

        var specification = new EmployeeQuerySpecification();
        var predicate = deps.Scope.PredicateFor(deps.User);
        if (request.Scope == ExportScope.Filtered)
        {
            predicate = predicate.And(specification.Filter(filters.Value));
            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                if (request.Search.Length > Paging.MaxSearchLength)
                {
                    var message = $"Search must be at most {Paging.MaxSearchLength} characters.";
                    return Result.Failure<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(AppError.Validation(message, "search", message));
                }

                predicate = predicate.And(specification.Search(request.Search.Trim()));
            }
        }

        if (request.Scope == ExportScope.Selected)
        {
            var ids = request.SelectedIds ?? [];
            predicate = predicate.And(e => ids.Contains(e.Id));
        }

        if (request.DateFrom is { } from)
        {
            predicate = predicate.And(e => e.JoiningDate >= from);
        }

        if (request.DateTo is { } to)
        {
            predicate = predicate.And(e => e.JoiningDate <= to);
        }

        var employees = new List<Employee>();
        const int pageSize = 500;
        for (var page = 0; employees.Count <= limit; page++)
        {
            var result = await deps.Employees.QueryAsync(
                new EntityQuery<Employee>(predicate, new SortCriterion<Employee>(e => e.EmployeeCode, false), page, pageSize),
                cancellationToken);
            employees.AddRange(result.Items);
            if (result.Items.Count < pageSize || (page + 1) * pageSize >= result.Total)
            {
                break;
            }
        }

        if (employees.Count > limit + 1)
        {
            employees = employees.Take(limit + 1).ToList();
        }

        var lookups = await OrganizationLookups.LoadAsync(deps, cancellationToken);
        var managerIds = employees.Where(e => e.ManagerEmployeeId is not null).Select(e => e.ManagerEmployeeId!.Value).Distinct().ToList();
        var managers = managerIds.Count == 0
            ? []
            : (await deps.Employees.ListAsync(e => managerIds.Contains(e.Id), cancellationToken)).ToDictionary(e => e.Id);

        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows = [.. employees.Select(e => ToRecord(e, lookups, managers))];
        return Result.Success(rows);
    }

    public IReadOnlyList<ExportSheet> ExtraSheets(IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)
    {
        ExportSheet Summary(string name, string label, string key) => new(
            name,
            [Plain("name", label, 34), new ExcelColumn { Key = "employees", Header = "Employees", Type = ExcelColumnType.Integer }],
            [.. rows
                .GroupBy(r => r[key] as string is { Length: > 0 } text ? text : "—")
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?> { ["name"] = g.Key, ["employees"] = g.Count() })]);
        return [Summary("Department Summary", "Department", "department"), Summary("Branch Summary", "Branch", "branch")];
    }

    private static ExcelColumn Plain(string key, string header, int width) => new() { Key = key, Header = header, Width = width };

    private static Result<EmployeeFilter> ParseFilters(ExportRequest request)
    {
        var filter = new EmployeeFilter();
        if (request.Scope != ExportScope.Filtered || request.Filters is null)
        {
            return Result.Success(filter);
        }

        try
        {
            foreach (var (key, value) in request.Filters)
            {
                if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                {
                    continue;
                }

                filter = key.ToLowerInvariant() switch
                {
                    "departmentid" => filter with { DepartmentId = Int(value) },
                    "branchid" => filter with { BranchId = Int(value) },
                    "shiftid" => filter with { ShiftId = Int(value) },
                    "employmentstatus" => filter with { EmploymentStatus = value.GetString() },
                    "employmenttype" => filter with { EmploymentType = value.GetString() },
                    "joiningfrom" => filter with { JoiningFrom = Date(value) },
                    "joiningto" => filter with { JoiningTo = Date(value) },
                    _ => filter,
                };
            }

            return Result.Success(filter);
        }
        catch (Exception exception) when (exception is FormatException or InvalidOperationException)
        {
            return Result.Failure<EmployeeFilter>(AppError.Validation("A filter value is not valid.", "filters", "A filter value is not valid."));
        }
    }

    private static int? Int(JsonElement value) =>
        value.ValueKind == JsonValueKind.Number ? value.GetInt32() : int.Parse(value.GetString()!, CultureInfo.InvariantCulture);

    private static DateOnly? Date(JsonElement value) =>
        string.IsNullOrWhiteSpace(value.GetString()) ? null : DateOnly.ParseExact(value.GetString()![..10], "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static Dictionary<string, object?> ToRecord(Employee e, OrganizationLookups lookups, Dictionary<int, Employee> managers)
    {
        var company = lookups.Companies.FirstOrDefault(c => c.Id == e.CompanyId);
        var branch = lookups.Branches.FirstOrDefault(b => b.Id == e.BranchId);
        var department = lookups.Departments.FirstOrDefault(d => d.Id == e.DepartmentId);
        var section = e.SectionId is { } sectionId ? lookups.Sections.FirstOrDefault(s => s.Id == sectionId) : null;
        var position = lookups.Positions.FirstOrDefault(p => p.Id == e.PositionId);
        var grade = lookups.Grades.FirstOrDefault(g => g.Id == e.JobGradeId);
        var shift = e.ShiftId is { } shiftId ? lookups.Shifts.FirstOrDefault(s => s.Id == shiftId) : null;
        var manager = e.ManagerEmployeeId is { } managerId && managers.TryGetValue(managerId, out var found) ? found : null;
        return new Dictionary<string, object?>
        {
            ["employeeCode"] = e.EmployeeCode,
            ["fullName"] = EmployeeMapping.FullName(e),
            ["firstName"] = e.FirstName,
            ["lastName"] = e.LastName,
            ["gender"] = GenderText.ToText(e.Gender),
            ["workEmail"] = e.WorkEmail,
            ["phone"] = e.Phone,
            ["company"] = company?.Name ?? string.Empty,
            ["branch"] = branch?.Name ?? string.Empty,
            ["department"] = department?.Name ?? string.Empty,
            ["section"] = section?.Name ?? string.Empty,
            ["position"] = position?.Title ?? string.Empty,
            ["jobGrade"] = grade?.Code ?? string.Empty,
            ["manager"] = manager is null ? string.Empty : EmployeeMapping.FullName(manager),
            ["shift"] = shift?.Name ?? string.Empty,
            ["companyCode"] = company?.Code ?? string.Empty,
            ["branchCode"] = branch?.Code ?? string.Empty,
            ["departmentCode"] = department?.Code ?? string.Empty,
            ["sectionCode"] = section?.Code ?? string.Empty,
            ["positionCode"] = position?.Code ?? string.Empty,
            ["jobGradeCode"] = grade?.Code ?? string.Empty,
            ["managerCode"] = manager?.EmployeeCode ?? string.Empty,
            ["shiftCode"] = shift?.Code ?? string.Empty,
            ["employmentType"] = EmploymentTypeText.ToText(e.EmploymentType),
            ["employmentStatus"] = EmploymentStatusText.ToText(e.Status),
            ["joiningDate"] = e.JoiningDate,
            ["probationEndDate"] = e.ProbationEndDate,
            ["exitDate"] = e.ExitDate,
            ["dateOfBirth"] = e.DateOfBirth,
            ["maritalStatus"] = MaritalStatusText.ToText(e.MaritalStatus),
            ["nationality"] = e.Nationality,
            ["nationalId"] = e.NationalId,
            ["personalEmail"] = e.PersonalEmail,
            ["alternatePhone"] = e.AlternatePhone,
            ["addressLine1"] = e.Address.Line1,
            ["addressLine2"] = e.Address.Line2,
            ["city"] = e.Address.City,
            ["state"] = e.Address.State,
            ["postalCode"] = e.Address.PostalCode,
            ["country"] = e.Address.Country,
            ["emergencyName"] = e.EmergencyContact.Name,
            ["emergencyRelationship"] = EmergencyRelationshipText.ToText(e.EmergencyContact.Relationship),
            ["emergencyPhone"] = e.EmergencyContact.Phone,
            ["bankName"] = e.BankAccount?.BankName,
            ["bankBranchName"] = e.BankAccount?.BranchName,
            ["bankAccountName"] = e.BankAccount?.AccountName,
            ["bankAccountNumber"] = e.BankAccount?.AccountNumber,
        };
    }
}

/// <summary>The organization tables, read once per run (they are small and do not change while a batch runs).</summary>
internal sealed record OrganizationLookups(
    IReadOnlyList<Company> Companies,
    IReadOnlyList<Branch> Branches,
    IReadOnlyList<Department> Departments,
    IReadOnlyList<Section> Sections,
    IReadOnlyList<Position> Positions,
    IReadOnlyList<JobGrade> Grades,
    IReadOnlyList<Shift> Shifts)
{
    public static async Task<OrganizationLookups> LoadAsync(EmployeeExcelDependencies deps, CancellationToken cancellationToken) => new(
        await deps.Companies.ListAsync(Predicates.Always<Company>(), cancellationToken),
        await deps.Branches.ListAsync(Predicates.Always<Branch>(), cancellationToken),
        await deps.Departments.ListAsync(Predicates.Always<Department>(), cancellationToken),
        await deps.Sections.ListAsync(Predicates.Always<Section>(), cancellationToken),
        await deps.Positions.ListAsync(Predicates.Always<Position>(), cancellationToken),
        await deps.JobGrades.ListAsync(Predicates.Always<JobGrade>(), cancellationToken),
        await deps.Shifts.ListAsync(Predicates.Always<Shift>(), cancellationToken));
}

internal sealed class EmployeeImporter(EmployeeExcelDependencies deps) : IExcelImportAdapter
{
    private static readonly Dictionary<string, string> FieldToColumn = new(StringComparer.OrdinalIgnoreCase)
    {
        ["companyId"] = "companyCode",
        ["branchId"] = "branchCode",
        ["departmentId"] = "departmentCode",
        ["sectionId"] = "sectionCode",
        ["positionId"] = "positionCode",
        ["jobGradeId"] = "jobGradeCode",
        ["managerId"] = "managerCode",
        ["shiftId"] = "shiftCode",
        ["status"] = "employmentStatus",
        ["address.line1"] = "addressLine1",
        ["address.line2"] = "addressLine2",
        ["address.city"] = "city",
        ["address.state"] = "state",
        ["address.postalCode"] = "postalCode",
        ["address.country"] = "country",
        ["emergencyContact.name"] = "emergencyName",
        ["emergencyContact.relationship"] = "emergencyRelationship",
        ["emergencyContact.phone"] = "emergencyPhone",
        ["bankName"] = "bankName",
        ["branchName"] = "bankBranchName",
        ["accountName"] = "bankAccountName",
        ["accountNumber"] = "bankAccountNumber",
    };

    private static readonly string[] LeftStatuses = ["Resigned", "Terminated"];

    private OrganizationLookups lookups = null!;

    public async Task PrepareAsync(CancellationToken cancellationToken) =>
        lookups = await OrganizationLookups.LoadAsync(deps, cancellationToken);

    public async Task<bool> ExistsAsync(ParsedRow row, CancellationToken cancellationToken) =>
        await FindAsync(row.Text("employeeCode"), cancellationToken) is not null;

    public async Task<IReadOnlyList<ImportIssueDto>> ApplyAsync(ParsedRow row, bool exists, CancellationToken cancellationToken)
    {
        var issues = new List<ImportIssueDto>();
        var refs = await ResolveAsync(row, issues, cancellationToken);
        if (refs is null)
        {
            return issues;
        }

        var failure = exists
            ? await UpdateAsync(row, refs, cancellationToken)
            : await CreateAsync(row, refs, cancellationToken);
        if (failure is not null)
        {
            issues.AddRange(ToIssues(failure, row));
            return issues;
        }

        if (row.Text("employmentStatus") == "Probation" && !row.Has("probationEndDate") && !exists)
        {
            issues.Add(Warning());
        }

        return issues;
    }

    private static ImportIssueDto Warning() => new(
        EmployeeExcelColumns.ByKey("probationEndDate").Header,
        string.Empty,
        "The employee is on probation but has no probation end date.",
        "Add the probation end date.",
        ImportSeverity.Warning);

    private sealed record References(int CompanyId, int BranchId, int DepartmentId, int? SectionId, int PositionId, int JobGradeId, int? ManagerId, int? ShiftId, bool SectionGiven);

    private async Task<Employee?> FindAsync(string code, CancellationToken cancellationToken)
    {
        var upper = code.Trim().ToUpperInvariant();
        var scope = deps.Scope.PredicateFor(deps.User).And(e => e.EmployeeCode == upper);
        var found = await deps.Employees.ListAsync(scope, cancellationToken);
        return found.Count > 0 ? found[0] : null;
    }

    private async Task<References?> ResolveAsync(ParsedRow row, List<ImportIssueDto> issues, CancellationToken cancellationToken)
    {
        void Problem(string key, string message) => issues.Add(new ImportIssueDto(
            EmployeeExcelColumns.ByKey(key).Header,
            ExcelCoercion.Shown(EmployeeExcelColumns.ByKey(key), row.Text(key)),
            message,
            null,
            ImportSeverity.Error));

        bool Same(string code, string key) => string.Equals(code, row.Text(key), StringComparison.OrdinalIgnoreCase);

        var company = lookups.Companies.FirstOrDefault(c => Same(c.Code, "companyCode"));
        if (company is null)
        {
            Problem("companyCode", $"Company \"{row.Text("companyCode")}\" does not exist.");
        }

        var branch = company is null ? null : lookups.Branches.FirstOrDefault(b => b.CompanyId == company.Id && Same(b.Code, "branchCode"));
        if (company is not null && branch is null)
        {
            Problem("branchCode", $"Branch \"{row.Text("branchCode")}\" does not exist in company {company.Name}.");
        }

        var department = branch is null ? null : lookups.Departments.FirstOrDefault(d => d.BranchId == branch.Id && Same(d.Code, "departmentCode"));
        if (branch is not null && department is null)
        {
            Problem("departmentCode", $"Department \"{row.Text("departmentCode")}\" does not exist in branch {branch.Name}.");
        }

        var sectionGiven = row.Has("sectionCode");
        var section = department is not null && sectionGiven
            ? lookups.Sections.FirstOrDefault(s => s.DepartmentId == department.Id && Same(s.Code, "sectionCode"))
            : null;
        if (department is not null && sectionGiven && section is null)
        {
            Problem("sectionCode", $"Section \"{row.Text("sectionCode")}\" does not exist in department {department.Name}.");
        }

        var position = department is null ? null : lookups.Positions.FirstOrDefault(p => p.DepartmentId == department.Id && Same(p.Code, "positionCode"));
        if (department is not null && position is null)
        {
            Problem("positionCode", $"Position \"{row.Text("positionCode")}\" does not exist in department {department.Name}.");
        }

        var gradeId = position?.JobGradeId ?? 0;
        if (row.Has("jobGradeCode"))
        {
            var grade = lookups.Grades.FirstOrDefault(g => Same(g.Code, "jobGradeCode"));
            if (grade is null)
            {
                Problem("jobGradeCode", $"Job grade \"{row.Text("jobGradeCode")}\" does not exist.");
            }
            else
            {
                gradeId = grade.Id;
            }
        }

        int? managerId = null;
        if (row.Has("managerCode"))
        {
            var manager = await FindAsync(row.Text("managerCode"), cancellationToken);
            if (manager is null)
            {
                Problem("managerCode", $"Manager \"{row.Text("managerCode")}\" does not exist.");
            }
            else if (manager.HasLeft)
            {
                Problem("managerCode", $"Manager \"{manager.EmployeeCode}\" is no longer a current employee.");
            }
            else
            {
                managerId = manager.Id;
            }
        }

        int? shiftId = null;
        if (row.Has("shiftCode"))
        {
            var shift = lookups.Shifts.FirstOrDefault(s => Same(s.Code, "shiftCode"));
            if (shift is null)
            {
                Problem("shiftCode", $"Shift \"{row.Text("shiftCode")}\" does not exist.");
            }
            else
            {
                shiftId = shift.Id;
            }
        }

        return issues.Count > 0 || company is null || branch is null || department is null || position is null
            ? null
            : new References(company.Id, branch.Id, department.Id, section?.Id, position.Id, gradeId, managerId, shiftId, sectionGiven);
    }

    private async Task<AppError?> CreateAsync(ParsedRow row, References refs, CancellationToken cancellationToken)
    {
        var request = new EmployeeCreateRequest(
            row.Text("employeeCode"),
            row.Text("firstName"),
            row.Text("lastName"),
            row.Text("gender"),
            row.Text("workEmail"),
            null,
            refs.CompanyId,
            refs.BranchId,
            refs.DepartmentId,
            refs.SectionId,
            refs.PositionId,
            refs.JobGradeId,
            refs.ManagerId,
            refs.ShiftId,
            row.Text("employmentType"),
            Date(row.Text("joiningDate")),
            DateOrNull(row.Text("probationEndDate")),
            row.Text("employmentStatus"),
            Date(row.Text("dateOfBirth")),
            row.Text("maritalStatus"),
            row.Text("nationality"),
            row.Text("nationalId"),
            Opt(row.Text("personalEmail")),
            row.Text("phone"),
            Opt(row.Text("alternatePhone")),
            new AddressRequest(row.Text("addressLine1"), Opt(row.Text("addressLine2")), row.Text("city"), Opt(row.Text("state")), Opt(row.Text("postalCode")), row.Text("country")),
            new EmergencyContactRequest(row.Text("emergencyName"), row.Text("emergencyRelationship"), row.Text("emergencyPhone")));
        var created = await deps.Create.HandleAsync(request, cancellationToken);
        if (created.IsFailure)
        {
            return created.Error;
        }

        var bank = await deps.UpdateBank.HandleAsync(
            created.Value.Id,
            new BankAccountUpdateRequest(row.Text("bankName"), Opt(row.Text("bankBranchName")), row.Text("bankAccountName"), row.Text("bankAccountNumber"), null),
            cancellationToken);
        return bank.IsFailure ? bank.Error : null;
    }

    private async Task<AppError?> UpdateAsync(ParsedRow row, References refs, CancellationToken cancellationToken)
    {
        var existing = await FindAsync(row.Text("employeeCode"), cancellationToken);
        if (existing is null)
        {
            return AppError.NotFound("The record no longer exists.");
        }

        var current = await deps.Get.HandleAsync(existing.Id, cancellationToken);
        if (current.IsFailure)
        {
            return current.Error;
        }

        var cur = current.Value;
        string Pick(string key, string fallback) => row.Has(key) ? row.Text(key) : fallback;

        // Blank optional cells keep the current value.
        var requested = new EmployeeUpdateRequest(
            cur.EmployeeCode,
            Pick("firstName", cur.FirstName),
            Pick("lastName", cur.LastName),
            Pick("gender", cur.Gender),
            Pick("workEmail", cur.WorkEmail),
            cur.PhotoUrl,
            refs.CompanyId,
            refs.BranchId,
            refs.DepartmentId,
            refs.SectionGiven ? refs.SectionId : (cur.DepartmentId == refs.DepartmentId ? cur.SectionId : null),
            refs.PositionId,
            refs.JobGradeId,
            row.Has("managerCode") ? refs.ManagerId : cur.ManagerId,
            row.Has("shiftCode") ? refs.ShiftId : cur.ShiftId,
            Pick("employmentType", cur.EmploymentType),
            row.Has("joiningDate") ? Date(row.Text("joiningDate")) : cur.JoiningDate,
            row.Has("probationEndDate") ? DateOrNull(row.Text("probationEndDate")) : cur.ProbationEndDate,
            Convert.ToBase64String(cur.RowVersion));
        if (!SameEmployment(requested, cur))
        {
            var updated = await deps.Update.HandleAsync(cur.Id, requested, cancellationToken);
            if (updated.IsFailure)
            {
                return updated.Error;
            }
        }

        var personalFailure = await UpdatePersonalAsync(row, cur.Id, cancellationToken);
        if (personalFailure is not null)
        {
            return personalFailure;
        }

        var bankFailure = await UpdateBankAsync(row, cur.Id, cancellationToken);
        return bankFailure ?? await ChangeStatusAsync(row, cur, cancellationToken);
    }

    private static bool SameEmployment(EmployeeUpdateRequest r, EmployeeDto c) =>
        r.FirstName == c.FirstName && r.LastName == c.LastName && r.Gender == c.Gender && r.WorkEmail == c.WorkEmail
        && r.CompanyId == c.CompanyId && r.BranchId == c.BranchId && r.DepartmentId == c.DepartmentId && r.SectionId == c.SectionId
        && r.PositionId == c.PositionId && r.JobGradeId == c.JobGradeId && r.ManagerId == c.ManagerId && r.ShiftId == c.ShiftId
        && r.EmploymentType == c.EmploymentType && r.JoiningDate == c.JoiningDate && r.ProbationEndDate == c.ProbationEndDate;

    private async Task<AppError?> UpdatePersonalAsync(ParsedRow row, int id, CancellationToken cancellationToken)
    {
        var loaded = await deps.GetPersonal.HandleAsync(id, cancellationToken);
        if (loaded.IsFailure)
        {
            return loaded.Error;
        }

        var cur = loaded.Value;
        string Pick(string key, string fallback) => row.Has(key) ? row.Text(key) : fallback;
        var requested = new EmployeePersonalUpdateRequest(
            row.Has("dateOfBirth") ? Date(row.Text("dateOfBirth")) : cur.DateOfBirth,
            Pick("maritalStatus", cur.MaritalStatus),
            Pick("nationality", cur.Nationality),
            Pick("nationalId", cur.NationalId),
            Opt(Pick("personalEmail", cur.PersonalEmail)),
            Pick("phone", cur.Phone),
            Opt(Pick("alternatePhone", cur.AlternatePhone)),
            new AddressRequest(
                Pick("addressLine1", cur.Address.Line1),
                Opt(Pick("addressLine2", cur.Address.Line2)),
                Pick("city", cur.Address.City),
                Opt(Pick("state", cur.Address.State)),
                Opt(Pick("postalCode", cur.Address.PostalCode)),
                Pick("country", cur.Address.Country)),
            new EmergencyContactRequest(
                Pick("emergencyName", cur.EmergencyContact.Name),
                Pick("emergencyRelationship", cur.EmergencyContact.Relationship),
                Pick("emergencyPhone", cur.EmergencyContact.Phone)),
            Convert.ToBase64String(cur.RowVersion));
        var same = requested.DateOfBirth == cur.DateOfBirth && requested.MaritalStatus == cur.MaritalStatus
            && requested.Nationality == cur.Nationality && requested.NationalId == cur.NationalId
            && requested.PersonalEmail == Opt(cur.PersonalEmail) && requested.Phone == cur.Phone
            && requested.AlternatePhone == Opt(cur.AlternatePhone)
            && requested.Address!.Line1 == cur.Address.Line1 && requested.Address.Line2 == Opt(cur.Address.Line2)
            && requested.Address.City == cur.Address.City && requested.Address.State == Opt(cur.Address.State)
            && requested.Address.PostalCode == Opt(cur.Address.PostalCode) && requested.Address.Country == cur.Address.Country
            && requested.EmergencyContact!.Name == cur.EmergencyContact.Name
            && requested.EmergencyContact.Relationship == cur.EmergencyContact.Relationship
            && requested.EmergencyContact.Phone == cur.EmergencyContact.Phone;
        if (same)
        {
            return null;
        }

        var updated = await deps.UpdatePersonal.HandleAsync(id, requested, cancellationToken);
        return updated.IsFailure ? updated.Error : null;
    }

    private async Task<AppError?> UpdateBankAsync(ParsedRow row, int id, CancellationToken cancellationToken)
    {
        var loaded = await deps.GetBank.HandleAsync(id, cancellationToken);
        if (loaded.IsFailure && loaded.Error.Type != ErrorType.NotFound)
        {
            return loaded.Error;
        }

        var cur = loaded.IsSuccess ? loaded.Value : null;
        string Pick(string key, string? fallback) => row.Has(key) ? row.Text(key) : fallback ?? string.Empty;
        var requested = new BankAccountUpdateRequest(
            Pick("bankName", cur?.BankName),
            Opt(Pick("bankBranchName", cur?.BranchName)),
            Pick("bankAccountName", cur?.AccountName),
            Pick("bankAccountNumber", cur?.AccountNumber),
            cur is null ? null : Convert.ToBase64String(cur.RowVersion));
        if (cur is not null && requested.BankName == cur.BankName && requested.BranchName == Opt(cur.BranchName)
            && requested.AccountName == cur.AccountName && requested.AccountNumber == cur.AccountNumber)
        {
            return null;
        }

        var updated = await deps.UpdateBank.HandleAsync(id, requested, cancellationToken);
        return updated.IsFailure ? updated.Error : null;
    }

    private async Task<AppError?> ChangeStatusAsync(ParsedRow row, EmployeeDto before, CancellationToken cancellationToken)
    {
        var wanted = row.Text("employmentStatus");
        if (wanted.Length == 0 || wanted == before.EmploymentStatus)
        {
            return null;
        }

        var reloaded = await deps.Get.HandleAsync(before.Id, cancellationToken);
        if (reloaded.IsFailure)
        {
            return reloaded.Error;
        }

        var version = Convert.ToBase64String(reloaded.Value.RowVersion);
        if (LeftStatuses.Contains(wanted))
        {
            if (!row.Has("exitDate") && before.ExitDate is null)
            {
                return AppError.Validation("An exit date is required for resigned or terminated employees.", "exitDate", "An exit date is required for resigned or terminated employees.");
            }

            var exitDate = row.Has("exitDate") ? Date(row.Text("exitDate")) : before.ExitDate!.Value;
            var exited = await deps.Exit.HandleAsync(new ExitEmployeeCommand(before.Id, version, exitDate, wanted), cancellationToken);
            return exited.IsFailure ? exited.Error : null;
        }

        if (LeftStatuses.Contains(before.EmploymentStatus))
        {
            var back = await deps.Reinstate.HandleAsync(new ReinstateEmployeeCommand(before.Id, version, wanted), cancellationToken);
            return back.IsFailure ? back.Error : null;
        }

        var changed = await deps.ChangeStatus.HandleAsync(new ChangeEmployeeStatusCommand(before.Id, version, wanted), cancellationToken);
        return changed.IsFailure ? changed.Error : null;
    }

    private static IEnumerable<ImportIssueDto> ToIssues(AppError error, ParsedRow row)
    {
        if (error.FieldErrors is { Count: > 0 })
        {
            foreach (var (field, messages) in error.FieldErrors)
            {
                var key = FieldToColumn.TryGetValue(field, out var mapped) ? mapped : field;
                var column = EmployeeExcelColumns.Import.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase));
                foreach (var message in messages)
                {
                    yield return column is null
                        ? Generic(message)
                        : new ImportIssueDto(column.Header, Short(ExcelCoercion.Shown(column, row.Text(column.Key))), message, null, ImportSeverity.Error);
                }
            }

            yield break;
        }

        yield return Generic(error.Message);

        ImportIssueDto Generic(string message) => new(
            "Employee Code",
            Short(row.Text("employeeCode")),
            message,
            null,
            ImportSeverity.Error);
    }

    private static string Short(string value) => value.Length > 60 ? value[..60] + "…" : value;

    private static string? Opt(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;

    private static DateOnly Date(string text) => DateOnly.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static DateOnly? DateOrNull(string text) => text.Length == 0 ? null : Date(text);
}
