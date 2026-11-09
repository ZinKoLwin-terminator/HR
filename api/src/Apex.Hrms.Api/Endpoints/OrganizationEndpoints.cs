using Apex.Hrms.Api.Authorization;
using Apex.Hrms.Application.Organization.Branches;
using Apex.Hrms.Application.Organization.Companies;
using Apex.Hrms.Application.Organization.Departments;
using Apex.Hrms.Application.Organization.JobGrades;
using Apex.Hrms.Application.Organization.Positions;
using Apex.Hrms.Application.Organization.Sections;

namespace Apex.Hrms.Api.Endpoints;

internal static class OrganizationEndpoints
{
    private const string Tag = "Organization";

    private static readonly PermissionModule Module = PermissionModule.Organization;

    public static IEndpointRouteBuilder MapOrganizationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapCrud<CompanyDto, CompanyCreateRequest, CompanyUpdateRequest, CompanyFilter>("/api/companies", Tag, Module)
            .MapStatusCommands<CompanyDto>(Module);
        app.MapCrud<BranchDto, BranchCreateRequest, BranchUpdateRequest, BranchFilter>("/api/branches", Tag, Module)
            .MapStatusCommands<BranchDto>(Module);
        app.MapCrud<DepartmentDto, DepartmentCreateRequest, DepartmentUpdateRequest, DepartmentFilter>("/api/departments", Tag, Module)
            .MapStatusCommands<DepartmentDto>(Module);
        app.MapCrud<SectionDto, SectionCreateRequest, SectionUpdateRequest, SectionFilter>("/api/sections", Tag, Module)
            .MapStatusCommands<SectionDto>(Module);
        app.MapCrud<JobGradeDto, JobGradeCreateRequest, JobGradeUpdateRequest, JobGradeFilter>("/api/job-grades", Tag, Module)
            .MapStatusCommands<JobGradeDto>(Module);
        app.MapCrud<PositionDto, PositionCreateRequest, PositionUpdateRequest, PositionFilter>("/api/positions", Tag, Module)
            .MapStatusCommands<PositionDto>(Module);
        return app;
    }
}
