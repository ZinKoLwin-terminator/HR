using Apex.Hrms.Api.Authorization;
using Apex.Hrms.Api.Http;
using Apex.Hrms.Application.Performance.Categories;
using Apex.Hrms.Application.Performance.Insights;
using Apex.Hrms.Application.Performance.Periods;
using Apex.Hrms.Application;
using Apex.Hrms.Application.Performance.RatingBands;
using Apex.Hrms.Application.Performance.Scorecards;
using Apex.Hrms.Application.Performance.Templates;
using Apex.Hrms.Application.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace Apex.Hrms.Api.Endpoints;

/// <summary>
/// KPI master data under <c>/api/kpi</c>: categories, templates, evaluation periods and rating bands. All of it needs
/// <c>kpi-setup</c> permissions (HR), except the period picker, which employees and managers use and which needs
/// <c>kpi:View</c> only. Scorecards (reads, assignment, the plan, the workflow, evidence), the dashboards and the reports follow below.
/// </summary>
internal static class KpiEndpoints
{
    private const string Tag = "KPI";

    private static readonly PermissionModule Setup = PermissionModule.KpiSetup;

    public static IEndpointRouteBuilder MapKpiEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapCrud<KpiCategoryDto, KpiCategoryCreateRequest, KpiCategoryUpdateRequest, KpiCategoryFilter>("/api/kpi/categories", Tag, Setup)
            .MapStatusCommands<KpiCategoryDto>(Setup);
        app.MapCrud<KpiTemplateDto, KpiTemplateCreateRequest, KpiTemplateUpdateRequest, KpiTemplateFilter>("/api/kpi/templates", Tag, Setup)
            .MapStatusCommands<KpiTemplateDto>(Setup);
        app.MapCrud<RatingBandDto, RatingBandCreateRequest, RatingBandUpdateRequest, RatingBandFilter>("/api/kpi/rating-bands", Tag, Setup)
            .MapStatusCommands<RatingBandDto>(Setup);

