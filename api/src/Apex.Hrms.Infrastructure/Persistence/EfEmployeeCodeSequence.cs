using System.Globalization;
using Apex.Hrms.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Apex.Hrms.Infrastructure.Persistence;

/// <summary>
/// Reads the next number of the sequence hr.EmployeeCodeSeq. Each call consumes a number; gaps are acceptable. It uses
/// a plain command because SQL Server refuses NEXT VALUE FOR inside the subquery EF wraps around raw queries.
/// </summary>
internal sealed class EfEmployeeCodeSequence(HrmsDbContext context) : IEmployeeCodeSequence
{
    public async Task<int> NextAsync(CancellationToken cancellationToken)
    {
        await context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT NEXT VALUE FOR [hr].[EmployeeCodeSeq]";
            var value = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }
}
