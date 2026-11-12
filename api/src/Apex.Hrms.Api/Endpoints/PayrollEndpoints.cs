using Apex.Hrms.Api.Authorization;
using Apex.Hrms.Api.Http;
using Apex.Hrms.Application.Payroll.Assignments;
using Apex.Hrms.Application.Payroll.Components;
using Apex.Hrms.Application.Payroll.Payslips;
using Apex.Hrms.Application.Payroll.Runs;
using Apex.Hrms.Application.Payroll.Structures;
using Apex.Hrms.Application.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace Apex.Hrms.Api.Endpoints;

/// <summary>
/// The payroll module. ALL FIGURES ARE DEMONSTRATION FIGURES, not statutory tax or social security calculations. Salary
/// amounts and payslips are personal data: every route needs a payroll permission, the lists of salaries and payslips are
/// paged only, and no response carries more than the masked bank account.
/// The database seed has no payroll Delete (only View, Create, Edit and Approve), so deleting a component, a structure,
/// a salary assignment or a draft period needs payroll:Edit.
/// </summary>
internal static class PayrollEndpoints
{
    private const string Tag = "Payroll";

    private static readonly PermissionModule Module = PermissionModule.Payroll;

    private const CrudOperations ReadCreateUpdate = CrudOperations.Read | CrudOperations.Create | CrudOperations.Update;

    public static IEndpointRouteBuilder MapPayrollEndpoints(this IEndpointRouteBuilder app)
    {
        MapDeleteWithEdit<SalaryComponentDto>(
            app.MapCrud<SalaryComponentDto, SalaryComponentCreateRequest, SalaryComponentUpdateRequest, SalaryComponentFilter>(
                "/api/salary-components", Tag, Module, ReadCreateUpdate)
            .MapStatusCommands<SalaryComponentDto>(Module));
        MapDeleteWithEdit<SalaryStructureDto>(
            app.MapCrud<SalaryStructureDto, SalaryStructureCreateRequest, SalaryStructureUpdateRequest, SalaryStructureFilter>(
                "/api/salary-structures", Tag, Module, ReadCreateUpdate)
            .MapStatusCommands<SalaryStructureDto>(Module));

        MapAssignments(app);
        MapRuns(app);
        MapPayslips(app);
        return app;
    }

    /// <summary>
    /// Salaries are personal data, so there is deliberately no "get all": assignments are only listed through /query, in
    /// pages of a limited size. An employee's salary history is /query?employeeId=..&amp;sort=effectiveFrom:desc.
    /// </summary>
    private static void MapAssignments(IEndpointRouteBuilder app)
    {
        var group = app.MapCrud<SalaryAssignmentDto, SalaryAssignmentCreateRequest, SalaryAssignmentUpdateRequest, SalaryAssignmentFilter>(
            "/api/salary-assignments",
            Tag,
            Module,
            CrudOperations.Create | CrudOperations.Update);
        MapDeleteWithEdit<SalaryAssignmentDto>(group);

        var view = Module.PolicyName(Permission.View);
        group.MapGet(
            "/query",
            async (
                [AsParameters] PageQuery page,
                [AsParameters] SalaryAssignmentFilter filter,
                IQueryHandler<SalaryAssignmentDto, SalaryAssignmentFilter> handler,
                CancellationToken cancellationToken) =>
                (await handler.HandleAsync(page.ToRequest(filter), cancellationToken)).ToOk())
            .RequireAuthorization(view);
        group.MapGet("/{id:int}", async (int id, IGetByIdHandler<SalaryAssignmentDto> handler, CancellationToken cancellationToken) =>
            (await handler.HandleAsync(id, cancellationToken)).ToOk())
            .RequireAuthorization(view);
    }

    /// <summary>
    /// A run starts in Draft and moves only through commands, never through PUT. Preparing (create, change a draft, delete a
    /// draft, process) is payroll:Edit or Create; approving, marking paid and returning a processed run to draft are payroll:Approve,
    /// so the people who prepare a run cannot reverse an approval. Nobody approves a run they processed.
    /// </summary>
    private static void MapRuns(IEndpointRouteBuilder app)
    {
        var group = app.MapCrud<PayrollRunDto, PayrollRunCreateRequest, PayrollRunUpdateRequest, PayrollRunFilter>(
            "/api/payroll-runs",
            Tag,
            Module,
            ReadCreateUpdate);
        MapDeleteWithEdit<PayrollRunDto>(group);

        group
            .MapCommand<PayrollRunDto, ProcessPayrollRunCommand>("process", Module, Permission.Edit)
            .MapCommand<PayrollRunDto, ApprovePayrollRunCommand>("approve", Module, Permission.Approve)
            .MapCommand<PayrollRunDto, MarkPayrollRunPaidCommand>("mark-paid", Module, Permission.Approve)
            .MapCommand<PayrollRunDto, ReturnToDraftBody, ReturnPayrollRunToDraftCommand>(
                "return-to-draft",
                Module,
                (id, body) => new ReturnPayrollRunToDraftCommand(id, body?.RowVersion, body?.Reason),
                Permission.Approve);
    }

    /// <summary>
    /// Payslips are read only: processing a run makes them and returning the run to draft removes them. There is no "get all":
    /// a run's payslips and an employee's payslips are /query filtered by runId or employeeId.
    /// </summary>
    private static void MapPayslips(IEndpointRouteBuilder app)
    {
        var view = Module.PolicyName(Permission.View);
        var group = app.MapGroup("/api/payslips").WithTags(Tag);
        group.MapGet(
            "/query",
            async (
                [AsParameters] PageQuery page,
                [AsParameters] PayslipFilter filter,
                IQueryHandler<PayslipDto, PayslipFilter> handler,
                CancellationToken cancellationToken) =>
                (await handler.HandleAsync(page.ToRequest(filter), cancellationToken)).ToOk())
            .RequireAuthorization(view);
        group.MapGet("/{id:int}", async (int id, IGetByIdHandler<PayslipDto> handler, CancellationToken cancellationToken) =>
            (await handler.HandleAsync(id, cancellationToken)).ToOk())
            .RequireAuthorization(view);
    }

    /// <summary>
    /// <c>DELETE {group}/{id}</c> with body <c>{ "rowVersion": "..." }</c>, authorized by payroll:Edit (the module has no Delete).
    /// The shared CRUD mapping would ask for a payroll:Delete policy that does not exist.
    /// </summary>
    private static void MapDeleteWithEdit<TDto>(RouteGroupBuilder group) =>
        group.MapDelete(
            "/{id:int}",
            async (int id, [FromBody] VersionedBody? body, IDeleteHandler<TDto> handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, body?.RowVersion, cancellationToken)).ToNoContent())
            .RequireAuthorization(Module.PolicyName(Permission.Edit));

    /// <summary>Body of return-to-draft: <c>{ "rowVersion": "...", "reason": "..." }</c>. The reason is optional.</summary>
    private sealed record ReturnToDraftBody(string? RowVersion, string? Reason);
}
