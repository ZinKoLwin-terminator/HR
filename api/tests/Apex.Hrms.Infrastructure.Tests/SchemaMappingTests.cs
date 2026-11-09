using Apex.Hrms.Domain.Organization;
using Apex.Hrms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Apex.Hrms.Infrastructure.Tests;

/// <summary>
/// Checks the EF model against the SSDT schema (the HR DBPJ SSDT project). The context is only used to build the model and
/// SQL text; no connection is ever opened.
/// </summary>
public sealed class SchemaMappingTests : IDisposable
{
    private readonly HrmsDbContext context = new(
        new DbContextOptionsBuilder<HrmsDbContext>()
            .UseSqlServer("Server=.;Database=HR;Integrated Security=True;Encrypt=True;TrustServerCertificate=True")
            .Options);

    public void Dispose() => context.Dispose();

    private IEntityType Entity<T>() => context.Model.FindEntityType(typeof(T))!;

    [Theory]
    [InlineData(typeof(Company), "org", "Company")]
    [InlineData(typeof(Branch), "org", "Branch")]
    [InlineData(typeof(Department), "org", "Department")]
    [InlineData(typeof(Section), "org", "Section")]
    [InlineData(typeof(JobGrade), "org", "JobGrade")]
    [InlineData(typeof(Position), "org", "Position")]
    [InlineData(typeof(EmployeeReference), "hr", "Employee")]
    public void Tables_map_to_the_ssdt_schema_and_name(Type entity, string schema, string table)
    {
        var type = context.Model.FindEntityType(entity)!;

        Assert.Equal(schema, type.GetSchema());
        Assert.Equal(table, type.GetTableName());
    }

    [Fact]
    public void Every_org_table_has_an_identity_key_and_a_rowversion_concurrency_token()
    {
        foreach (var type in new[] { Entity<Company>(), Entity<Branch>(), Entity<Department>(), Entity<Section>(), Entity<JobGrade>(), Entity<Position>() })
        {
            var rowVersion = type.FindProperty("RowVersion")!;
            Assert.True(rowVersion.IsConcurrencyToken, type.Name);
            Assert.Equal(ValueGenerated.OnAddOrUpdate, rowVersion.ValueGenerated);
            Assert.Equal(ValueGenerated.OnAdd, type.FindProperty("Id")!.ValueGenerated);
        }
    }

    [Fact]
    public void Status_and_type_are_non_unicode_varchar_30()
    {
        Assert.Equal("varchar(30)", Entity<Company>().FindProperty("Status")!.GetColumnType());
        Assert.Equal("varchar(30)", Entity<Branch>().FindProperty("Type")!.GetColumnType());
    }

    [Fact]
    public void Money_dates_and_small_integers_use_the_ssdt_column_types()
    {
        Assert.Equal("decimal(18,2)", Entity<JobGrade>().FindProperty("MinSalary")!.GetColumnType());
        Assert.Equal("smallint", Entity<JobGrade>().FindProperty("Level")!.GetColumnType());
        Assert.Equal("smallint", Entity<Position>().FindProperty("Headcount")!.GetColumnType());
        Assert.Equal("date", Entity<Company>().FindProperty("FoundedDate")!.GetColumnType());
        Assert.Equal("datetime2(3)", Entity<Company>().FindProperty("CreatedAtUtc")!.GetColumnType());
    }

    [Theory]
    [InlineData(typeof(Company), "Name", 100)]
    [InlineData(typeof(Company), "LegalName", 150)]
    [InlineData(typeof(Branch), "Email", 120)]
    [InlineData(typeof(Department), "CostCenter", 20)]
    [InlineData(typeof(JobGrade), "Name", 60)]
    [InlineData(typeof(Position), "Title", 100)]
    [InlineData(typeof(Section), "Description", 300)]
    public void Text_columns_have_the_ssdt_lengths(Type entity, string property, int length) =>
        Assert.Equal(length, context.Model.FindEntityType(entity)!.FindProperty(property)!.GetMaxLength());

    [Fact]
    public void Unique_constraints_match_ssdt()
    {
        AssertUnique<Company>("UQ_Company_Code", "Code");
        AssertUnique<Company>("UQ_Company_Name", "Name");
        AssertUnique<Branch>("UQ_Branch_Code", "Code");
        AssertUnique<Department>("UQ_Department_Code", "Code");
        AssertUnique<Section>("UQ_Section_Code", "Code");
        AssertUnique<JobGrade>("UQ_JobGrade_Code", "Code");
        AssertUnique<Position>("UQ_Position_Code", "Code");
    }

    [Fact]
    public void Composite_foreign_keys_keep_children_inside_their_parents()
    {
        AssertForeignKey<Department>("FK_Department_Branch", "BranchId", "CompanyId");
        AssertForeignKey<Position>("FK_Position_Section", "SectionId", "DepartmentId");
        AssertForeignKey<Branch>("FK_Branch_Company", "CompanyId");
        AssertForeignKey<Position>("FK_Position_Position_ReportsToPositionId", "ReportsToPositionId");
    }

    [Fact]
    public void Foreign_keys_never_cascade_deletes()
    {
        foreach (var type in context.Model.GetEntityTypes())
        {
            Assert.All(type.GetForeignKeys(), key => Assert.Equal(DeleteBehavior.NoAction, key.DeleteBehavior));
        }
    }

    [Fact]
    public void Sorting_by_a_boxed_key_translates_to_sql_with_a_stable_id_tiebreak()
    {
        var sql = context.Set<JobGrade>().AsNoTracking()
            .OrderByDescending(Key<JobGrade>(g => g.Level)).ThenBy(g => g.Id)
            .Skip(10).Take(5)
            .ToQueryString();

        Assert.Contains("ORDER BY [j].[Level] DESC, [j].[Id]", sql, StringComparison.Ordinal);
        Assert.Contains("[org].[JobGrade]", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Search_predicate_from_the_application_layer_translates_to_like()
    {
        var predicate = Apex.Hrms.Application.Predicates.ContainsAny<Company>("alp", c => c.Code, c => c.Name);

        var sql = context.Set<Company>().AsNoTracking().Where(predicate).ToQueryString();

        Assert.Contains("LIKE", sql, StringComparison.Ordinal);
        Assert.Contains("[org].[Company]", sql, StringComparison.Ordinal);
    }

    private static System.Linq.Expressions.Expression<Func<T, object>> Key<T>(System.Linq.Expressions.Expression<Func<T, object>> key) => key;

    private void AssertUnique<T>(string name, params string[] columns)
    {
        var index = Entity<T>().GetIndexes().SingleOrDefault(candidate => candidate.GetDatabaseName() == name);
        Assert.NotNull(index);
        Assert.True(index.IsUnique);
        Assert.Equal(columns, index.Properties.Select(property => property.Name));
    }

    private void AssertForeignKey<T>(string name, params string[] columns)
    {
        var key = Entity<T>().GetForeignKeys().SingleOrDefault(candidate => candidate.GetConstraintName() == name);
        Assert.NotNull(key);
        Assert.Equal(columns, key.Properties.Select(property => property.Name));
    }
}
