using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;

namespace Apex.Hrms.Application.Employees;

/// <summary>
/// Suggests employee codes such as <c>EMP061</c> from the database sequence and skips codes that are already in use
/// (people also type codes by hand, and the sequence can lag behind imported data).
/// </summary>
internal sealed class EmployeeCodeGenerator(IEmployeeCodeSequence sequence, IRepository<Employee> employees)
{
    public const string Prefix = "EMP";

    private const int MaxAttempts = 1000;

    public async Task<Result<string>> NextFreeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var number = await sequence.NextAsync(cancellationToken);
            var code = $"{Prefix}{number:D3}";
            if (code.Length > Employee.CodeMaxLength)
            {
                break;
            }

            if (!await employees.AnyAsync(employee => employee.EmployeeCode == code, cancellationToken))
            {
                return Result.Success(code);
            }
        }

        return Result.Failure<string>(AppError.Conflict("No unused employee code could be generated. Enter a code yourself."));
    }
}
