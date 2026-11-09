using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Organization.Branches;
using Apex.Hrms.Application.Organization.Companies;
using Apex.Hrms.Application.Organization.Departments;
using Apex.Hrms.Application.Organization.JobGrades;
using Apex.Hrms.Application.Organization.Positions;
using Apex.Hrms.Application.Organization.Sections;

namespace Apex.Hrms.IntegrationTests.Support;

/// <summary>
/// Valid requests with fictional data. Tests override only what they care about. Every code and name argument is a
/// suffix: the factory prepends the tag of the test that owns it (<c>TST-1A2B3C4D-</c> before a code,
/// <c>TST-1A2B3C4D </c> before a name), so the rows of one test never collide with those of another and the cleanup
/// can find exactly its own rows.
/// </summary>
public sealed class Requests(string tag)
{
    public static string Version(byte[] rowVersion) => Convert.ToBase64String(rowVersion);

    public CompanyCreateRequest Company(
        string code = "A",
        string name = "Test Company A",
        string status = "Active",
        string city = "Yangon") => new(
        Code(code),
        Name(name),
        $"{Name(name)} Ltd",
        RegistrationNumber: null,
        TaxId: null,
        "Software",
        "info@example.test",
        "+95 1 555 0100",
        Website: null,
        Address: null,
        city,
        "Myanmar",
        new DateOnly(2015, 3, 1),
        status);

    public CompanyUpdateRequest CompanyUpdate(
        CompanyDto current,
        string? code = null,
        string? name = null,
        string? city = null,
        string? rowVersion = null) => new(
        code is null ? current.Code : Code(code),
        name is null ? current.Name : Name(name),
        current.LegalName,
        RegistrationNumber: null,
        TaxId: null,
        current.Industry,
        current.Email,
        current.Phone,
        Website: null,
        Address: null,
        city ?? current.City,
        current.Country,
        current.FoundedDate,
        rowVersion ?? Version(current.RowVersion));

    public BranchCreateRequest Branch(
        int companyId,
        string code = "A-YGN",
        string name = "Yangon Test Office",
        string status = "Active",
        string type = "Branch",
        string city = "Yangon") => new(
        companyId,
        Code(code),
        Name(name),
        type,
        Email: null,
        Phone: null,
        Address: null,
        city,
        "Myanmar",
        ManagerEmployeeId: null,
        new DateOnly(2020, 1, 1),
        status);

    public static BranchUpdateRequest BranchUpdate(BranchDto current, int? companyId = null, string? rowVersion = null) => new(
        companyId ?? current.CompanyId,
        current.Code,
        current.Name,
        current.Type,
        Email: null,
        Phone: null,
        Address: null,
        current.City,
        current.Country,
        ManagerEmployeeId: null,
        current.OpenedDate,
        rowVersion ?? Version(current.RowVersion));

    public DepartmentCreateRequest Department(
        int companyId,
        int branchId,
        string code = "A-ENG",
        string name = "Engineering",
        string status = "Active") => new(
        companyId,
        branchId,
        Code(code),
        Name(name),
        Description: null,
        HeadEmployeeId: null,
        CostCenter: null,
        status);

    public static DepartmentUpdateRequest DepartmentUpdate(DepartmentDto current, int? companyId = null, int? branchId = null) => new(
        companyId ?? current.CompanyId,
        branchId ?? current.BranchId,
        current.Code,
        current.Name,
        Description: null,
        HeadEmployeeId: null,
        CostCenter: null,
        Version(current.RowVersion));

    public SectionCreateRequest Section(
        int departmentId,
        string code = "A-BE",
        string name = "Backend",
        string status = "Active") => new(
        departmentId,
        Code(code),
        Name(name),
        Description: null,
        SupervisorEmployeeId: null,
        status);

    public static SectionUpdateRequest SectionUpdate(SectionDto current, int? departmentId = null) => new(
        departmentId ?? current.DepartmentId,
        current.Code,
        current.Name,
        Description: null,
        SupervisorEmployeeId: null,
        Version(current.RowVersion));