        MapPeriods(app);
        MapScorecards(app);
        MapAssignments(app);
        MapEvidence(app);
        MapDashboards(app);
        MapReports(app);
        return app;
    }

    /// <summary>
    /// The read-only dashboards. The employee dashboard needs <c>kpi:View</c> and shows the caller's own scorecards only; the manager and
    /// HR dashboards need <c>kpi:Approve</c>. The HR dashboard also needs the HR tier (<c>employees:Edit</c>), which the application layer
    /// checks: anyone else gets 403. <c>periodId</c> is optional everywhere (an unknown one is 404).
    /// </summary>
    private static void MapDashboards(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/kpi/dashboard").WithTags(Tag);

        group.MapGet(
            "/employee",
            async ([FromQuery] int? periodId, IKpiEmployeeDashboardHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(periodId, cancellationToken)).ToOk())
            .RequireAuthorization(PermissionModule.Kpi.PolicyName(Permission.View));

        group.MapGet(
            "/manager",
            async ([FromQuery] int? periodId, IKpiManagerDashboardHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(periodId, cancellationToken)).ToOk())
            .RequireAuthorization(PermissionModule.Kpi.PolicyName(Permission.Approve));

        group.MapGet(
            "/hr",
            async ([FromQuery] int? periodId, [FromQuery] int? companyId, IKpiHrDashboardHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(periodId, companyId, cancellationToken)).ToOk())
            .RequireAuthorization(PermissionModule.Kpi.PolicyName(Permission.Approve));
    }

    /// <summary>
    /// The reports need <c>kpi:View</c> and read only the scorecards in the caller's scope (own, team or all). The filters
    /// (<see cref="KpiReportFilter"/>), the search, the sort whitelist and the paging are those of the query string.
    /// </summary>
    private static void MapReports(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/kpi/reports").WithTags(Tag);
        var view = PermissionModule.Kpi.PolicyName(Permission.View);

        group.MapGet(
            "/summary",
            async ([AsParameters] PageQuery page, [AsParameters] KpiReportFilter filter, IKpiSummaryReportHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(page.ToRequest(filter), cancellationToken)).ToOk())
            .RequireAuthorization(view);

        group.MapGet(
            "/employee/{employeeId:int}",
            async (int employeeId, [AsParameters] PageQuery page, [AsParameters] KpiReportFilter filter, IKpiEmployeeReportHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(employeeId, page.ToRequest(filter), cancellationToken)).ToOk())
            .RequireAuthorization(view);

        group.MapGet(
            "/department/{departmentId:int}",
            async (int departmentId, [AsParameters] PageQuery page, [AsParameters] KpiReportFilter filter, IKpiDepartmentReportHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(departmentId, page.ToRequest(filter), cancellationToken)).ToOk())
            .RequireAuthorization(view);

        group.MapGet(
            "/ranking",
            async ([AsParameters] PageQuery page, [AsParameters] KpiReportFilter filter, IKpiRankingReportHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(page.ToRequest(filter), cancellationToken)).ToOk())
            .RequireAuthorization(view);

        group.MapGet(
            "/kpi-achievement",
            async ([AsParameters] PageQuery page, [AsParameters] KpiReportFilter filter, IKpiAchievementReportHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(page.ToRequest(filter), cancellationToken)).ToOk())
            .RequireAuthorization(view);

        group.MapGet(
            "/target-vs-actual",
            async ([AsParameters] PageQuery page, [AsParameters] KpiReportFilter filter, IKpiTargetVsActualReportHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(page.ToRequest(filter), cancellationToken)).ToOk())
            .RequireAuthorization(view);
    }

    /// <summary>
    /// Evidence files on a KPI of a scorecard. Adding and removing need <c>kpi:Create</c> and are for the scorecard's own employee only
    /// (the application layer decides: 404 outside the caller's scope, 403 for someone else) with the scorecard's row version, while it is
    /// Draft or Returned and its period Active. Download needs <c>kpi:View</c> and follows the scorecard's visibility. The upload's request
    /// size is capped just above the 10 MB file limit; the file type is decided by its content. Antiforgery is checked for every unsafe
    /// method by the middleware (header <c>X-XSRF-TOKEN</c>), as everywhere else.
    /// </summary>
    private static void MapEvidence(IEndpointRouteBuilder app)
    {
        app.MapPost(
            "/api/kpi/scorecard-items/{itemId:int}/attachments",
            async (int itemId, HttpRequest request, IUploadEvidenceHandler handler, CancellationToken cancellationToken) =>
            {
                var upload = await EvidenceUploadReader.ReadAsync(request, cancellationToken);
                return upload.IsFailure
                    ? Results.Problem(ApiProblems.From(upload.Error))
                    : (await handler.HandleAsync(itemId, upload.Value, cancellationToken))
                        .ToCreated(created => $"/api/kpi/attachments/{created.Attachment.Id}");
            })
            .WithTags(Tag)
            .RequireAuthorization(PermissionModule.Kpi.PolicyName(Permission.Create));

        var attachments = app.MapGroup("/api/kpi/attachments").WithTags(Tag);

        attachments.MapGet(
            "/{id:int}",
            async (int id, IDownloadEvidenceHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, cancellationToken)).ToAttachmentFile())
            .RequireAuthorization(PermissionModule.Kpi.PolicyName(Permission.View));

        attachments.MapDelete(
            "/{id:int}",
            async (int id, [FromBody] VersionedBody? body, IRemoveEvidenceHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, body?.RowVersion, cancellationToken)).ToOk())
            .RequireAuthorization(PermissionModule.Kpi.PolicyName(Permission.Create));
    }

    /// <summary>
    /// Scorecard reads need <c>kpi:View</c> and are scoped to the caller's rows (own, own and direct reports, or all): a scorecard
    /// outside the scope is a 404. The HR plan writes (delete a plan, finalize it) need <c>kpi-setup</c> and are not row-scoped.
    /// </summary>
    private static void MapScorecards(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/kpi/scorecards").WithTags(Tag);
        var view = PermissionModule.Kpi.PolicyName(Permission.View);

        group.MapGet(
            "/query",
            async (
                [AsParameters] PageQuery page,
                [AsParameters] ScorecardFilter filter,
                IQueryHandler<ScorecardDto, ScorecardFilter> handler,
                CancellationToken cancellationToken) =>
                (await handler.HandleAsync(page.ToRequest(filter), cancellationToken)).ToOk())
            .RequireAuthorization(view);

        group.MapGet(
            "/{id:int}",
            async (int id, IGetByIdHandler<ScorecardDetailDto> handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, cancellationToken)).ToOk())
            .RequireAuthorization(view);

        group.MapDelete(
            "/{id:int}",
            async (int id, [FromBody] VersionedBody? body, IDeleteHandler<ScorecardDto> handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, body?.RowVersion, cancellationToken)).ToNoContent())
            .RequireAuthorization(Setup.PolicyName(Permission.Delete));

        group.MapCommand<ScorecardDetailDto, FinalizePlanCommand>("finalize-plan", Setup);

        MapSubmission(group);
        MapReview(group);
        MapQueues(app);
    }

    /// <summary>
    /// The employee's part: results and submission need <c>kpi:Create</c> (every employee has it) and are for the scorecard's own employee
    /// only. The application layer decides that and answers 404 for a scorecard outside the caller's scope and 403 for someone else's.
    /// </summary>
    private static void MapSubmission(RouteGroupBuilder group)
    {
        group.MapPut(
            "/{id:int}/results",
            async (int id, SaveScorecardResultsRequest request, IUpdateHandler<SaveScorecardResultsRequest, ScorecardDetailDto> handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, request, cancellationToken)).ToOk())
            .RequireAuthorization(PermissionModule.Kpi.PolicyName(Permission.Create));

        group.MapCommand<ScorecardDetailDto, SubmitScorecardCommand>("submit", PermissionModule.Kpi, Permission.Create);
    }

    /// <summary>
    /// The reviewer's part needs <c>kpi:Approve</c>; whether the caller is the scorecard's reviewer or the HR tier (<c>employees:Edit</c> too),
    /// that nobody acts on their own scorecard and the separation of duties of the HR review are decided by the application layer.
    /// Every write carries the scorecard's <c>rowVersion</c>; the status changes only through these commands.
    /// </summary>
    private static void MapReview(RouteGroupBuilder group)
    {
        var module = PermissionModule.Kpi;
        group.MapPut(
            "/{id:int}/review",
            async (int id, SaveScorecardReviewRequest request, IUpdateHandler<SaveScorecardReviewRequest, ScorecardDetailDto> handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, request, cancellationToken)).ToOk())
            .RequireAuthorization(module.PolicyName(Permission.Approve));

        group
            .MapCommand<ScorecardDetailDto, StartScorecardReviewCommand>("start-review", module, Permission.Approve)
            .MapCommand<ScorecardDetailDto, CommentBody, ApproveScorecardCommand>(
                "approve", module, (id, body) => new ApproveScorecardCommand(id, body?.RowVersion, body?.Comment), Permission.Approve)
            .MapCommand<ScorecardDetailDto, CommentBody, ReturnScorecardCommand>(
                "return", module, (id, body) => new ReturnScorecardCommand(id, body?.RowVersion, body?.Comment), Permission.Approve)
            .MapCommand<ScorecardDetailDto, CommentBody, RejectScorecardCommand>(
                "reject", module, (id, body) => new RejectScorecardCommand(id, body?.RowVersion, body?.Comment), Permission.Approve)
            .MapCommand<ScorecardDetailDto, CommentBody, FinalizeScorecardCommand>(
                "finalize", module, (id, body) => new FinalizeScorecardCommand(id, body?.RowVersion, body?.Comment), Permission.Approve);
    }

    /// <summary>The caller's own scorecards (<c>kpi:View</c>) and the review queue (<c>kpi:Approve</c>).</summary>
    private static void MapQueues(IEndpointRouteBuilder app)
    {
        app.MapGet(
            "/api/kpi/my-scorecards",
            async ([AsParameters] PageQuery page, [AsParameters] ScorecardFilter filter, IMyScorecardsHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(page.ToRequest(filter), cancellationToken)).ToOk())
            .WithTags(Tag)
            .RequireAuthorization(PermissionModule.Kpi.PolicyName(Permission.View));

        app.MapGet(
            "/api/kpi/reviews/pending",
            async ([AsParameters] PageQuery page, [AsParameters] ScorecardFilter filter, IPendingReviewsHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(page.ToRequest(filter), cancellationToken)).ToOk())
            .WithTags(Tag)
            .RequireAuthorization(PermissionModule.Kpi.PolicyName(Permission.Approve));
    }

    /// <summary>
    /// Assigning KPIs and changing the plan are HR work: <c>kpi-setup:Create</c> to assign, <c>kpi-setup:Edit</c> to change or remove
    /// a planned KPI. A single assignment answers 201 with a Location when it created the scorecard and 200 when it added KPIs to
    /// the one in Planning; both return the scorecard.
    /// </summary>
    private static void MapAssignments(IEndpointRouteBuilder app)
    {
        var assignments = app.MapGroup("/api/kpi/assignments").WithTags(Tag);

        assignments.MapPost(
            "/",
            async (AssignKpiRequest request, IAssignKpiHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(request, cancellationToken)).ToCreatedOrOk(
                    assigned => assigned.Created,
                    assigned => assigned.Scorecard,
                    scorecard => $"/api/kpi/scorecards/{scorecard.Id}"))
            .RequireAuthorization(Setup.PolicyName(Permission.Create));

        assignments.MapPost(
            "/bulk",
            async (BulkAssignKpiRequest request, IBulkAssignKpiHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(request, cancellationToken)).ToOk())
            .RequireAuthorization(Setup.PolicyName(Permission.Create));

        var items = app.MapGroup("/api/kpi/scorecard-items").WithTags(Tag);

        items.MapPut(
            "/{id:int}",
            async (int id, ScorecardItemUpdateRequest request, IUpdateHandler<ScorecardItemUpdateRequest, ScorecardItemDto> handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, request, cancellationToken)).ToOk())
            .RequireAuthorization(Setup.PolicyName(Permission.Edit));

        items.MapDelete(
            "/{id:int}",
            async (int id, [FromBody] VersionedBody? body, IDeleteHandler<ScorecardItemDto> handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, body?.RowVersion, cancellationToken)).ToNoContent())
            .RequireAuthorization(Setup.PolicyName(Permission.Edit));
    }

    /// <summary>
    /// Periods never change status through PUT: each move is a command that needs <c>kpi-setup:Edit</c>. The picker is
    /// declared on the same group; <c>/options</c> cannot be mistaken for <c>/{id:int}</c>.
    /// </summary>
    private static void MapPeriods(IEndpointRouteBuilder app)
    {
        var group = app.MapCrud<EvaluationPeriodDto, EvaluationPeriodCreateRequest, EvaluationPeriodUpdateRequest, EvaluationPeriodFilter>(
            "/api/kpi/periods", Tag, Setup);

        group.MapGet(
            "/options",
            async ([FromQuery] int? companyId, IEvaluationPeriodOptionsHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(companyId, cancellationToken)).ToOk())
            .RequireAuthorization(PermissionModule.Kpi.PolicyName(Permission.View));

        group
            .MapCommand<EvaluationPeriodDto, ActivatePeriodCommand>("activate", Setup)
            .MapCommand<EvaluationPeriodDto, CompletePeriodCommand>("complete", Setup)
            .MapCommand<EvaluationPeriodDto, ClosePeriodCommand>("close", Setup);
    }

    /// <summary>Body of approve, return, reject and finalize: <c>{ "rowVersion": "...", "comment": "..." }</c>. The comment is required to return or reject.</summary>
    private sealed record CommentBody(string? RowVersion, string? Comment);
}
