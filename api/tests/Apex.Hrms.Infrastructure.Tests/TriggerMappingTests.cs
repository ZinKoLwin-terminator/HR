using System.Text.RegularExpressions;
using Apex.Hrms.Domain.Grievances;
using Apex.Hrms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Apex.Hrms.Infrastructure.Tests;

/// <summary>
/// Every trigger the SSDT project puts on a mapped table must be declared in the EF model (<c>HasTrigger</c>): without it EF saves with
/// <c>OUTPUT</c> without <c>INTO</c>, which SQL Server refuses on a table with an enabled trigger, so every save to that table fails.
/// The list below is the SSDT project's; when the project folder is on this machine the test also reads its trigger files, so a new
/// trigger that is not in the list fails here. No connection is opened.
/// </summary>
public sealed partial class TriggerMappingTests : IDisposable
{
    /// <summary>Overrides where the SSDT project is looked for (read only, never built or deployed by the tests).</summary>
    public const string ProjectFolderVariable = "HRMS_SSDT_FOLDER";

    private const string DefaultProjectFolder = @"D:\New folder\HR DBPJ\HR DBPJ";

    private static readonly (string Trigger, string Table)[] Known =
    [
        ("pay.TR_PayrollRun_PaidIsFinal", "pay.PayrollRun"),
        ("pay.TR_Payslip_PaidRunIsFinal", "pay.Payslip"),
        ("pay.TR_PayslipLine_PaidRunIsFinal", "pay.PayslipLine"),
        ("pay.TR_PayrollRunWarning_PaidRunIsFinal", "pay.PayrollRunWarning"),
        ("grv.TR_Case_ClosedIsFinal", "grv.Case"),
        ("grv.TR_Case_SubmittedIsFixed", "grv.Case"),
        ("grv.TR_Attachment_Fixed", "grv.Attachment"),
    ];

    private readonly HrmsDbContext context = new(
        new DbContextOptionsBuilder<HrmsDbContext>()
            .UseSqlServer("Server=.;Database=HR;Integrated Security=True;Encrypt=True;TrustServerCertificate=True")
            .Options);

    public void Dispose() => context.Dispose();

    private IEntityType? TableOf(string qualified)
    {
        var parts = qualified.Split('.');
        return context.Model.GetEntityTypes().SingleOrDefault(type => type.GetSchema() == parts[0] && type.GetTableName() == parts[1]);
    }

    [Fact]
    public void Every_known_trigger_is_declared_on_its_mapped_table()
    {
        foreach (var (trigger, table) in Known)
        {
            var type = TableOf(table);
            Assert.True(type is not null, $"{table} is not mapped.");
            var declared = type.GetDeclaredTriggers().Select(declaredTrigger => declaredTrigger.GetDatabaseName()).ToList();
            Assert.True(declared.Contains(trigger.Split('.')[1]), $"{trigger} is not declared on {table} (declared: {string.Join(", ", declared)}).");
        }
    }

    [Fact]
    public void The_grievance_case_and_attachment_declare_their_triggers()
    {
        Assert.Equal(
            ["TR_Case_ClosedIsFinal", "TR_Case_SubmittedIsFixed"],
            context.Model.FindEntityType(typeof(GrievanceCase))!.GetDeclaredTriggers().Select(trigger => trigger.GetDatabaseName()).Order(StringComparer.Ordinal));
        Assert.Equal(
            ["TR_Attachment_Fixed"],
            context.Model.FindEntityType(typeof(GrievanceAttachment))!.GetDeclaredTriggers().Select(trigger => trigger.GetDatabaseName()));
    }

    [Fact]
    public void The_known_list_matches_the_triggers_of_the_database_project_when_it_is_present()
    {
        var folder = Environment.GetEnvironmentVariable(ProjectFolderVariable) is { Length: > 0 } configured ? configured : DefaultProjectFolder;
        if (!Directory.Exists(folder))
        {
            return;
        }

        var found = Directory.EnumerateFiles(folder, "*.sql", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .SelectMany(path => TriggerPattern().Matches(File.ReadAllText(path)).Select(match =>
                (Trigger: $"{match.Groups["schema"].Value}.{match.Groups["name"].Value}", Table: $"{match.Groups["tableSchema"].Value}.{match.Groups["table"].Value}")))
            .Order()
            .ToList();

        Assert.Equal(Known.Order(), found);
    }

    [GeneratedRegex(
        @"CREATE\s+TRIGGER\s+\[(?<schema>\w+)\]\.\[(?<name>\w+)\]\s+ON\s+\[(?<tableSchema>\w+)\]\.\[(?<table>\w+)\]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TriggerPattern();
}
