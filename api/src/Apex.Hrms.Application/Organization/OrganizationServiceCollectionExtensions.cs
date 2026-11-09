using Apex.Hrms.Application.Organization.Branches;
using Apex.Hrms.Application.Organization.Companies;
using Apex.Hrms.Application.Organization.Departments;
using Apex.Hrms.Application.Organization.JobGrades;
using Apex.Hrms.Application.Organization.Positions;
using Apex.Hrms.Application.Organization.Sections;
using Apex.Hrms.Application.References;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Organization;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.Application.Organization;

internal static class OrganizationServiceCollectionExtensions
{
    public static IServiceCollection AddOrganization(this IServiceCollection services) => services
        .AddCrudResource<Company, CompanyDetails, CompanyCreateRequest, CompanyUpdateRequest, CompanyDto, CompanyFilter, CompanyMapping, CompanyRules, CompanyQuerySpecification>()
        .AddCrudResource<Branch, BranchDetails, BranchCreateRequest, BranchUpdateRequest, BranchDto, BranchFilter, BranchMapping, BranchRules, BranchQuerySpecification>()
        .AddCrudResource<Department, DepartmentDetails, DepartmentCreateRequest, DepartmentUpdateRequest, DepartmentDto, DepartmentFilter, DepartmentMapping, DepartmentRules, DepartmentQuerySpecification>()
        .AddCrudResource<Section, SectionDetails, SectionCreateRequest, SectionUpdateRequest, SectionDto, SectionFilter, SectionMapping, SectionRules, SectionQuerySpecification>()
        .AddCrudResource<JobGrade, JobGradeDetails, JobGradeCreateRequest, JobGradeUpdateRequest, JobGradeDto, JobGradeFilter, JobGradeMapping, JobGradeRules, JobGradeQuerySpecification>()
        .AddCrudResource<Position, PositionDetails, PositionCreateRequest, PositionUpdateRequest, PositionDto, PositionFilter, PositionMapping, PositionRules, PositionQuerySpecification>()
        .AddOrganizationReferences();

    /// <summary>
    /// Which records point at which, and what that blocks. Delete: the record cannot be removed while they exist.
    /// Reparent: the record cannot move under another parent, because the composite foreign keys in the database
    /// (for example Department to Branch on BranchId and CompanyId) would reject the change.
    /// </summary>
    private static IServiceCollection AddOrganizationReferences(this IServiceCollection services) => services
        .AddReference<Company, Branch>("branch(es)", ReferenceUse.Delete, ownerId => branch => branch.CompanyId == ownerId)
        .AddReference<Company, Department>("department(s)", ReferenceUse.Delete, ownerId => department => department.CompanyId == ownerId)
        .AddReference<Company, EmployeeReference>("employee(s)", ReferenceUse.Delete, ownerId => employee => employee.CompanyId == ownerId)

        .AddReference<Branch, Department>("department(s)", ReferenceUse.Both, ownerId => department => department.BranchId == ownerId)
        .AddReference<Branch, EmployeeReference>("employee(s)", ReferenceUse.Both, ownerId => employee => employee.BranchId == ownerId)

        .AddReference<Department, Section>("section(s)", ReferenceUse.Both, ownerId => section => section.DepartmentId == ownerId)
        .AddReference<Department, Position>("position(s)", ReferenceUse.Both, ownerId => position => position.DepartmentId == ownerId)
        .AddReference<Department, EmployeeReference>("employee(s)", ReferenceUse.Both, ownerId => employee => employee.DepartmentId == ownerId)

        .AddReference<Section, Position>("position(s)", ReferenceUse.Both, ownerId => position => position.SectionId == ownerId)
        .AddReference<Section, EmployeeReference>("employee(s)", ReferenceUse.Both, ownerId => employee => employee.SectionId == ownerId)

        .AddReference<JobGrade, Position>("position(s)", ReferenceUse.Delete, ownerId => position => position.JobGradeId == ownerId)
        .AddReference<JobGrade, EmployeeReference>("employee(s)", ReferenceUse.Delete, ownerId => employee => employee.JobGradeId == ownerId)

        .AddReference<Position, EmployeeReference>("employee(s)", ReferenceUse.Both, ownerId => employee => employee.PositionId == ownerId)
        .AddReference<Position, Position>("position(s) reporting to it", ReferenceUse.Delete, ownerId => position => position.ReportsToPositionId == ownerId);
}
