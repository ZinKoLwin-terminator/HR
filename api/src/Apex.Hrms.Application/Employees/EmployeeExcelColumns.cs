using Apex.Hrms.Application.Excel;

namespace Apex.Hrms.Application.Employees;

/// <summary>
/// The columns of the employee import template and export. Ported from <c>employee-excel.module.ts</c> of the frontend
/// mock: the same headers, required markers, limits and examples.
/// </summary>
internal static class EmployeeExcelColumns
{
    public const string ModuleCode = "employees";

    private const string Code = "^[A-Z0-9][A-Z0-9-]*$";
    private const string CodeMessage = "must use capital letters, numbers and dashes only.";

    private static readonly string[] Genders = ["Male", "Female", "Other"];
    private static readonly string[] Marital = ["Single", "Married", "Divorced", "Widowed"];
    private static readonly string[] Relationships = ["Spouse", "Father", "Mother", "Sibling", "Child", "Friend", "Other"];
    private static readonly string[] EmploymentTypes = ["Full-time", "Part-time", "Contract", "Intern"];
    private static readonly string[] EmploymentStatuses = ["Active", "Probation", "On Leave", "Suspended", "Resigned", "Terminated"];

    public static IReadOnlyList<ExcelColumn> Import { get; } =
    [
        Col("employeeCode", "Employee Code", "Unique code of the employee. Existing employees are matched by this code.", "EMP061", c => c with
        {
            Required = true,
            MaxLength = 12,
            Uppercase = true,
            Pattern = Code,
            PatternMessage = $"Employee Code {CodeMessage}",
            Aliases = ["Employee ID", "Emp Code"],
        }),
        Col("firstName", "First Name", "Given name.", "Mia", c => c with { Required = true, MaxLength = 60 }),
        Col("lastName", "Last Name", "Family name.", "Hartley", c => c with { Required = true, MaxLength = 60 }),
        Col("gender", "Gender", "Gender.", "Female", c => c with { Required = true, Type = ExcelColumnType.Enum, Allowed = Genders }),
        Col("dateOfBirth", "Date of Birth", "Employees must be at least 18 years old.", "1992-04-18", c => c with { Required = true, Type = ExcelColumnType.Date }),
        Col("maritalStatus", "Marital Status", "Marital status.", "Single", c => c with { Required = true, Type = ExcelColumnType.Enum, Allowed = Marital }),
        Col("nationality", "Nationality", "Nationality.", "Myanmar", c => c with { Required = true, MaxLength = 60 }),
        Col("nationalId", "National ID", "National ID or passport number.", "NRC-123456", c => c with { Required = true, MaxLength = 40, Sensitive = true }),
        Col("workEmail", "Work Email", "Company email address (must be unique).", "mia.hartley@apexhorizon.example", c => c with { Required = true, MaxLength = 120, Type = ExcelColumnType.Email }),
        Col("personalEmail", "Personal Email", "Private email address (optional).", "mia@example.com", c => c with { MaxLength = 120, Sensitive = true, Type = ExcelColumnType.Email }),
        Col("phone", "Mobile Phone", "Mobile number, e.g. +95 9 123 456 789.", "+95 9 123 456 789", c => c with { Required = true, Type = ExcelColumnType.Phone }),
        Col("alternatePhone", "Alternate Phone", "Second phone number (optional).", "+95 1 234 567", c => c with { Sensitive = true, Type = ExcelColumnType.Phone }),
        Col("addressLine1", "Address Line 1", "Street address.", "12 Park Road", c => c with { Required = true, MaxLength = 120, Sensitive = true }),
        Col("addressLine2", "Address Line 2", "Apartment, floor or landmark (optional).", "Floor 3", c => c with { MaxLength = 120, Sensitive = true }),
        Col("city", "City", "City.", "Yangon", c => c with { Required = true, MaxLength = 60 }),
        Col("state", "State / Region", "State or region (optional).", "Yangon Region", c => c with { MaxLength = 60 }),
        Col("postalCode", "Postal Code", "Postal code (optional).", "11181", c => c with { MaxLength = 12 }),
        Col("country", "Country", "Country.", "Myanmar", c => c with { Required = true, MaxLength = 60 }),
        Col("emergencyName", "Emergency Contact Name", "Who to contact in an emergency.", "John Hartley", c => c with { Required = true, MaxLength = 80, Sensitive = true }),
        Col("emergencyRelationship", "Emergency Contact Relationship", "Relationship to the employee.", "Spouse", c => c with { Required = true, Sensitive = true, Type = ExcelColumnType.Enum, Allowed = Relationships }),
        Col("emergencyPhone", "Emergency Contact Phone", "Phone number of the emergency contact.", "+95 9 987 654 321", c => c with { Required = true, Sensitive = true, Type = ExcelColumnType.Phone }),
        Col("employmentType", "Employment Type", "Type of employment.", "Full-time", c => c with { Required = true, Type = ExcelColumnType.Enum, Allowed = EmploymentTypes }),
        Col("employmentStatus", "Employment Status", "Current status. Resigned and Terminated employees need an exit date.", "Active", c => c with { Required = true, Type = ExcelColumnType.Enum, Allowed = EmploymentStatuses }),
        Col("joiningDate", "Joining Date", "First day of employment.", "2026-01-05", c => c with { Required = true, Type = ExcelColumnType.Date }),
        Col("probationEndDate", "Probation End Date", "End of probation (optional, not before the joining date).", "2026-04-05", c => c with { Type = ExcelColumnType.Date }),
        Col("exitDate", "Exit Date", "Last day of employment. Required for Resigned or Terminated.", null, c => c with { Type = ExcelColumnType.Date }),
        Col("companyCode", "Company Code", "Code of the company (see the \"Organization codes\" sheet).", "AHT", c => c with { Required = true, MaxLength = 20 }),
        Col("branchCode", "Branch Code", "Code of a branch of that company.", "YGN-HQ", c => c with { Required = true, MaxLength = 20 }),
        Col("departmentCode", "Department Code", "Code of a department of that branch.", "ENG", c => c with { Required = true, MaxLength = 20 }),
        Col("sectionCode", "Section Code", "Code of a section of that department (optional).", null, c => c with { MaxLength = 20 }),
        Col("positionCode", "Position Code", "Code of a position in that department.", "ENG-SE", c => c with { Required = true, MaxLength = 20 }),
        Col("jobGradeCode", "Job Grade Code", "Job grade code. Leave blank to use the position’s grade.", "G3", c => c with { MaxLength = 20, Example = null }),
        Col("managerCode", "Manager Employee Code", "Employee Code of the reporting manager (optional).", "EMP003", c => c with { MaxLength = 12, Example = null }),
        Col("shiftCode", "Shift Code", "Code of the work shift (optional).", "GEN", c => c with { MaxLength = 20, Example = null }),
        Col("bankName", "Bank Name", "Bank where salary is paid (sample data only).", "Sample Bank", c => c with { Required = true, MaxLength = 60, Sensitive = true }),
        Col("bankBranchName", "Bank Branch", "Bank branch (optional).", "Main Branch", c => c with { MaxLength = 60, Sensitive = true }),
        Col("bankAccountName", "Bank Account Holder", "Name on the bank account.", "Mia Hartley", c => c with { Required = true, MaxLength = 80, Sensitive = true }),
        Col("bankAccountNumber", "Bank Account Number", "Digits only, 8 to 20 characters.", "1234567890", c => c with
        {
            Required = true,
            MinLength = 8,
            MaxLength = 20,
            Pattern = @"^\d+$",
            PatternMessage = "Bank Account Number must contain digits only.",
            Sensitive = true,
        }),
    ];

