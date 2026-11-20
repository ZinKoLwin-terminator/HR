using System.Text.RegularExpressions;
using Apex.Hrms.Application;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Attendance.Shifts;
using Apex.Hrms.Application.Organization;
using Apex.Hrms.Application.Performance.Categories;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.Infrastructure.Tests;

/// <summary>
/// The lookups readable by any signed-in user (<c>/api/organization/lookup</c>, <c>/api/shifts/lookup</c>) and the KPI category
/// picker (<c>/api/kpi/categories/options</c>, <c>kpi:View</c>) run their real queries through the EF read model and are translated
/// to SQL text, never executed. The columns each SELECT reads are checked against an allowlist per table, so a new column (a salary
/// band, an employee id, contact data) cannot slip into what these wide audiences receive. No connection is opened.
/// </summary>
public sealed partial class LookupQueryTranslationTests : IDisposable
{
    private readonly HrmsDbContext context = new(
        new DbContextOptionsBuilder<HrmsDbContext>()
            .UseSqlServer("Server=.;Database=HR;Integrated Security=True;Encrypt=True;TrustServerCertificate=True")
            .Options);

    public void Dispose() => context.Dispose();

    private (ServiceProvider Provider, SqlCapture Capture) Arrange()
    {
        var capture = new SqlCapture(new EfReadModel(context));
        var provider = new ServiceCollection()
            .AddSingleton<IReadModel>(capture)
            .AddApplication()
            .BuildServiceProvider();
        return (provider, capture);
    }

    /// <summary>
    /// The columns of the outer SELECT list, by table (<c>[schema].[Table]</c>). Every item of the list must be a plain
    /// <c>[alias].[Column]</c> (optionally <c>AS [name]</c>) of a table in the FROM or JOIN clauses, so no expression can hide a column.
    /// </summary>
    private static Dictionary<string, string[]> SelectedColumns(string sql)
    {
        var select = SelectList().Match(sql);
        Assert.True(select.Success, $"No SELECT ... FROM in:\n{sql}");
        var tables = TableAlias().Matches(sql).ToDictionary(match => match.Groups["alias"].Value, match => match.Groups["table"].Value);

        var columns = new List<(string Table, string Column)>();
        foreach (var item in select.Groups["list"].Value.Split(',', StringSplitOptions.TrimEntries))
        {
            var column = ColumnItem().Match(item);
            Assert.True(column.Success, $"The SELECT item \"{item}\" is not a plain column in:\n{sql}");
            Assert.True(tables.TryGetValue(column.Groups["alias"].Value, out var table), $"Unknown alias in \"{item}\" in:\n{sql}");
            columns.Add((table!, column.Groups["column"].Value));
        }

        return columns
            .GroupBy(column => column.Table)
            .ToDictionary(group => group.Key, group => group.Select(column => column.Column).Order(StringComparer.Ordinal).ToArray());
    }

    private static void AssertColumns(string sql, Dictionary<string, string[]> expected)
    {
        var actual = SelectedColumns(sql);
        Assert.True(
            expected.Keys.Order(StringComparer.Ordinal).SequenceEqual(actual.Keys.Order(StringComparer.Ordinal)),
            $"Tables read: {string.Join(", ", actual.Keys)} in:\n{sql}");
        foreach (var (table, columns) in expected)
        {
            var wanted = columns.Order(StringComparer.Ordinal).ToArray();
            Assert.True(wanted.SequenceEqual(actual[table]), $"{table}: expected {string.Join(", ", wanted)} but read {string.Join(", ", actual[table])} in:\n{sql}");
        }
    }

    [Fact]
    public async Task The_organization_lookup_is_six_statements_that_read_exactly_the_contract_columns()
    {
        var (provider, capture) = Arrange();
        using (provider)
        {
            await provider.GetRequiredService<IOrganizationLookupHandler>().HandleAsync(default);
        }

        Assert.Equal(6, capture.Statements.Count);
        AssertColumns(capture.Statements[0], new() { ["[org].[Company]"] = ["Id", "Code", "Name", "Status"] });
        AssertColumns(capture.Statements[1], new() { ["[org].[Branch]"] = ["Id", "CompanyId", "Code", "Name", "Status"] });
        AssertColumns(capture.Statements[2], new() { ["[org].[Department]"] = ["Id", "CompanyId", "BranchId", "Code", "Name", "Status"] });
        AssertColumns(capture.Statements[3], new() { ["[org].[Section]"] = ["Id", "DepartmentId", "Code", "Name", "Status"] });
        AssertColumns(capture.Statements[4], new() { ["[org].[Position]"] = ["Id", "DepartmentId", "SectionId", "JobGradeId", "Code", "Title", "Status"] });
        AssertColumns(capture.Statements[5], new() { ["[org].[JobGrade]"] = ["Id", "Code", "Name", "Level", "Status"] });
        Assert.All(capture.Statements, sql => Assert.Contains("ORDER BY", sql, StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_shift_lookup_is_one_statement_that_reads_exactly_the_contract_columns_and_the_work_days()
    {
        var (provider, capture) = Arrange();
        using (provider)
        {
            await provider.GetRequiredService<IShiftLookupHandler>().HandleAsync(default);
        }

        var sql = Assert.Single(capture.Statements);

        // ShiftWorkDay: the day, and ShiftId, the key EF reads to put each day under its shift (not returned to the caller).
        AssertColumns(sql, new()
        {
            ["[att].[Shift]"] = ["Id", "Code", "Name", "StartTime", "EndTime", "IsNightShift", "Status"],
            ["[att].[ShiftWorkDay]"] = ["DayOfWeek", "ShiftId"],
        });
        Assert.Contains("ORDER BY", sql, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_kpi_category_picker_is_one_statement_that_reads_exactly_the_contract_columns()
    {
        var (provider, capture) = Arrange();
        using (provider)
        {
            await provider.GetRequiredService<IKpiCategoryOptionsHandler>().HandleAsync(default);
        }

        var sql = Assert.Single(capture.Statements);
        AssertColumns(sql, new() { ["[kpi].[Category]"] = ["Id", "Code", "Name", "Status"] });
        Assert.Contains("ORDER BY", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void The_column_parser_refuses_an_expression_in_the_select_list()
    {
        const string sql = "SELECT [c].[Id], COALESCE([c].[TaxId], N'') AS [x]\nFROM [org].[Company] AS [c]";

        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => SelectedColumns(sql));
    }

    // The outer SELECT list: from the first SELECT to the first FROM at the start of a line (EF writes each clause on its own line).
    [GeneratedRegex(@"^SELECT (?<list>.+?)\r?\nFROM ", RegexOptions.Singleline | RegexOptions.Multiline)]
    private static partial Regex SelectList();

    [GeneratedRegex(@"(?<table>\[\w+\]\.\[\w+\]) AS \[(?<alias>\w+)\]")]
    private static partial Regex TableAlias();

    [GeneratedRegex(@"^\[(?<alias>\w+)\]\.\[(?<column>\w+)\](?: AS \[\w+\])?$")]
    private static partial Regex ColumnItem();

    /// <summary>The EF sources; "running" a query records its SQL text and returns no rows.</summary>
    private sealed class SqlCapture(IReadModel inner) : IReadModel
    {
        public List<string> Statements { get; } = [];

        public IQueryable<TEntity> Query<TEntity>()
            where TEntity : Entity =>
            inner.Query<TEntity>();

        public Task<List<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken cancellationToken)
        {
            Statements.Add(query.ToQueryString());
            return Task.FromResult(new List<T>());
        }

        public Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