    public JobGradeCreateRequest JobGrade(
        string code = "G1",
        string name = "Test Grade",
        decimal min = 100m,
        decimal max = 200m,
        string status = "Active") => new(
        Code(code),
        Name(name),
        Level: 1,
        min,
        max,
        Description: null,
        status);

    public static JobGradeUpdateRequest JobGradeUpdate(JobGradeDto current, decimal? min = null, decimal? max = null) => new(
        current.Code,
        current.Name,
        current.Level,
        min ?? current.MinSalary,
        max ?? current.MaxSalary,
        Description: null,
        Version(current.RowVersion));

    public PositionCreateRequest Position(
        int departmentId,
        int jobGradeId,
        string code = "A-DEV",
        string title = "Developer",
        int? sectionId = null) => new(
        departmentId,
        sectionId,
        jobGradeId,
        Code(code),
        title,
        Description: null,
        Headcount: 2,
        ReportsToPositionId: null,
        "Active");

    /// <summary>Moves a position; <paramref name="sectionId"/> is the new section (null removes it).</summary>
    public static PositionUpdateRequest PositionUpdate(PositionDto current, int? departmentId = null, int? sectionId = null) => new(
        departmentId ?? current.DepartmentId,
        sectionId,
        current.JobGradeId,
        current.Code,
        current.Title,
        Description: null,
        current.Headcount,
        current.ReportsToPositionId,
        Version(current.RowVersion));

    /// <summary>
    /// The employee code of a test: the 8 hex characters of the tag, a dash and a suffix of at most 3 characters, 12 in
    /// all, which is the longest code the table accepts. Employees are found for cleanup by their work email instead.
    /// </summary>
    public string EmployeeCode(string suffix) => $"{tag[TestRows.TagFamily.Length..]}-{suffix}";

    /// <summary>The work email of a test employee: lower case (the server lower-cases it), starting with the tag.</summary>
    public string WorkEmail(string local) => $"{tag.ToLowerInvariant()}-{local}@example.test";

    /// <summary>A valid, fictional employee placed in the given organization records. A null code suffix asks for a generated code.</summary>
    public EmployeeCreateRequest Employee(
        int companyId,
        int branchId,
        int departmentId,
        int positionId,
        int jobGradeId,
        string? codeSuffix = null,
        string local = "mia",
        string status = "Active") => new(
        codeSuffix is null ? null : EmployeeCode(codeSuffix),
        "Mia",
        "Testwell",
        "Female",
        WorkEmail(local),
        PhotoUrl: null,
        companyId,
        branchId,
        departmentId,
        SectionId: null,
        positionId,
        jobGradeId,
        ManagerId: null,
        ShiftId: null,
        "Full-time",
        new DateOnly(2024, 3, 1),
        ProbationEndDate: null,
        status,
        new DateOnly(1994, 5, 17),
        "Single",
        "Myanmar",
        "NRC-TEST-000001",
        PersonalEmail: "mia.personal@example.test",
        "+95 9 555 0101",
        AlternatePhone: null,
        new AddressRequest("12 Test Street", null, "Yangon", null, null, "Myanmar"),
        new EmergencyContactRequest("Test Contact", "Sibling", "+95 9 555 0102"));

    public static EmployeeUpdateRequest EmployeeUpdate(EmployeeDto current, int? positionId = null, int? jobGradeId = null, string? workEmail = null) => new(
        current.EmployeeCode,
        current.FirstName,
        current.LastName,
        current.Gender,
        workEmail ?? current.WorkEmail,
        current.PhotoUrl,
        current.CompanyId,
        current.BranchId,
        current.DepartmentId,
        current.SectionId,
        positionId ?? current.PositionId,
        jobGradeId ?? current.JobGradeId,
        current.ManagerId,
        current.ShiftId,
        current.EmploymentType,
        current.JoiningDate,
        current.ProbationEndDate,
        Version(current.RowVersion));

    private string Code(string suffix) => $"{tag}-{suffix}";

    private string Name(string text) => $"{tag} {text}";
}
