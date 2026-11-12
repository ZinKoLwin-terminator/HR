using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Excel;
using Apex.Hrms.Application.Security;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Excel;
using Apex.Hrms.Domain.Security;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.Application.Tests.Excel;

/// <summary>Security re-review L3: an import batch (history, detail, error report, execute) belongs to its uploader and to HR Administrators.</summary>
public sealed class ExcelBatchVisibilityTests : IDisposable
{
    private static readonly string[] Headers = ["Code", "Name"];

    private readonly FakeWorkbookReader reader = new();
    private readonly FakeBatchStore batches = new();
    private readonly TestHost host;
    private readonly SettingsHostForExcel settings;

    public ExcelBatchVisibilityTests()
    {
        host = new TestHost(services =>
        {
            services.AddSingleton<IFileStorage>(new FakeFileStorage());
            services.AddSingleton<IWorkbookReader>(reader);
            services.AddSingleton<IWorkbookWriter>(new FakeWorkbookWriter());
            services.AddSingleton<IImportBatchStore>(batches);
            services.AddSingleton<IBatchTransactionFactory>(new FakeTransactions());
            services.AddSingleton<IActivityMute>(new NoMute());
            services.AddSingleton<IExcelModule>(new ScriptedModule());
            services.AddSingleton(new ExcelLimits { MaxFileBytes = 5000 });
        });
        settings = new SettingsHostForExcel(host);
    }

    private IExcelImportService Imports => host.Get<IExcelImportService>();

    public void Dispose() => host.Dispose();

    private sealed class SettingsHostForExcel(TestHost host)
    {
        public int SeedAccount(string roleName, bool builtIn, string email)
        {
            var role = ResultAssert.Succeeded(AppRole.Create(new RoleDetails(roleName, "Fictional role"), RecordStatus.Active));
            if (builtIn)
            {
                typeof(AppRole).GetProperty(nameof(AppRole.IsSystem))!.SetValue(role, true);
            }

            host.Repository<AppRole>().Add(role);
            var account = AppAccount.Create(null, "TST- Person", email, role.Id, $"seed-{email}", AccountStatus.Active);
            host.Repository<AppAccount>().Add(account);
            return account.Id;
        }
    }

    private async Task<string> ValidateAsUploaderAsync()
    {
        host.User.UserId = 11;
        reader.Next = Result.Success(ExcelTestFiles.Sheet(Headers, [["A1", "Alpha"]]));
        var inspected = ResultAssert.Succeeded(await Imports.InspectAsync("items", "items.xlsx", ExcelTestFiles.Package(), default));
        var preview = ResultAssert.Succeeded(await Imports.ValidateAsync("items", new ImportValidateRequest(inspected.UploadId, ImportStrategy.InsertOnly, null), default));
        return preview.BatchId;
    }

    [Fact]
    public async Task The_uploader_sees_their_own_batch_in_history_detail_and_report()
    {
        var batchId = await ValidateAsUploaderAsync();

        Assert.Single((await Imports.GetHistoryAsync(0, 10, default)).Value.Items);
        Assert.True((await Imports.GetBatchAsync(batchId, default)).IsSuccess);
        Assert.True((await Imports.GetErrorReportAsync(batchId, default)).IsSuccess);
    }

    [Fact]
    public async Task Another_users_batch_is_not_found_and_missing_from_their_history()
    {
        var batchId = await ValidateAsUploaderAsync();
        host.User.UserId = 22;

        Assert.Empty((await Imports.GetHistoryAsync(0, 10, default)).Value.Items);
        Assert.Equal(ErrorType.NotFound, (await Imports.GetBatchAsync(batchId, default)).Error.Type);
        Assert.Equal(ErrorType.NotFound, (await Imports.GetErrorReportAsync(batchId, default)).Error.Type);
        Assert.Equal(ErrorType.NotFound, (await Imports.ModuleOfBatchAsync(batchId, default)).Error.Type);
        Assert.Equal(ErrorType.NotFound, (await Imports.ExecuteAsync(batchId, ImportMode.Partial, default)).Error.Type);
    }

    [Fact]
    public async Task An_hr_administrator_sees_every_batch()
    {
        var batchId = await ValidateAsUploaderAsync();
        host.User.UserId = settings.SeedAccount(SecurityPolicy.AdministratorRoleName, builtIn: true, "admin@example.test");

        Assert.Single((await Imports.GetHistoryAsync(0, 10, default)).Value.Items);
        Assert.True((await Imports.GetBatchAsync(batchId, default)).IsSuccess);
        Assert.True((await Imports.ModuleOfBatchAsync(batchId, default)).IsSuccess);
    }
}
