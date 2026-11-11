using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Excel;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Excel;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.Application.Tests.Excel;

/// <summary>
/// The employee import goes through the real create and update handlers (in-memory fakes underneath): a row is accepted
/// only when the API would accept it, and the messages are the API's.
/// </summary>
public sealed class EmployeeExcelImportTests : IDisposable
{
    private readonly FakeFileStorage storage = new();
    private readonly FakeWorkbookReader reader = new();
    private readonly TestHost host;
    private OrgSeed org = null!;

    public EmployeeExcelImportTests() =>
        host = new TestHost(services =>
        {
            services.AddSingleton<IFileStorage>(storage);
            services.AddSingleton<IWorkbookReader>(reader);
            services.AddSingleton<IWorkbookWriter>(new FakeWorkbookWriter());
            services.AddSingleton<IImportBatchStore>(new FakeBatchStore());
            services.AddSingleton<IBatchTransactionFactory>(new FakeTransactions());
            services.AddSingleton<IActivityMute>(new NoMute());
        });

    public void Dispose() => host.Dispose();

    private IExcelImportService Imports => host.Get<IExcelImportService>();

    private static string[] Headers => [.. Columns.Select(c => c.Header)];

    private static IReadOnlyList<ExcelColumn> Columns =>
        Info.Columns;

    private static ExcelModuleInfo Info { get; } = GetInfo();

    private static ExcelModuleInfo GetInfo()
    {
        using var catalogHost = new TestHost();
        return catalogHost.Get<IExcelCatalogService>().GetModules().Value.Modules.Single(m => m.Code == "employees");
    }

    /// <summary>One valid row (fictional person) with the organization of the seeded records; <paramref name="changes"/> override single columns by key.</summary>
    private string?[] Row(string code, params (string Key, string? Value)[] changes)
    {
        var values = new Dictionary<string, string?>
        {
            ["employeeCode"] = code, ["firstName"] = "Mia", ["lastName"] = "Hartley", ["gender"] = "Female",
            ["dateOfBirth"] = "1992-04-18", ["maritalStatus"] = "Single", ["nationality"] = "Myanmar", ["nationalId"] = "NRC-" + code,
            ["workEmail"] = code.ToLowerInvariant() + "@example.test", ["phone"] = "+95 9 123 456 789",
            ["addressLine1"] = "12 Park Road", ["city"] = "Yangon", ["country"] = "Myanmar",
            ["emergencyName"] = "John Hartley", ["emergencyRelationship"] = "Spouse", ["emergencyPhone"] = "+95 9 987 654 321",
            ["employmentType"] = "Full-time", ["employmentStatus"] = "Active", ["joiningDate"] = "2024-03-01",
            ["companyCode"] = org.Company.Code, ["branchCode"] = org.Branch.Code, ["departmentCode"] = org.Department.Code,
            ["positionCode"] = org.Position.Code, ["bankName"] = "Sample Bank", ["bankAccountName"] = "Mia Hartley",
            ["bankAccountNumber"] = "1234567890",
        };
        foreach (var (key, value) in changes)
        {
            values[key] = value;
        }

        return [.. Info.Columns.Select(c => values.GetValueOrDefault(c.Key))];
    }

    private async Task<ImportPreviewDto> ValidateAsync(ImportStrategy strategy, params string?[][] rows)
    {
        org = org ?? await host.SeedOrgAsync();
        reader.Next = Result(rows);
        var inspected = await Imports.InspectAsync("employees", "employees.xlsx", ExcelTestFiles.Package(), default);
        Assert.True(inspected.IsSuccess, inspected.IsFailure ? inspected.Error.Message : null);
        var preview = await Imports.ValidateAsync("employees", new ImportValidateRequest(inspected.Value.UploadId, strategy, null), default);
        Assert.True(preview.IsSuccess, preview.IsFailure ? preview.Error.Message : null);
        return preview.Value;
    }

    private static Domain.Common.Result<ParsedSheet> Result(string?[][] rows) =>
        Domain.Common.Result.Success(ExcelTestFiles.Sheet(Headers, rows));

    private async Task<ImportPreviewDto> ValidateWithOrgAsync(ImportStrategy strategy, Func<string?[][]> rows)
    {
        org = await host.SeedOrgAsync();
        return await ValidateAsync(strategy, rows());
    }

