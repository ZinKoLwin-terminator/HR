using System.Linq.Expressions;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Payroll;

namespace Apex.Hrms.Application.Payroll.Payslips;

/// <summary>One earning or deduction of a payslip. Mirrors PayLine in payroll.models.ts; <c>basis</c> says how the amount was worked out, e.g. "10% of basic".</summary>
public sealed record PayLineDto(string Code, string Name, decimal Amount, string Basis);

/// <summary>
/// Mirrors the Payslip interface in payroll.models.ts. DEMONSTRATION figures, not statutory calculations. A payslip is salary
/// data (personal data): only callers with payroll access receive it. <c>accountMasked</c> is the only account detail it
/// holds: four bullets, a space and the last four digits. The full number never leaves the employee's bank record.
/// </summary>
public sealed record PayslipDto(
    int Id,
    int RunId,
    string PeriodName,
    int EmployeeId,
    string EmployeeCode,
    string EmployeeName,
    int DepartmentId,
    string DepartmentName,
    string PositionTitle,
    string CompanyName,
    string BankName,
    string AccountMasked,
    decimal BasicSalary,
    IReadOnlyList<PayLineDto> Earnings,
    IReadOnlyList<PayLineDto> Deductions,
    decimal GrossSalary,
    decimal TotalDeductions,
    decimal NetSalary,
    int PeriodDays,
    int DaysEmployed,
    int WorkingDays,
    decimal UnpaidLeaveDays,
    int OvertimeMinutes,
    DateOnly GeneratedOn) : IIdentified;

/// <summary>Filters of the payslip list: a run's payslips and an employee's payslips are this list filtered by <c>runId</c> or <c>employeeId</c>.</summary>
public sealed record PayslipFilter(int? RunId = null, int? EmployeeId = null, int? DepartmentId = null);

internal sealed class PayslipMapping : IDtoMapper<Payslip, PayslipDto>
{
    public PayslipDto ToDto(Payslip entity) => new(
        entity.Id,
        entity.PayrollRunId,
        entity.PeriodName,
        entity.EmployeeId,
        entity.EmployeeCode,
        entity.EmployeeName,
        entity.DepartmentId,
        entity.DepartmentName,
        entity.PositionTitle,
        entity.CompanyName,
        entity.BankName,
        entity.AccountMasked,
        entity.BasicSalary,
        [.. entity.Earnings.Select(Line)],
        [.. entity.Deductions.Select(Line)],
        entity.GrossSalary,
        entity.TotalDeductions,
        entity.NetSalary,
        entity.PeriodDays,
        entity.DaysEmployed,
        entity.WorkingDays,
        entity.UnpaidLeaveDays,
        entity.OvertimeMinutes,
        entity.GeneratedOn);

    private static PayLineDto Line(PayslipLine line) => new(line.Code, line.Name, line.Amount, line.Basis);
}

internal sealed class PayslipQuerySpecification : IQuerySpecification<Payslip, PayslipFilter>
{
    public IReadOnlyDictionary<string, Expression<Func<Payslip, object>>> SortKeys { get; } =
        new Dictionary<string, Expression<Func<Payslip, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = p => p.Id,
            ["runId"] = p => p.PayrollRunId,
            ["employeeCode"] = p => p.EmployeeCode,
            ["employeeName"] = p => p.EmployeeName,
            ["departmentName"] = p => p.DepartmentName,
            ["grossSalary"] = p => p.GrossSalary,
            ["netSalary"] = p => p.NetSalary,
            ["generatedOn"] = p => p.GeneratedOn,
        };

    public Expression<Func<Payslip, bool>> Search(string term) =>
        Predicates.ContainsAny<Payslip>(term, p => p.EmployeeName, p => p.EmployeeCode);

    public Expression<Func<Payslip, bool>> Filter(PayslipFilter filter) => Predicates.Always<Payslip>()
        .And(filter.RunId is { } runId ? p => p.PayrollRunId == runId : Predicates.Always<Payslip>())
        .And(filter.EmployeeId is { } employeeId ? p => p.EmployeeId == employeeId : Predicates.Always<Payslip>())
        .And(filter.DepartmentId is { } departmentId ? p => p.DepartmentId == departmentId : Predicates.Always<Payslip>());
}

/// <summary>Reads one payslip with its lines, inside the access scope of the caller (outside it the payslip does not exist). A payslip has no row version: it is never changed.</summary>
internal sealed class PayslipGetHandler(
    IRepository<Payslip> repository,
    IAccessScope<Payslip> scope,
    ICurrentUser user,
    PayslipMapping mapper) : IGetByIdHandler<PayslipDto>
{
    public async Task<Result<PayslipDto>> HandleAsync(int id, CancellationToken cancellationToken)
    {
        var payslip = await repository.FindAsync(id, scope.PredicateFor(user), cancellationToken);
        return payslip is null
            ? Result.Failure<PayslipDto>(NotFoundError.For("Payslip", id))
            : Result.Success(mapper.ToDto(payslip));
    }
}
