using Apex.Hrms.Application.Organization.Branches;
using Apex.Hrms.Application.Organization.Companies;
using Apex.Hrms.Application.Organization.Departments;
using Apex.Hrms.Application.Organization.JobGrades;
using Apex.Hrms.Application.Organization.Positions;
using Apex.Hrms.Application.Organization.Sections;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Organization;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.Application.Organization;

internal static class OrganizationServiceCollectionExtensions
{
    public static IServiceCollection AddOrganization(this IServiceCollection services) => services
        .AddCrudResource<Company, CompanyRequest, CompanyDto, CompanyFilter, CompanyMapping, CompanyRules, CompanyQuerySpecification, CompanyDeletionPolicy>()
        .AddCrudResource<Branch, BranchRequest, BranchDto, BranchFilter, BranchMapping, BranchRules, BranchQuerySpecification, BranchDeletionPolicy>()
        .AddCrudResource<Department, DepartmentRequest, DepartmentDto, DepartmentFilter, DepartmentMapping, DepartmentRules, DepartmentQuerySpecification, DepartmentDeletionPolicy>()
        .AddCrudResource<Section, SectionRequest, SectionDto, SectionFilter, SectionMapping, SectionRules, SectionQuerySpecification, SectionDeletionPolicy>()
        .AddCrudResource<JobGrade, JobGradeRequest, JobGradeDto, JobGradeFilter, JobGradeMapping, JobGradeRules, JobGradeQuerySpecification, JobGradeDeletionPolicy>()
        .AddCrudResource<Position, PositionRequest, PositionDto, PositionFilter, PositionMapping, PositionRules, PositionQuerySpecification, PositionDeletionPolicy>();
}
