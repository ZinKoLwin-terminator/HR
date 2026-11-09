using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Employees;

namespace Apex.Hrms.Application.Employees;

// ---- Responses. Which DTO holds which field is a security decision (review H1/H2): ----
// EmployeeListItemDto and EmployeeDto carry no NationalId, date of birth, personal email or phones, address or
// emergency contact; EmployeePersonalDto (endpoint /personal) carries exactly those; BankAccountDto (endpoint
// /bank-account) carries the account number in full, every other response only a masked value.

/// <summary>One row of the employee list. Mirrors the list item of employee.models.ts; names of the organization records are resolved by the client from their own endpoints.</summary>
public sealed record EmployeeListItemDto(
    int Id,
    string EmployeeCode,
    string FullName,
    string FirstName,
    string LastName,
    string WorkEmail,
    string? PhotoUrl,
    string Gender,
    int CompanyId,
    int BranchId,
    int DepartmentId,
    int PositionId,
    string EmploymentType,
    string EmploymentStatus,
    DateOnly JoiningDate,
    int? ShiftId,
    byte[] RowVersion) : IIdentified;

/// <summary>One employee without personal data. <see cref="BankAccountMasked"/> shows only the last four digits, e.g. <c>****5678</c>.</summary>
public sealed record EmployeeDto(
    int Id,
    string EmployeeCode,
    string FirstName,
    string LastName,
    string FullName,
    string WorkEmail,
    string? PhotoUrl,
    string Gender,
    int CompanyId,
    int BranchId,
    int DepartmentId,
    int? SectionId,
    int PositionId,
    int JobGradeId,
    int? ManagerId,
    int? ShiftId,
    string EmploymentType,
    string EmploymentStatus,
    DateOnly JoiningDate,
    DateOnly? ProbationEndDate,
    DateOnly? ExitDate,
    string? BankAccountMasked,
    byte[] RowVersion) : IIdentified;

public sealed record AddressDto(string Line1, string Line2, string City, string State, string PostalCode, string Country);

public sealed record EmergencyContactDto(string Name, string Relationship, string Phone);

/// <summary>The personal data of an employee. Only <c>GET /api/employees/{id}/personal</c> returns it (permission employees:ViewSensitive).</summary>
public sealed record EmployeePersonalDto(
    int Id,
    DateOnly DateOfBirth,
    string MaritalStatus,
    string Nationality,
    string NationalId,
    string PersonalEmail,
    string Phone,
    string AlternatePhone,
    AddressDto Address,
    EmergencyContactDto EmergencyContact,
    byte[] RowVersion) : IIdentified;

/// <summary>The salary bank account in full. Only <c>/api/employees/{id}/bank-account</c> returns it. RowVersion is the one of the account row.</summary>
public sealed record BankAccountDto(
    int EmployeeId,
    string BankName,
    string BranchName,
    string AccountName,
    string AccountNumber,
    byte[] RowVersion);

public sealed record EmploymentHistoryEntryDto(
    int Id,
    int EmployeeId,
    DateOnly Date,
    string Type,
    string Title,
    string Details,
    string RecordedBy) : IIdentified;

/// <summary>The suggested code for a new employee, e.g. <c>EMP061</c>.</summary>
public sealed record NextEmployeeCodeDto(string Code);

// ---- Requests ----

public sealed record AddressRequest(string? Line1, string? Line2, string? City, string? State, string? PostalCode, string? Country);

public sealed record EmergencyContactRequest(string? Name, string? Relationship, string? Phone);

/// <summary>The fields of the employment part, shared by the create and update requests.</summary>
internal interface IEmployeeFields
{
    string? EmployeeCode { get; }

    string FirstName { get; }

    string LastName { get; }

    string Gender { get; }

    string WorkEmail { get; }

    string? PhotoUrl { get; }

    int CompanyId { get; }

    int BranchId { get; }

    int DepartmentId { get; }

    int? SectionId { get; }

    int PositionId { get; }

    int JobGradeId { get; }

    int? ManagerId { get; }

    int? ShiftId { get; }

    string EmploymentType { get; }

    DateOnly JoiningDate { get; }

    DateOnly? ProbationEndDate { get; }
}

/// <summary>The personal fields, shared by the create and the personal update requests.</summary>
internal interface IPersonalFields
{
    DateOnly DateOfBirth { get; }

    string MaritalStatus { get; }

    string Nationality { get; }

    string NationalId { get; }

    string? PersonalEmail { get; }

    string Phone { get; }

    string? AlternatePhone { get; }

    AddressRequest? Address { get; }

    EmergencyContactRequest? EmergencyContact { get; }
}

