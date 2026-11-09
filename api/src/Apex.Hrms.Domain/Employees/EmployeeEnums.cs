using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Employees;

public enum Gender
{
    Male,
    Female,
    Other,
}

public enum MaritalStatus
{
    Unmarried,
    Married,
    Divorced,
    Widowed,
}

public enum EmploymentType
{
    FullTime,
    PartTime,
    Contract,
    Intern,
}

public enum EmploymentStatus
{
    Active,
    Probation,
    OnLeave,
    Suspended,
    Resigned,
    Terminated,
}

public enum EmergencyRelationship
{
    Spouse,
    Father,
    Mother,
    Sibling,
    Child,
    Friend,
    Other,
}

public enum DocumentType
{
    NationalId,
    Passport,
    Resume,
    OfferLetter,
    EmploymentContract,
    Certificate,
    WorkPermit,
    MedicalReport,
    Other,
}

public enum HistoryEventType
{
    Joined,
    Confirmation,
    Promotion,
    Transfer,
    SalaryRevision,
    ContractChange,
    StatusChange,
    Exit,
}

public static class GenderText
{
    public static readonly TextMap<Gender> Map = new(new Dictionary<Gender, string>
    {
        [Gender.Male] = "Male",
        [Gender.Female] = "Female",
        [Gender.Other] = "Other",
    });

    public static string ToText(Gender value) => Map.ToText(value);

    public static Gender Parse(string? text) => Map.Parse(text);

    public static bool TryParse(string? text, out Gender value) => Map.TryParse(text, out value);
}

public static class MaritalStatusText
{
    public static readonly TextMap<MaritalStatus> Map = new(new Dictionary<MaritalStatus, string>
    {
        [MaritalStatus.Unmarried] = "Single",
        [MaritalStatus.Married] = "Married",
        [MaritalStatus.Divorced] = "Divorced",
        [MaritalStatus.Widowed] = "Widowed",
    });

    public static string ToText(MaritalStatus value) => Map.ToText(value);

    public static MaritalStatus Parse(string? text) => Map.Parse(text);

    public static bool TryParse(string? text, out MaritalStatus value) => Map.TryParse(text, out value);
}

public static class EmploymentTypeText
{
    public static readonly TextMap<EmploymentType> Map = new(new Dictionary<EmploymentType, string>
    {
        [EmploymentType.FullTime] = "Full-time",
        [EmploymentType.PartTime] = "Part-time",
        [EmploymentType.Contract] = "Contract",
        [EmploymentType.Intern] = "Intern",
    });

    public static string ToText(EmploymentType value) => Map.ToText(value);

    public static EmploymentType Parse(string? text) => Map.Parse(text);

    public static bool TryParse(string? text, out EmploymentType value) => Map.TryParse(text, out value);
}

public static class EmploymentStatusText
{
    public static readonly TextMap<EmploymentStatus> Map = new(new Dictionary<EmploymentStatus, string>
    {
        [EmploymentStatus.Active] = "Active",
        [EmploymentStatus.Probation] = "Probation",
        [EmploymentStatus.OnLeave] = "On Leave",
        [EmploymentStatus.Suspended] = "Suspended",
        [EmploymentStatus.Resigned] = "Resigned",
        [EmploymentStatus.Terminated] = "Terminated",
    });

    public static string ToText(EmploymentStatus value) => Map.ToText(value);

    public static EmploymentStatus Parse(string? text) => Map.Parse(text);

    public static bool TryParse(string? text, out EmploymentStatus value) => Map.TryParse(text, out value);

    /// <summary>Resigned and Terminated: the employee has left and has an exit date.</summary>
    public static bool HasLeft(this EmploymentStatus status) =>
        status is EmploymentStatus.Resigned or EmploymentStatus.Terminated;
}

public static class EmergencyRelationshipText
{
    public static readonly TextMap<EmergencyRelationship> Map = new(new Dictionary<EmergencyRelationship, string>
    {
        [EmergencyRelationship.Spouse] = "Spouse",
        [EmergencyRelationship.Father] = "Father",
        [EmergencyRelationship.Mother] = "Mother",
        [EmergencyRelationship.Sibling] = "Sibling",
        [EmergencyRelationship.Child] = "Child",
        [EmergencyRelationship.Friend] = "Friend",
        [EmergencyRelationship.Other] = "Other",
    });

    public static string ToText(EmergencyRelationship value) => Map.ToText(value);

    public static EmergencyRelationship Parse(string? text) => Map.Parse(text);

    public static bool TryParse(string? text, out EmergencyRelationship value) => Map.TryParse(text, out value);
}

public static class DocumentTypeText
{
    public static readonly TextMap<DocumentType> Map = new(new Dictionary<DocumentType, string>
    {
        [DocumentType.NationalId] = "National ID",
        [DocumentType.Passport] = "Passport",
        [DocumentType.Resume] = "Resume",
        [DocumentType.OfferLetter] = "Offer Letter",
        [DocumentType.EmploymentContract] = "Employment Contract",
        [DocumentType.Certificate] = "Certificate",
        [DocumentType.WorkPermit] = "Work Permit",
        [DocumentType.MedicalReport] = "Medical Report",
        [DocumentType.Other] = "Other",
    });

    public static string ToText(DocumentType value) => Map.ToText(value);

    public static DocumentType Parse(string? text) => Map.Parse(text);

    public static bool TryParse(string? text, out DocumentType value) => Map.TryParse(text, out value);
}

public static class HistoryEventTypeText
{
    public static readonly TextMap<HistoryEventType> Map = new(new Dictionary<HistoryEventType, string>
    {
        [HistoryEventType.Joined] = "Joined",
        [HistoryEventType.Confirmation] = "Confirmation",
        [HistoryEventType.Promotion] = "Promotion",
        [HistoryEventType.Transfer] = "Transfer",
        [HistoryEventType.SalaryRevision] = "Salary Revision",
        [HistoryEventType.ContractChange] = "Contract Change",
        [HistoryEventType.StatusChange] = "Status Change",
        [HistoryEventType.Exit] = "Exit",
    });

    public static string ToText(HistoryEventType value) => Map.ToText(value);

    public static HistoryEventType Parse(string? text) => Map.Parse(text);

    public static bool TryParse(string? text, out HistoryEventType value) => Map.TryParse(text, out value);
}
