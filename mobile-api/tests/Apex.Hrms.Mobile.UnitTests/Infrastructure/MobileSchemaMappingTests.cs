using System.Text.RegularExpressions;
using Apex.Hrms.Mobile.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Apex.Hrms.Mobile.UnitTests.Infrastructure;

/// <summary>
/// Checks every table and column of <see cref="MobileDbContext"/> against the CREATE TABLE scripts of the database project
/// (HR DBPJ), with the same type and nullability. No connection is ever opened, so a renamed or retyped column breaks this
/// test instead of a sign-in at run time.
/// </summary>
public sealed partial class MobileSchemaMappingTests : IDisposable
{
    private readonly MobileDbContext context = new(
        new DbContextOptionsBuilder<MobileDbContext>()
            .UseSqlServer("Server=.;Database=HR;Integrated Security=True;Encrypt=True;TrustServerCertificate=True")
            .Options);

    public void Dispose() => context.Dispose();

    public static TheoryData<string> MappedTables()
    {
        using var context = new MobileDbContext(new DbContextOptionsBuilder<MobileDbContext>().UseSqlServer("Server=.").Options);
        var data = new TheoryData<string>();
        foreach (var entity in context.Model.GetEntityTypes())
        {
            data.Add($"{entity.GetSchema()}.{entity.GetTableName()}");
        }

        return data;
    }

    [Fact]
    public void The_context_maps_exactly_the_six_tables_the_mobile_api_uses()
    {
        var tables = context.Model.GetEntityTypes().Select(entity => $"{entity.GetSchema()}.{entity.GetTableName()}").Order(StringComparer.Ordinal);

        Assert.Equal(["auth.User", "auth.UserToken", "hr.Employee", "org.Department", "org.Position", "sec.AppUser"], tables);
    }

    [Theory]
    [MemberData(nameof(MappedTables))]
    public void Every_mapped_column_exists_in_the_ssdt_table_with_the_same_type_and_nullability(string table)
    {
        var entity = context.Model.GetEntityTypes().Single(type => $"{type.GetSchema()}.{type.GetTableName()}" == table);
        var columns = SsdtColumns(table);

        foreach (var property in entity.GetProperties())
        {
            var name = property.GetColumnName();
            Assert.True(columns.TryGetValue(name, out var column), $"{table}.{name} is not a column of the SSDT table.");
            Assert.True(
                Normalize(property.GetColumnType()) == Normalize(column.Type),
                $"{table}.{name}: EF maps {property.GetColumnType()}, the SSDT table has {column.Type}.");
            Assert.True(property.IsNullable == column.Nullable, $"{table}.{name}: nullability differs from the SSDT table.");
        }
    }

    [Fact]
    public void The_session_key_is_the_primary_key_of_auth_UserToken()
    {
        var key = context.Model.FindEntityType(typeof(SessionTokenRow))!.FindPrimaryKey()!;

        Assert.Equal(["UserId", "LoginProvider", "Name"], key.Properties.Select(property => property.GetColumnName()));
    }

    private static string Normalize(string type) => type.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();

    private static Dictionary<string, (string Type, bool Nullable)> SsdtColumns(string table)
    {
        var (schema, name) = (table.Split('.')[0], table.Split('.')[1]);
        var file = Path.Combine(RepositoryRoot(), "HR DBPJ", schema, "Tables", $"{schema}.{name}.sql");
        var columns = new Dictionary<string, (string, bool)>(StringComparer.Ordinal);
        foreach (Match match in ColumnLine().Matches(File.ReadAllText(file)))
        {
            var rest = match.Groups["rest"].Value;
            columns[match.Groups["name"].Value] = (match.Groups["type"].Value, !rest.Contains("NOT NULL", StringComparison.OrdinalIgnoreCase));
        }

        return columns;
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "HR DBPJ")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("The database project folder 'HR DBPJ' was not found above the test output.");
    }

    // "    [Name]   nvarchar(450)   NOT NULL," -> name, type (with length), the rest of the line.
    [GeneratedRegex(@"^\s*\[(?<name>\w+)\]\s+(?<type>[a-z0-9]+(\s*\([^)]*\))?)(?<rest>[^\r\n]*)", RegexOptions.Multiline)]
    private static partial Regex ColumnLine();
}
