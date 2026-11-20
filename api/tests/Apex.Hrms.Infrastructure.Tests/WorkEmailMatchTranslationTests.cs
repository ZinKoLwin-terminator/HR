using Apex.Hrms.Application.Settings;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Apex.Hrms.Infrastructure.Tests;

/// <summary>
/// Linking an account to its employee finds the employee by work email, trimmed and case-insensitively. The predicate is
/// translated to SQL text here (no connection is opened), so a change that EF Core cannot translate fails in this test, not at
/// run time, and the address stays a query parameter.
/// </summary>
public sealed class WorkEmailMatchTranslationTests : IDisposable
{
    private readonly HrmsDbContext context = new(
        new DbContextOptionsBuilder<HrmsDbContext>()
            .UseSqlServer("Server=.;Database=HR;Integrated Security=True;Encrypt=True;TrustServerCertificate=True")
            .Options);

    public void Dispose() => context.Dispose();

    [Fact]
    public void The_work_email_match_runs_in_SQL_as_lower_of_the_trimmed_column_against_a_parameter()
    {
        var sql = context.Set<Employee>().Where(WorkEmailMatch.Is("  TST.Person@Example.Test ")).ToQueryString();

        Assert.Contains("WHERE LOWER(LTRIM(RTRIM([e].[WorkEmail]))) = @normalized", sql, StringComparison.Ordinal);
        Assert.Contains("@normalized nvarchar(120) = N'tst.person@example.test'", sql, StringComparison.Ordinal);
    }
}
