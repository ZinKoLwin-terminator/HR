using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Leave.Applications;
using Apex.Hrms.Application.Leave.LeavePolicies;
using Apex.Hrms.Application.Leave.LeaveTypes;
using Apex.Hrms.Application.Organization.Branches;
using Apex.Hrms.Application.Organization.Companies;
using Apex.Hrms.Application.Organization.Departments;
using Apex.Hrms.Application.Organization.JobGrades;
using Apex.Hrms.Application.Organization.Positions;
using Apex.Hrms.Application.Organization.Sections;
using Apex.Hrms.Application.Recruitment.Applications;
using Apex.Hrms.Application.Recruitment.Candidates;
using Apex.Hrms.Application.Recruitment.Interviews;
using Apex.Hrms.Application.Recruitment.JobOpenings;

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

    /// <summary>A valid leave type; the code carries the tag so the cleanup finds it.</summary>
    public LeaveTypeCreateRequest LeaveType(
        string code = "AL",
        string name = "Annual Leave",
        decimal days = 15m,
        short maxConsecutive = 10,
        bool requiresDocument = false,
        string? gender = null,
        string status = "Active") => new(
        Code(code),
        Name(name),
        Description: null,
        days,
        IsPaid: true,
        requiresDocument,
        maxConsecutive,
        CarryForward: false,
        MaxCarryForwardDays: 0m,
        "#3f6fd8",
        gender,
        status);

    public static LeaveTypeUpdateRequest LeaveTypeUpdate(LeaveTypeDto current, string? name = null, decimal? days = null) => new(
        current.Code,
        name ?? current.Name,
        current.Description,
        days ?? current.DefaultDaysPerYear,
        current.IsPaid,
        current.RequiresDocument,
        current.MaxConsecutiveDays,
        current.CarryForward,
        current.MaxCarryForwardDays,
        current.Color,
        current.EligibleGender,
        Version(current.RowVersion));

    /// <summary>A valid leave policy; the name starts with the tag.</summary>
    public LeavePolicyCreateRequest LeavePolicy(
        int leaveTypeId,
        string name = "Standard",
        int? jobGradeId = null,
        string? employmentType = null,
        decimal days = 15m,
        string accrual = "Upfront",
        short minServiceMonths = 0,
        string status = "Active") => new(Name(name), leaveTypeId, jobGradeId, employmentType, days, accrual, minServiceMonths, status);

    public static LeavePolicyUpdateRequest LeavePolicyUpdate(LeavePolicyDto current, decimal? days = null) => new(
        current.Name,
        current.LeaveTypeId,
        current.JobGradeId,
        current.EmploymentType,
        days ?? current.DaysPerYear,
        current.Accrual,
        current.MinServiceMonths,
        Version(current.RowVersion));

    public static LeaveApplicationCreateRequest LeaveApplication(
        int employeeId,
        int leaveTypeId,
        DateOnly start,
        DateOnly? end = null,
        bool halfDay = false,
        string? documentName = null) => new(employeeId, leaveTypeId, start, end ?? start, halfDay, "Family matter", documentName);

    public static LeaveApplicationUpdateRequest LeaveApplicationUpdate(LeaveApplicationDto current, DateOnly end) => new(
        current.EmployeeId,
        current.LeaveTypeId,
        current.StartDate,
        end,
        current.HalfDay,
        current.Reason,
        current.DocumentName,
        Version(current.RowVersion));

    /// <summary>
    /// A valid job opening. The code is made by the server, so the title carries the tag (followed by a dash) for the cleanup.
    /// A null closing date is 60 days after <paramref name="today"/>.
    /// </summary>
    public JobOpeningCreateRequest JobOpening(
        int departmentId,
        int hiringManagerId,
        DateOnly today,
        string title = "Engineer",
        int vacancies = 1,
        DateOnly? closing = null) => new(
        Code(title),
        departmentId,
        hiringManagerId,
        "Full-time",
        vacancies,
        PostedOn: null,
        closing ?? today.AddDays(60),
        "Builds test services.");

    public static JobOpeningUpdateRequest JobOpeningUpdate(JobOpeningDto current, string? title = null, int? vacancies = null) => new(
        title ?? current.Title,
        current.DepartmentId,
        current.HiringManagerId,
        current.EmploymentType,
        vacancies ?? current.Vacancies,
        current.PostedOn,
        current.ClosingDate,
        current.Description,
        Version(current.RowVersion));

    /// <summary>A valid candidate; the email is lower case and starts with the tag (candidates are found for cleanup by it).</summary>
    public CandidateCreateRequest Candidate(string local = "ada", decimal years = 4m) => new(
        "Ada",
        "Testcandidate",
        CandidateEmail(local),
        "+95 9 555 0103",
        "Referral",
        "Sample Employer",
        years,
        "cv.pdf",
        Notes: null);

    public string CandidateEmail(string local) => $"{tag.ToLowerInvariant()}-{local}@example.test";

    public static CandidateUpdateRequest CandidateUpdate(CandidateDto current, string? email = null, decimal? years = null) => new(
        current.FirstName,
        current.LastName,
        email ?? current.Email,
        current.Phone,
        current.Source,
        current.CurrentEmployer,
        years ?? current.ExperienceYears,
        current.ResumeName,
        current.Notes,
        Version(current.RowVersion));

    public static JobApplicationCreateRequest JobApplication(int jobOpeningId, int candidateId) => new(jobOpeningId, candidateId, Notes: null);

    public static MoveApplicationStageCommand MoveStage(JobApplicationDto application, string stage, string? reason = null) =>
        new(application.Id, Version(application.RowVersion), stage, reason);

    public static InterviewCreateRequest Interview(
        int applicationId,
        int interviewerId,
        DateOnly date,
        int startHour = 10,
        int minutes = 60) => new(
        applicationId,
        "Technical",
        date,
        new TimeOnly(startHour, 0),
        minutes,
        "Video",
        interviewerId,
        "Meeting link");

    public static RecordInterviewOutcomeCommand Outcome(InterviewDto interview, int score = 4, string recommendation = "Hire") =>
        new(interview.Id, Version(interview.RowVersion), score, recommendation, "Solid.");

    /// <summary>The name of a test holiday: it starts with the tag followed by a dash, as the cleanup expects.</summary>
    public string HolidayName(string text = "holiday") => $"{tag}-{text}";

    private string Code(string suffix) => $"{tag}-{suffix}";

    private string Name(string text) => $"{tag} {text}";
}