    [Fact]
    public async Task Valid_rows_are_accepted_exactly_when_the_api_would_create_the_employee()
    {
        var preview = await ValidateWithOrgAsync(ImportStrategy.InsertOnly, () => [Row("EMP061"), Row("EMP062", ("gender", "Unknown"))]);

        Assert.Equal(ImportRowStatus.Valid, preview.Rows[0].Status);
        Assert.Equal(ImportRowAction.Insert, preview.Rows[0].Action);
        Assert.Equal(ImportRowStatus.Invalid, preview.Rows[1].Status);
        Assert.Contains("\"Unknown\" is not an allowed value", preview.Rows[1].Issues[0].Message);
    }

    [Fact]
    public async Task The_importer_applies_the_rules_of_the_api_and_reports_them_against_the_right_column()
    {
        var preview = await ValidateWithOrgAsync(
            ImportStrategy.InsertOnly,
            () =>
            [
                Row("EMP061", ("dateOfBirth", "2015-01-01")),
                Row("EMP065"),
                Row("EMP062", ("workEmail", "emp065@example.test")),
                Row("EMP063", ("employmentStatus", "Resigned"), ("exitDate", "2026-01-01")),
                Row("EMP064", ("probationEndDate", "2023-01-01")),
            ]);

        // Row 2 is created by the dry run, so row 3 collides with its work email through the uniqueness rule of the API.
        Assert.Contains("at least 18", preview.Rows[0].Issues.Single().Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Date of Birth", preview.Rows[0].Issues.Single().Field);
        Assert.Equal("Work Email", preview.Rows[2].Issues.Single().Field);
        Assert.Equal(ImportRowStatus.Valid, preview.Rows[1].Status);
        Assert.Equal("Employment Status", preview.Rows[3].Issues.Single().Field);
        Assert.Equal(ImportRowStatus.Invalid, preview.Rows[4].Status);
    }

    [Fact]
    public async Task References_are_codes_and_a_missing_one_is_reported_on_its_row_in_the_mock_wording()
    {
        var preview = await ValidateWithOrgAsync(
            ImportStrategy.InsertOnly,
            () =>
            [
                Row("EMP061", ("companyCode", "NOPE")),
                Row("EMP062", ("departmentCode", "NOPE")),
                Row("EMP063", ("managerCode", "EMP999")),
                Row("EMP064", ("shiftCode", "NOPE")),
            ]);

        Assert.Equal("Company \"NOPE\" does not exist.", preview.Rows[0].Issues.Single().Message);
        Assert.Equal($"Department \"NOPE\" does not exist in branch {org.Branch.Name}.", preview.Rows[1].Issues.Single().Message);
        Assert.Equal("Manager \"EMP999\" does not exist.", preview.Rows[2].Issues.Single().Message);
        Assert.Equal("Shift \"NOPE\" does not exist.", preview.Rows[3].Issues.Single().Message);
    }

    [Fact]
    public async Task Sensitive_values_are_masked_in_the_preview()
    {
        var preview = await ValidateWithOrgAsync(ImportStrategy.InsertOnly, () => [Row("EMP061")]);

        Assert.Equal("••••", preview.Rows[0].Values["nationalId"]);
        Assert.Equal("••••", preview.Rows[0].Values["bankAccountNumber"]);
        Assert.Equal("Mia", preview.Rows[0].Values["firstName"]);
    }

    [Fact]
    public async Task Update_only_skips_unknown_codes_and_the_update_goes_through_the_update_handlers()
    {
        org = await host.SeedOrgAsync();
        var existing = await host.Get<ICreateHandler<EmployeeCreateRequest, EmployeeDto>>()
            .HandleAsync(EmployeeFixtures.NewEmployee(org, "EMP060"), default);
        Assert.True(existing.IsSuccess, existing.IsFailure ? existing.Error.Message : null);

        var preview = await ValidateAsync(
            ImportStrategy.UpdateOnly,
            Row("EMP060", ("firstName", "Maya")),
            Row("EMP999"));

        Assert.Equal(ImportRowAction.Update, preview.Rows[0].Action);
        Assert.Equal(ImportRowStatus.Valid, preview.Rows[0].Status);
        Assert.Equal(ImportRowStatus.Invalid, preview.Rows[1].Status);
        Assert.Contains("cannot be updated", preview.Rows[1].Issues.Single().Message, StringComparison.Ordinal);
    }
}
