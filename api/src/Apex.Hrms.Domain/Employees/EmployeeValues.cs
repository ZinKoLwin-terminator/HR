using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Employees;

/// <summary>Home address. Personal data: only the sensitive employee DTO exposes it.</summary>
public sealed record PostalAddress(
    string Line1,
    string? Line2,
    string City,
    string? State,
    string? PostalCode,
    string Country)
{
    public PostalAddress Normalized() => this with
    {
        Line1 = TextNormalization.Clean(Line1),
        Line2 = TextNormalization.CleanOptional(Line2),
        City = TextNormalization.Clean(City),
        State = TextNormalization.CleanOptional(State),
        PostalCode = TextNormalization.CleanOptional(PostalCode),
        Country = TextNormalization.Clean(Country),
    };
}

/// <summary>Who to call in an emergency. Personal data.</summary>
public sealed record EmergencyContact(string Name, EmergencyRelationship Relationship, string Phone)
{
    public EmergencyContact Normalized() => this with
    {
        Name = TextNormalization.Clean(Name),
        Phone = TextNormalization.Clean(Phone),
    };
}

/// <summary>Where the salary is paid. The account number is never logged or shown in full without the bank permission.</summary>
public sealed record BankDetails(string BankName, string? BranchName, string AccountName, string AccountNumber)
{
    public const int MinAccountDigits = 8;
    public const int MaxAccountDigits = 20;

    public BankDetails Normalized() => this with
    {
        BankName = TextNormalization.Clean(BankName),
        BranchName = TextNormalization.CleanOptional(BranchName),
        AccountName = TextNormalization.Clean(AccountName),
        AccountNumber = TextNormalization.Clean(AccountNumber),
    };

    public Result Validate()
    {
        var details = Normalized();
        return new Violations()
            .Require("bankName", "Bank name", details.BankName)
            .Require("accountName", "Account holder name", details.AccountName)
            .Require("accountNumber", "Account number", details.AccountNumber)
            .AddIf(
                details.AccountNumber.Length > 0
                    && (details.AccountNumber.Length is < MinAccountDigits or > MaxAccountDigits
                        || !details.AccountNumber.All(char.IsAsciiDigit)),
                "accountNumber",
                $"Account number must be {MinAccountDigits} to {MaxAccountDigits} digits.")
            .ToResult();
    }
}

/// <summary>The part of an employee that is not personal data: who they are at work and where they sit in the organization.</summary>
/// <param name="EmployeeCode">Null on create asks the server to generate one; an explicit code is stored upper case.</param>
/// <param name="ManagerId">The employee this one reports to (hr.Employee.ManagerEmployeeId).</param>
public sealed record EmployeeDetails(
    string? EmployeeCode,
    string FirstName,
    string LastName,
    Gender Gender,
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
    EmploymentType EmploymentType,
    DateOnly JoiningDate,
    DateOnly? ProbationEndDate)
{
    /// <summary>Trims text, upper-cases the code, lower-cases the work email. Idempotent.</summary>
    public EmployeeDetails Normalized() => this with
    {
        EmployeeCode = TextNormalization.CleanOptional(EmployeeCode)?.ToUpperInvariant(),
        FirstName = TextNormalization.Clean(FirstName),
        LastName = TextNormalization.Clean(LastName),
        WorkEmail = TextNormalization.Clean(WorkEmail).ToLowerInvariant(),
        PhotoUrl = TextNormalization.CleanOptional(PhotoUrl),
    };
}

/// <summary>Personal data (security review H2): readable only through the sensitive endpoint.</summary>
public sealed record PersonalDetails(
    DateOnly DateOfBirth,
    MaritalStatus MaritalStatus,
    string Nationality,
    string NationalId,
    string? PersonalEmail,
    string Phone,
    string? AlternatePhone,
    PostalAddress Address,
    EmergencyContact EmergencyContact)
{
    public PersonalDetails Normalized() => this with
    {
        Nationality = TextNormalization.Clean(Nationality),
        NationalId = TextNormalization.Clean(NationalId),
        PersonalEmail = TextNormalization.CleanOptional(PersonalEmail)?.ToLowerInvariant(),
        Phone = TextNormalization.Clean(Phone),
        AlternatePhone = TextNormalization.CleanOptional(AlternatePhone),
        Address = Address.Normalized(),
        EmergencyContact = EmergencyContact.Normalized(),
    };
}

/// <summary>When and by whom a change is made; the history entries it leaves carry both.</summary>
public sealed record ChangeContext(DateOnly Today, string RecordedBy);

/// <summary>What the application resolved about the position change of an update, for the history text.</summary>
public sealed record PlacementNotes(string? PreviousPositionTitle, string? NewPositionTitle, bool IsPromotion)
{
    public static PlacementNotes None { get; } = new(null, null, false);
}
