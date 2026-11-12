using System.Linq.Expressions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;

namespace Apex.Hrms.Application.Employees.Blacklist;

/// <summary>
/// One do-not-rehire entry with the employee it is about. The reason is sensitive: only callers with
/// <c>employees:ViewBlacklist</c> receive this. Active entries have no <c>LiftedOn</c>.
/// </summary>
public sealed record BlacklistEntryDto(
    int Id,
    int EmployeeId,
    string EmployeeCode,
    string EmployeeName,
    string Reason,
    DateOnly BlacklistedOn,
    string BlacklistedBy,
    DateOnly? LiftedOn,
    string? LiftedBy,
    string? LiftReason,
    byte[] RowVersion) : IIdentified;

/// <summary>POST body: the former employee and why they must not be hired again.</summary>
public sealed record BlacklistAddRequest(int EmployeeId, string? Reason);

/// <summary>Body of the lift command: the row version of the entry and why it is lifted.</summary>
public sealed record BlacklistLiftRequest(string? RowVersion, string? Reason);

/// <summary>Filter of the list: <c>active=true</c> only entries in force, <c>active=false</c> only lifted ones, none for both.</summary>
public sealed record BlacklistFilter(bool? Active = null);

public interface IBlacklistHandler
{
    Task<Result<PagedResult<BlacklistEntryDto>>> QueryAsync(PageRequest<BlacklistFilter> request, CancellationToken cancellationToken);

    Task<Result<BlacklistEntryDto>> GetAsync(int id, CancellationToken cancellationToken);

    /// <summary>The active entry of an employee; NotFound when the employee does not exist or is not on the list.</summary>
    Task<Result<BlacklistEntryDto>> GetActiveOfEmployeeAsync(int employeeId, CancellationToken cancellationToken);

    Task<Result<BlacklistEntryDto>> AddAsync(BlacklistAddRequest request, CancellationToken cancellationToken);

    Task<Result<BlacklistEntryDto>> LiftAsync(int id, BlacklistLiftRequest request, CancellationToken cancellationToken);
}

internal sealed class BlacklistEntryDescriptor : IEntityDescriptor<EmployeeBlacklistEntry>
{
    public string Label => "Do-not-rehire entry";

    public string ActivityCategory => Domain.Activity.ActivityCategories.Employee;

    /// <summary>An id only: the reason and the employee's name are never part of a message.</summary>
    public string Describe(EmployeeBlacklistEntry entity) => $"entry #{entity.Id}";
}

internal static class BlacklistSorting
{
    public static readonly IReadOnlyDictionary<string, Expression<Func<EmployeeBlacklistEntry, object>>> Keys =
        new Dictionary<string, Expression<Func<EmployeeBlacklistEntry, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = e => e.Id,
            ["employeeId"] = e => e.EmployeeId,
            ["blacklistedOn"] = e => e.BlacklistedOn,
            ["liftedOn"] = e => e.LiftedOn!,
        };

    public static Expression<Func<EmployeeBlacklistEntry, bool>> Filter(BlacklistFilter filter) => filter.Active switch
    {
        true => e => e.LiftedOn == null,
        false => e => e.LiftedOn != null,
        _ => Predicates.Always<EmployeeBlacklistEntry>(),
    };
}
