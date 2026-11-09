using Apex.Hrms.Application.Organization.Branches;
using Apex.Hrms.Application.Organization.Companies;
using Apex.Hrms.Application.Organization.Departments;
using Apex.Hrms.Application.Organization.JobGrades;
using Apex.Hrms.Application.Organization.Positions;
using Apex.Hrms.Application.Organization.Sections;

namespace Apex.Hrms.Application.Tests.Fakes;

/// <summary>
/// Valid, fictional requests; tests override only the field they care about. Create requests carry a status,
/// update requests carry the row version of the record they change (pass the DTO row version).
/// </summary>
internal static class Requests
{
    public static string Version(byte[] rowVersion) => Convert.ToBase64String(rowVersion);

    public static CompanyCreateRequest Company(string code = "AHT", string name = "Apex Holdings", string status = "Active") => new(
        code,
        name,
        "Apex Holdings Ltd",
        RegistrationNumber: null,
        TaxId: null,
        "Technology",
        "info@example.test",
        "+95 1 555 0100",
        Website: null,
        Address: null,
        "Yangon",
        "Myanmar",
        new DateOnly(2015, 3, 1),
        status);

    public static CompanyUpdateRequest CompanyUpdate(byte[] rowVersion, string code = "AHT", string name = "Apex Holdings") => new(
        code,
        name,
        "Apex Holdings Ltd",
        RegistrationNumber: null,
        TaxId: null,
        "Technology",
        "info@example.test",
        "+95 1 555 0100",
        Website: null,
        Address: null,
        "Yangon",
        "Myanmar",
        new DateOnly(2015, 3, 1),
        Version(rowVersion));

    public static BranchCreateRequest Branch(int companyId, string code = "YGN", string status = "Active", DateOnly? opened = null) => new(
        companyId,
        code,
        "Yangon Office",
        "Branch",
        Email: null,
        Phone: null,
        Address: null,
        "Yangon",
        "Myanmar",
        ManagerEmployeeId: null,
        opened ?? new DateOnly(2020, 1, 1),
        status);

    public static BranchUpdateRequest BranchUpdate(byte[] rowVersion, int companyId, string code = "YGN") => new(
        companyId,
        code,
        "Yangon Office",
        "Branch",
        Email: null,
        Phone: null,
        Address: null,
        "Yangon",
        "Myanmar",
        ManagerEmployeeId: null,
        new DateOnly(2020, 1, 1),
        Version(rowVersion));

    public static DepartmentCreateRequest Department(int companyId, int branchId, string code = "ENG", string status = "Active") => new(
        companyId,
        branchId,
        code,
        "Engineering",
        Description: null,
        HeadEmployeeId: null,
        CostCenter: null,
        status);

    public static DepartmentUpdateRequest DepartmentUpdate(byte[] rowVersion, int companyId, int branchId, string code = "ENG") => new(
        companyId,
        branchId,
        code,
        "Engineering",
        Description: null,
        HeadEmployeeId: null,
        CostCenter: null,
        Version(rowVersion));

    public static SectionCreateRequest Section(int departmentId, string code = "BE", string status = "Active") => new(
        departmentId,
        code,
        "Backend",
        Description: null,
        SupervisorEmployeeId: null,
        status);

    public static SectionUpdateRequest SectionUpdate(byte[] rowVersion, int departmentId, string code = "BE") => new(
        departmentId,
        code,
        "Backend",
        Description: null,
        SupervisorEmployeeId: null,
        Version(rowVersion));

    public static JobGradeCreateRequest JobGrade(string code = "G1", string status = "Active", decimal min = 100m, decimal max = 200m) => new(
        code,
        "Junior",
        Level: 1,
        min,
        max,
        Description: null,
        status);

    public static JobGradeUpdateRequest JobGradeUpdate(byte[] rowVersion, string code = "G1", decimal min = 100m, decimal max = 200m) => new(
        code,
        "Junior",
        Level: 1,
        min,
        max,
        Description: null,
        Version(rowVersion));

    public static PositionCreateRequest Position(
        int departmentId,
        int jobGradeId,
        string code = "DEV",
        int? sectionId = null,
        int? reportsTo = null) => new(
        departmentId,
        sectionId,
        jobGradeId,
        code,
        "Developer",
        Description: null,
        Headcount: 2,
        reportsTo,
        "Active");

    public static PositionUpdateRequest PositionUpdate(
        byte[] rowVersion,
        int departmentId,
        int jobGradeId,
        string code = "DEV",
        int? sectionId = null,
        int? reportsTo = null) => new(
        departmentId,
        sectionId,
        jobGradeId,
        code,
        "Developer",
        Description: null,
        Headcount: 2,
        reportsTo,
        Version(rowVersion));
}
