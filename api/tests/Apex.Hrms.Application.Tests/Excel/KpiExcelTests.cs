using System.Text.Json;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Excel;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.Application.Tests.Excel;

/// <summary>
/// The KPI Excel modules (kpi-scorecards, kpi-ranking, kpi-target-vs-actual): export only, kpi:View on top of excel:Export, the same
/// scope and filters as the KPI reports, the row limit, and never a comment.
/// </summary>
public sealed class KpiExcelTests : IDisposable
{
    private static readonly string[] Modules = ["kpi-scorecards", "kpi-ranking", "kpi-target-vs-actual"];

    private readonly ExcelModuleHarness excel = new();

    public void Dispose() => excel.Dispose();

    private TestHost Host => excel.Host;

    private static ExportRequest Filtered(object filters, string? search = null)
    {
        var json = JsonSerializer.SerializeToElement(filters);
        return new ExportRequest(
            ExportScope.Filtered,
            search,
            json.EnumerateObject().ToDictionary(property => property.Name, property => property.Value.Clone()));
    }

    [Fact]
    public void The_kpi_modules_are_export_only_and_need_kpi_view()
    {
        foreach (var code in Modules)
        {
            var info = excel.Info(code);
            Assert.True(info.CanExport, code);
            Assert.False(info.CanImport, code);
            Assert.Empty(info.Columns);
            Assert.NotEmpty(info.ExportColumns!);
            Assert.Equal(["kpi:View"], Host.Get<IEnumerable<IExcelModule>>().Single(module => module.Info.Code == code).ExportPolicies);
        }

        Host.Permissions.Only("excel:View", "excel:Export");
        Assert.All(Modules, code => Assert.False(excel.Info(code).CanExport, code));
    }

    [Fact]
    public void No_kpi_module_has_a_comment_or_evidence_column()
    {
        foreach (var code in Modules)
        {
            Assert.DoesNotContain(excel.Info(code).ExportColumns!, column =>
                column.Key.Contains("comment", StringComparison.OrdinalIgnoreCase)
                || column.Key.Contains("evidence", StringComparison.OrdinalIgnoreCase)
                || column.Key.Contains("attachment", StringComparison.OrdinalIgnoreCase)
                || column.Key.Contains("rating", StringComparison.OrdinalIgnoreCase) && column.Key != "ratingLabel");
        }
    }

    [Fact]
    public async Task An_employee_exports_only_their_own_rows_from_every_kpi_module()
    {
        var world = await Host.SeedInsightsAsync();
        Host.AsEmployee(world, "Ben");

        foreach (var code in Modules)
        {
            var rows = await excel.ExportAsync(code);
            Assert.NotEmpty(rows);
            Assert.All(rows, row => Assert.Equal("EMP-0002", row["employeeCode"]));
        }
    }

    [Fact]
    public async Task The_ranking_export_ranks_the_filtered_finalized_scorecards_with_shared_ranks()
    {
        var world = await Host.SeedInsightsAsync();
        Host.AsHr(world);

        var rows = await excel.ExportAsync("kpi-ranking", Filtered(new { periodId = world.Q4.Id }));
        var ops = await excel.ExportAsync("kpi-ranking", Filtered(new { periodId = world.Q4.Id, departmentId = world.Ops.Id }));

        Assert.Equal(["EMP-0001", "EMP-0003", "EMP-0002", "EMP-0007"], rows.Select(row => (string)row["employeeCode"]!));
        Assert.Equal([1, 1, 3, 4], rows.Select(row => (int)row["rank"]!));
        Assert.Equal([90m, 90m, 80m, 65m], rows.Select(row => (decimal)row["overallScore"]!));
        Assert.Equal(("EMP-0007", 1), ((string)Assert.Single(ops)["employeeCode"]!, (int)ops[0]["rank"]!));
    }

    [Fact]
    public async Task The_target_vs_actual_export_writes_the_figures_and_no_comment()
    {
        var world = await Host.SeedInsightsAsync();
        Host.AsHr(world);

        var rows = await excel.ExportAsync("kpi-target-vs-actual", Filtered(new { categoryId = world.Quality.Id }));

        var row = Assert.Single(rows);
        Assert.Equal(("EMP-0002", "Q-1", 50m, 40m, 80m, 20m, 16m), ((string)row["employeeCode"]!, (string)row["kpiCode"]!, (decimal)row["target"]!, (decimal)row["actual"]!, (decimal)row["achievementPct"]!, (decimal)row["weight"]!, (decimal)row["weightedScore"]!));
        Assert.DoesNotContain(row.Values, value => value is string text && text.Contains("never be exported", StringComparison.Ordinal));
        Assert.Equal(excel.Info("kpi-target-vs-actual").ExportColumns!.Select(column => column.Key).Order(StringComparer.Ordinal), row.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task The_scorecard_export_follows_the_status_filter_search_and_selected_ids()
    {
        var world = await Host.SeedInsightsAsync();
        Host.AsManager(world, "Anna", "Ben", "Cara", "Dan");

        var all = await excel.ExportAsync("kpi-scorecards");
        var submitted = await excel.ExportAsync("kpi-scorecards", Filtered(new { status = "Submitted" }));
        var searched = await excel.ExportAsync("kpi-scorecards", Filtered(new { periodId = world.Q3.Id }, search: "Cara"));
        var selected = await excel.ExportAsync("kpi-scorecards", new ExportRequest(ExportScope.Selected, SelectedIds: [world.Card("Gail", world.Q4).Id, world.Card("Eve", world.Q4).Id]));

        // Anna's own Q4, Ben, Cara, Dan (Q4 and Q3) and Gail (Anna reviews her): 8 scorecards; never Eve, Finn, Hana, Ivy or Jon.
        Assert.Equal(8, all.Count);
        Assert.Equal(["EMP-0004"], submitted.Select(row => (string)row["employeeCode"]!));
        Assert.Equal(["EMP-0003"], searched.Select(row => (string)row["employeeCode"]!));
        Assert.Equal(["EMP-0007"], selected.Select(row => (string)row["employeeCode"]!)); // Eve's is outside the scope and left out
        Assert.Equal("Anna Lead", selected[0]["reviewer"]);
        Assert.Equal(new DateOnly(2027, 1, 10), selected[0]["submissionDueDate"]);
    }

    [Fact]
    public async Task An_unknown_filter_value_is_refused()
    {
        var world = await Host.SeedInsightsAsync();
        Host.AsHr(world);

        var result = await excel.Exports.ExportAsync("kpi-ranking", Filtered(new { status = "Done" }), default);

        ResultAssert.FailedOnField(result, "status");
    }

    [Fact]
    public async Task An_export_over_the_row_limit_is_refused()
    {
        var writer = new FakeWorkbookWriter();
        using var small = new TestHost(services =>
        {
            services.AddSingleton<IWorkbookWriter>(writer);
            services.AddSingleton(new ExcelLimits { MaxExportRows = 3 });
        });
        var world = await small.SeedInsightsAsync();
        small.AsHr(world);

        var tooMany = await small.Get<IExcelExportService>().ExportAsync("kpi-target-vs-actual", new ExportRequest(ExportScope.All), default);
        var fits = await small.Get<IExcelExportService>().ExportAsync("kpi-ranking", Filtered(new { periodId = world.Q3.Id }), default);

        Assert.Equal("The export would contain more than 3 rows. Narrow the filters or the date range.", tooMany.Error.Message);
        Assert.True(fits.IsSuccess);
        Assert.Equal(3, writer.ExportedMain!.Rows.Count);
    }
}