    public static IReadOnlyList<ExcelColumn> Export { get; } =
    [
        Out("employeeCode", "Employee Code", width: 14),
        Out("fullName", "Employee Name", width: 26),
        Out("gender", "Gender"),
        Out("workEmail", "Work Email", width: 32),
        Out("phone", "Mobile Phone"),
        Out("company", "Company", width: 28),
        Out("branch", "Branch", width: 24),
        Out("department", "Department", width: 28),
        Out("section", "Section", width: 22),
        Out("position", "Position", width: 28),
        Out("jobGrade", "Job Grade"),
        Out("manager", "Manager", width: 24),
        Out("shift", "Shift", width: 18),
        Out("employmentType", "Employment Type"),
        Out("employmentStatus", "Employment Status"),
        Out("joiningDate", "Joining Date", ExcelColumnType.Date),
        Out("probationEndDate", "Probation End Date", ExcelColumnType.Date),
        Out("exitDate", "Exit Date", ExcelColumnType.Date),
        Out("dateOfBirth", "Date of Birth", ExcelColumnType.Date, true),
        Out("nationalId", "National ID", sensitive: true),
        Out("personalEmail", "Personal Email", sensitive: true, width: 30),
        Out("alternatePhone", "Alternate Phone", sensitive: true),
        Out("addressLine1", "Address Line 1", sensitive: true, width: 30),
        Out("addressLine2", "Address Line 2", sensitive: true, width: 24),
        Out("city", "City", sensitive: true),
        Out("state", "State / Region", sensitive: true),
        Out("postalCode", "Postal Code", sensitive: true),
        Out("country", "Country", sensitive: true),
        Out("emergencyName", "Emergency Contact Name", sensitive: true, width: 24),
        Out("emergencyRelationship", "Emergency Contact Relationship", sensitive: true),
        Out("emergencyPhone", "Emergency Contact Phone", sensitive: true),
        Out("bankName", "Bank Name", sensitive: true),
        Out("bankBranchName", "Bank Branch", sensitive: true),
        Out("bankAccountName", "Bank Account Holder", sensitive: true, width: 24),
        Out("bankAccountNumber", "Bank Account Number", sensitive: true, width: 22),
    ];

    public static ExcelModuleInfo Info { get; } = new(
        ModuleCode,
        "Employees",
        "Employees",
        "Import employee records: personal, contact, employment and bank details with the organization assignment given by business codes (company, branch, department, position).",
        Import,
        Export,
        ["employeeCode"],
        "Employee Code",
        CanImport: true,
        CanExport: true,
        "Joining date");

    public static ExcelColumn ByKey(string key) => Import.First(c => c.Key == key);

    private static ExcelColumn Col(string key, string header, string description, object? example, Func<ExcelColumn, ExcelColumn> configure) =>
        configure(new ExcelColumn { Key = key, Header = header, Description = description, Example = example });

    private static ExcelColumn Out(string key, string header, ExcelColumnType type = ExcelColumnType.Text, bool sensitive = false, int? width = null) =>
        new() { Key = key, Header = header, Type = type, Sensitive = sensitive, Width = width };
}
