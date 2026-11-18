using System.Globalization;
using Apex.Hrms.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Apex.Hrms.Infrastructure.Persistence;

/// <summary>
/// Reads the next number of the sequence grv.CaseNoSeq. Each call consumes a number; gaps are acceptable. It uses a plain command
/// because SQL Server refuses NEXT VALUE FOR inside the subquery EF wraps around raw queries (as for the leave numbers).
/// </summary>
internal sealed class EfGrievanceCaseNumberSequence(HrmsDbContext context) : IGrievanceCaseNumberSequence
{
    public async Task<int> NextAsync(CancellationToken cancellationToken)
    {
        await context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.Transaction = context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = "SELECT NEXT VALUE FOR [grv].[CaseNoSeq]";
            var value = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }
}