/// <summary>POST body. <c>employeeCode</c> is optional: when missing the server generates one. <c>employmentStatus</c> is the status the employee starts with (Active, Probation, On Leave or Suspended).</summary>
public sealed record EmployeeCreateRequest(
    string? EmployeeCode,
    string FirstName,
    string LastName,
    string Gender,
    string WorkEmail,
    string? PhotoUrl,
    int CompanyId,
    int BranchId,
    int DepartmentId,
    int? SectionId,
    int PositionId,
    int JobGradeId,
    int? ManagerId,
    int? ShiftId,
    string EmploymentType,
    DateOnly JoiningDate,
    DateOnly? ProbationEndDate,
    string EmploymentStatus,
    DateOnly DateOfBirth,
    string MaritalStatus,
    string Nationality,
    string NationalId,
    string? PersonalEmail,
    string Phone,
    string? AlternatePhone,
    AddressRequest? Address,
    EmergencyContactRequest? EmergencyContact) : IEmployeeFields, IPersonalFields
{
    public EmployeeDetails ToDetails() => EmployeeRequestFactory.Details(this);

    public PersonalDetails ToPersonal() => EmployeeRequestFactory.Personal(this);

    public EmploymentStatus ParseStatus() => EmploymentStatusText.Parse(EmploymentStatus.Trim());
}

/// <summary>PUT body: the employment part only. No status, no exit date (status commands) and no personal data (<c>PUT /personal</c>).</summary>
public sealed record EmployeeUpdateRequest(
    string? EmployeeCode,
    string FirstName,
    string LastName,
    string Gender,
    string WorkEmail,
    string? PhotoUrl,
    int CompanyId,
    int BranchId,
    int DepartmentId,
    int? SectionId,
    int PositionId,
    int JobGradeId,
    int? ManagerId,
    int? ShiftId,
    string EmploymentType,
    DateOnly JoiningDate,
    DateOnly? ProbationEndDate,
    string? RowVersion) : IEmployeeFields, IDetailsRequest<EmployeeDetails>, IVersionedRequest
{
    public EmployeeDetails ToDetails() => EmployeeRequestFactory.Details(this);
}

/// <summary>PUT /personal body.</summary>
public sealed record EmployeePersonalUpdateRequest(
    DateOnly DateOfBirth,
    string MaritalStatus,
    string Nationality,
    string NationalId,
    string? PersonalEmail,
    string Phone,
    string? AlternatePhone,
    AddressRequest? Address,
    EmergencyContactRequest? EmergencyContact,
    string? RowVersion) : IPersonalFields, IDetailsRequest<PersonalDetails>, IVersionedRequest
{
    public PersonalDetails ToDetails() => EmployeeRequestFactory.Personal(this);
}

/// <summary>PUT /bank-account body. <c>rowVersion</c> is the one of the existing account; it is ignored when the employee has no account yet.</summary>
public sealed record BankAccountUpdateRequest(
    string BankName,
    string? BranchName,
    string AccountName,
    string AccountNumber,
    string? RowVersion)
{
    public BankDetails ToDetails() => new BankDetails(BankName, BranchName, AccountName, AccountNumber).Normalized();
}

public sealed record EmployeeFilter(
    int? DepartmentId = null,
    int? BranchId = null,
    int? ShiftId = null,
    string? EmploymentStatus = null,
    string? EmploymentType = null,
    DateOnly? JoiningFrom = null,
    DateOnly? JoiningTo = null);

// ---- Commands (status changes never go through PUT) ----

public sealed record ExitEmployeeCommand(int Id, string? RowVersion, DateOnly? ExitDate, string? Status) : IEntityCommand;

public sealed record ChangeEmployeeStatusCommand(int Id, string? RowVersion, string? Status) : IEntityCommand;

public sealed record ReinstateEmployeeCommand(int Id, string? RowVersion, string? Status) : IEntityCommand;

internal static class EmployeeRequestFactory
{
    public static EmployeeDetails Details(IEmployeeFields fields) => new EmployeeDetails(
        fields.EmployeeCode,
        fields.FirstName,
        fields.LastName,
        GenderText.Parse(fields.Gender.Trim()),
        fields.WorkEmail,
        fields.PhotoUrl,
        fields.CompanyId,
        fields.BranchId,
        fields.DepartmentId,
        fields.SectionId,
        fields.PositionId,
        fields.JobGradeId,
        fields.ManagerId,
        fields.ShiftId,
        EmploymentTypeText.Parse(fields.EmploymentType.Trim()),
        fields.JoiningDate,
        fields.ProbationEndDate).Normalized();

    public static PersonalDetails Personal(IPersonalFields fields)
    {
        var address = fields.Address!;
        var contact = fields.EmergencyContact!;
        return new PersonalDetails(
            fields.DateOfBirth,
            MaritalStatusText.Parse(fields.MaritalStatus.Trim()),
            fields.Nationality,
            fields.NationalId,
            fields.PersonalEmail,
            fields.Phone,
            fields.AlternatePhone,
            new PostalAddress(address.Line1!, address.Line2, address.City!, address.State, address.PostalCode, address.Country!),
            new EmergencyContact(contact.Name!, EmergencyRelationshipText.Parse(contact.Relationship!.Trim()), contact.Phone!)).Normalized();
    }
}
