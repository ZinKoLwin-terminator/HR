using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Organization.Branches;
using Apex.Hrms.Application.Organization.Companies;
using Apex.Hrms.Application.Organization.Departments;
using Apex.Hrms.Application.Organization.JobGrades;
using Apex.Hrms.Application.Organization.Positions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Tests.Fakes;

internal sealed record OrgSeed(
    CompanyDto Company,
    BranchDto Branch,
    DepartmentDto Department,
    PositionDto Position,
    JobGradeDto Grade);

/// <summary>Fictional employee requests and the handler calls the employee tests share.</summary>
internal static class EmployeeFixtures
{
    public static async Task<OrgSeed> SeedOrgAsync(this TestHost host)
    {
        var company = await host.CreateCompanyAsync();
        var branch = await host.CreateBranchAsync(company.Id);
        var department = await host.CreateDepartmentAsync(company.Id, branch.Id);
        var grade = await host.CreateJobGradeAsync();
        var position = await host.CreatePositionAsync(department.Id, grade.Id);
        return new OrgSeed(company, branch, department, position, grade);
    }

    public static EmployeeCreateRequest NewEmployee(OrgSeed org, string? code = null, string email = "mia.hartley@example.test") => new(
        code,
        "Mia",
        "Hartley",
        "Female",
        email,
        PhotoUrl: null,
        org.Company.Id,
        org.Branch.Id,
        org.Department.Id,
        SectionId: null,
        org.Position.Id,
        org.Grade.Id,
        ManagerId: null,
        ShiftId: null,
        "Full-time",
        new DateOnly(2024, 3, 1),
        ProbationEndDate: null,
        "Active",
        new DateOnly(1994, 5, 17),
        "Single",
        "Myanmar",
        "NRC-TEST-000001",
        PersonalEmail: "mia.personal@example.test",
        "+95 9 555 0101",
        AlternatePhone: null,
        new AddressRequest("12 Test Street", null, "Yangon", null, null, "Myanmar"),
        new EmergencyContactRequest("Test Contact", "Sibling", "+95 9 555 0102"));

    public static EmployeeUpdateRequest UpdateOf(EmployeeDto current) => new(
        current.EmployeeCode,
        current.FirstName,
        current.LastName,
        current.Gender,
        current.WorkEmail,
        current.PhotoUrl,
        current.CompanyId,
        current.BranchId,
        current.DepartmentId,
        current.SectionId,
        current.PositionId,
        current.JobGradeId,
        current.ManagerId,
        current.ShiftId,
        current.EmploymentType,
        current.JoiningDate,
        current.ProbationEndDate,
        Requests.Version(current.RowVersion));

    public static EmployeePersonalUpdateRequest PersonalUpdateOf(EmployeePersonalDto current) => new(
        current.DateOfBirth,
        current.MaritalStatus,
        current.Nationality,
        current.NationalId,
        current.PersonalEmail,
        current.Phone,
        current.AlternatePhone,
        new AddressRequest(current.Address.Line1, current.Address.Line2, current.Address.City, current.Address.State, current.Address.PostalCode, current.Address.Country),
        new EmergencyContactRequest(current.EmergencyContact.Name, current.EmergencyContact.Relationship, current.EmergencyContact.Phone),
        Requests.Version(current.RowVersion));

    public static BankAccountUpdateRequest Bank(string number = "1234567890", string? rowVersion = null) =>
        new("Sample Bank", null, "Mia Hartley", number, rowVersion);

    public static Task<Result<EmployeeDto>> CreateEmployee(this TestHost host, EmployeeCreateRequest request) =>
        host.Get<ICreateHandler<EmployeeCreateRequest, EmployeeDto>>().HandleAsync(request, default);

    public static async Task<EmployeeDto> CreateEmployeeAsync(this TestHost host, EmployeeCreateRequest request) =>
        ResultAssert.Succeeded(await host.CreateEmployee(request));

    public static Task<Result<EmployeeDto>> UpdateEmployee(this TestHost host, int id, EmployeeUpdateRequest request) =>
        host.Get<IUpdateHandler<EmployeeUpdateRequest, EmployeeDto>>().HandleAsync(id, request, default);

    public static Task<Result<EmployeeDto>> GetEmployee(this TestHost host, int id) =>
        host.Get<IGetByIdHandler<EmployeeDto>>().HandleAsync(id, default);

    public static Task<Result<TDto>> Command<TCommand, TDto>(this TestHost host, TCommand command)
        where TCommand : IEntityCommand =>
        host.Get<ICommandHandler<TCommand, TDto>>().HandleAsync(command, default);
}
