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

    public static IEndpointRouteBuilder MapOrganizationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapCrud<CompanyDto, CompanyRequest, CompanyFilter>("/api/companies", Tag);
        app.MapCrud<BranchDto, BranchRequest, BranchFilter>("/api/branches", Tag);
        app.MapCrud<DepartmentDto, DepartmentRequest, DepartmentFilter>("/api/departments", Tag);
        app.MapCrud<SectionDto, SectionRequest, SectionFilter>("/api/sections", Tag);
        app.MapCrud<JobGradeDto, JobGradeRequest, JobGradeFilter>("/api/job-grades", Tag);
        app.MapCrud<PositionDto, PositionRequest, PositionFilter>("/api/positions", Tag);
        return app;
    }
}
