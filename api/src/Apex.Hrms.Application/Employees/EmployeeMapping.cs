using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Employees;

namespace Apex.Hrms.Application.Employees;

/// <summary>
/// Turns the aggregate into its three response shapes. What each shape contains is the PII decision of the module:
/// do not add a personal field to <see cref="EmployeeDto"/> or <see cref="EmployeeListItemDto"/>.
/// </summary>
internal sealed class EmployeeMapping :
    IEntityDescriptor<Employee>,
    IDtoMapper<Employee, EmployeeDto>,
    IDtoMapper<Employee, EmployeeListItemDto>,
    IDtoMapper<Employee, EmployeePersonalDto>
{
    public string Label => "Employee";

    public string Describe(Employee entity) => $"{FullName(entity)} ({entity.EmployeeCode})";

    public static string FullName(Employee entity) => $"{entity.FirstName} {entity.LastName}".Trim();

    public EmployeeDto ToDto(Employee entity) => new(
        entity.Id,
        entity.EmployeeCode,
        entity.FirstName,
        entity.LastName,
        FullName(entity),
        entity.WorkEmail,
        entity.PhotoUrl,
        GenderText.ToText(entity.Gender),
        entity.CompanyId,
        entity.BranchId,
        entity.DepartmentId,
        entity.SectionId,
        entity.PositionId,
        entity.JobGradeId,
        entity.ManagerEmployeeId,
        entity.ShiftId,
        EmploymentTypeText.ToText(entity.EmploymentType),
        EmploymentStatusText.ToText(entity.Status),
        entity.JoiningDate,
        entity.ProbationEndDate,
        entity.ExitDate,
        entity.BankAccount?.MaskedAccountNumber,
        entity.RowVersion);

    EmployeeListItemDto IDtoMapper<Employee, EmployeeListItemDto>.ToDto(Employee entity) => new(
        entity.Id,
        entity.EmployeeCode,
        FullName(entity),
        entity.FirstName,
        entity.LastName,
        entity.WorkEmail,
        entity.PhotoUrl,
        GenderText.ToText(entity.Gender),
        entity.CompanyId,
        entity.BranchId,
        entity.DepartmentId,
        entity.PositionId,
        EmploymentTypeText.ToText(entity.EmploymentType),
        EmploymentStatusText.ToText(entity.Status),
        entity.JoiningDate,
        entity.ShiftId,
        entity.RowVersion);

    EmployeePersonalDto IDtoMapper<Employee, EmployeePersonalDto>.ToDto(Employee entity) => new(
        entity.Id,
        entity.DateOfBirth,
        MaritalStatusText.ToText(entity.MaritalStatus),
        entity.Nationality,
        entity.NationalId,
        entity.PersonalEmail ?? string.Empty,
        entity.Phone,
        entity.AlternatePhone ?? string.Empty,
        new AddressDto(
            entity.Address.Line1,
            entity.Address.Line2 ?? string.Empty,
            entity.Address.City,
            entity.Address.State ?? string.Empty,
            entity.Address.PostalCode ?? string.Empty,
            entity.Address.Country),
        new EmergencyContactDto(
            entity.EmergencyContact.Name,
            EmergencyRelationshipText.ToText(entity.EmergencyContact.Relationship),
            entity.EmergencyContact.Phone),
        entity.RowVersion);

    public static BankAccountDto ToBankDto(Employee employee, EmployeeBankAccount account) => new(
        employee.Id,
        account.BankName,
        account.BranchName ?? string.Empty,
        account.AccountName,
        account.AccountNumber,
        account.RowVersion);

    public static EmploymentHistoryEntryDto ToHistoryDto(EmploymentHistoryEntry entry) => new(
        entry.Id,
        entry.EmployeeId,
        entry.Date,
        HistoryEventTypeText.ToText(entry.Type),
        entry.Title,
        entry.Details,
        entry.RecordedBy);
}
