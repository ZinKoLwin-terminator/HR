using Apex.Hrms.Api.Authorization;
using Apex.Hrms.Api.Http;
using Apex.Hrms.Application;
using Apex.Hrms.Application.Grievances;
using Apex.Hrms.Application.Grievances.Categories;
using Apex.Hrms.Application.Grievances.Insights;
using Apex.Hrms.Application.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace Apex.Hrms.Api.Endpoints;

/// <summary>
/// The grievance module under <c>/api/grievances</c>. The endpoints only translate HTTP: every rule (scope, capacity, redaction, the
/// state machine, the access log) lives in the application layer. Where several tiers share a route the endpoint requires ANY of their
/// permissions (<see cref="AuthorizationSetup.GrievanceCaseTeamPolicy"/>, <see cref="AuthorizationSetup.GrievanceCaseWorkPolicy"/>,
/// <see cref="AuthorizationSetup.GrievanceInsightPolicy"/>) and the handler decides what the caller may do on that case (404 outside
/// the scope, 403 for the wrong capacity). Uploads check the case from the route before a byte of the body is read and are rate limited.
/// </summary>
internal static class GrievanceEndpoints
{
    private const string Tag = "Grievances";
    private const string Root = "/api/grievances";

    private static readonly PermissionModule Self = PermissionModule.Grievance;
    private static readonly PermissionModule Management = PermissionModule.GrievanceManagement;
    private static readonly PermissionModule Investigation = PermissionModule.GrievanceInvestigation;
    private static readonly PermissionModule Review = PermissionModule.GrievanceReview;

    public static IEndpointRouteBuilder MapGrievanceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapCrud<GrievanceCategoryDto, GrievanceCategoryCreateRequest, GrievanceCategoryUpdateRequest, GrievanceCategoryFilter>($"{Root}/categories", Tag, Management)
            .MapStatusCommands<GrievanceCategoryDto>(Management);

        MapSelf(app);
        MapCaseReads(app);
        MapCommands(app);
        MapCaseWork(app);
        MapInsights(app);
        return app;
    }

    /// <summary>The submitter's own grievances: reads need <c>grievance:View</c>, writes <c>grievance:Create</c>.</summary>
    private static void MapSelf(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(Root).WithTags(Tag);
        var view = Self.PolicyName(Permission.View);
        var create = Self.PolicyName(Permission.Create);

        group.MapGet("/options", async (IMyGrievanceService my, CancellationToken cancellationToken) =>
            (await my.OptionsAsync(cancellationToken)).ToOk())
            .RequireAuthorization(AuthorizationSetup.GrievanceOptionsPolicy);

        group.MapGet(
            "/my",
            async ([AsParameters] PageQuery page, [AsParameters] MyGrievanceFilter filter, IMyGrievanceService my, CancellationToken cancellationToken) =>
                (await my.QueryAsync(page.ToRequest(filter), cancellationToken)).ToOk())
            .RequireAuthorization(view);

        group.MapGet("/my/dashboard", async (IMyGrievanceService my, CancellationToken cancellationToken) =>
            (await my.DashboardAsync(cancellationToken)).ToOk())
            .RequireAuthorization(view);

        group.MapGet("/my/{id:int}", async (int id, IMyGrievanceService my, CancellationToken cancellationToken) =>
            (await my.GetAsync(id, cancellationToken)).ToOk())
            .RequireAuthorization(view);

        group.MapGet("/my/attachments/{id:int}", async (int id, IMyGrievanceService my, CancellationToken cancellationToken) =>
            (await my.DownloadAttachmentAsync(id, cancellationToken)).ToAttachmentFile())
            .RequireAuthorization(view);

        group.MapPost("/my", async (GrievanceDraftCreateRequest request, IMyGrievanceService my, CancellationToken cancellationToken) =>
            (await my.CreateDraftAsync(request, cancellationToken)).ToCreated(created => $"{Root}/my/{created.Id}"))
            .RequireAuthorization(create);

        group.MapPut("/my/{id:int}", async (int id, GrievanceDraftUpdateRequest request, IMyGrievanceService my, CancellationToken cancellationToken) =>
            (await my.UpdateDraftAsync(id, request, cancellationToken)).ToOk())
            .RequireAuthorization(create);

        group.MapDelete("/my/{id:int}", async (int id, [FromBody] VersionedBody? body, IMyGrievanceService my, CancellationToken cancellationToken) =>
            (await my.DeleteDraftAsync(id, body?.RowVersion, cancellationToken)).ToNoContent())
            .RequireAuthorization(create);

        group.MapPost("/my/{id:int}/submit", async (int id, [FromBody] SubmitGrievanceRequest? body, IMyGrievanceService my, CancellationToken cancellationToken) =>
            (await my.SubmitAsync(id, body ?? new SubmitGrievanceRequest(null, false), cancellationToken)).ToOk())
            .RequireAuthorization(create);

        group.MapPost("/my/{id:int}/messages", async (int id, [FromBody] MyGrievanceMessageRequest? body, IMyGrievanceService my, CancellationToken cancellationToken) =>
            (await my.SendMessageAsync(id, body ?? new MyGrievanceMessageRequest(null, null, null), cancellationToken)).ToOk())
            .RequireAuthorization(create);

        group.MapPost(
            "/my/{id:int}/attachments",
            async (int id, HttpRequest request, IMyGrievanceService my, CancellationToken cancellationToken) =>
            {
                var allowed = await my.PrecheckUploadAsync(id, cancellationToken);
                if (allowed.IsFailure)
                {
                    return Results.Problem(ApiProblems.From(allowed.Error));
                }

                var form = await EvidenceUploadReader.ReadFormAsync(request, extraField: null, cancellationToken);
                return form.IsFailure
                    ? Results.Problem(ApiProblems.From(form.Error))
                    : (await my.UploadAttachmentAsync(id, Upload(form.Value), cancellationToken))
                        .ToCreated(created => $"{Root}/my/attachments/{created.Attachment.Id}");
            })
            .RequireAuthorization(create)
            .RequireRateLimiting(GrievanceUploadRateLimit.Policy);

        group.MapPost("/my/{id:int}/seen", async (int id, IMyGrievanceService my, CancellationToken cancellationToken) =>
            (await my.MarkSeenAsync(id, cancellationToken)).ToNoContent())
            .RequireAuthorization(view);
    }

    /// <summary>
    /// The case-team reads. The lists need their tier's View; a case, its history and a file need any of the three Views (the scope is
    /// the union of the tiers held). The access log needs management View and the HR Administrator role.
    /// </summary>
    private static void MapCaseReads(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(Root).WithTags(Tag);

        group.MapGet(
            "/cases/query",
            async ([AsParameters] PageQuery page, [AsParameters] GrievanceCaseFilter filter, IGrievanceCaseReader cases, CancellationToken cancellationToken) =>
                (await cases.QueryAsync(page.ToRequest(filter), cancellationToken)).ToOk())
            .RequireAuthorization(Management.PolicyName(Permission.View));

        group.MapGet(
            "/investigations",
            async ([AsParameters] PageQuery page, [AsParameters] GrievanceCaseFilter filter, IGrievanceCaseReader cases, CancellationToken cancellationToken) =>
                (await cases.InvestigationsAsync(page.ToRequest(filter), cancellationToken)).ToOk())
            .RequireAuthorization(Investigation.PolicyName(Permission.View));

        group.MapGet(
            "/reviews/pending",
            async ([AsParameters] PageQuery page, [AsParameters] GrievanceCaseFilter filter, IGrievanceCaseReader cases, CancellationToken cancellationToken) =>
                (await cases.PendingReviewsAsync(page.ToRequest(filter), cancellationToken)).ToOk())
            .RequireAuthorization(Review.PolicyName(Permission.View));

        group.MapGet("/cases/{id:int}", async (int id, IGrievanceCaseReader cases, CancellationToken cancellationToken) =>
            (await cases.GetAsync(id, cancellationToken)).ToOk())
            .RequireAuthorization(AuthorizationSetup.GrievanceCaseTeamPolicy);

        group.MapGet("/cases/{id:int}/history", async (int id, IGrievanceCaseReader cases, CancellationToken cancellationToken) =>
            (await cases.HistoryAsync(id, cancellationToken)).ToOk())
            .RequireAuthorization(AuthorizationSetup.GrievanceCaseTeamPolicy);

        group.MapGet(
            "/cases/{id:int}/audit",
            async (int id, [AsParameters] PageQuery page, [AsParameters] GrievanceAuditFilter filter, IGrievanceCaseReader cases, CancellationToken cancellationToken) =>
                (await cases.AuditAsync(id, page.ToRequest(filter), cancellationToken)).ToOk())
            .RequireAuthorization(Management.PolicyName(Permission.View), AuthorizationSetup.AdministratorPolicy);

        group.MapGet(
            "/audit",
            async ([AsParameters] PageQuery page, [AsParameters] GrievanceAuditFilter filter, IGrievanceCaseReader cases, CancellationToken cancellationToken) =>
                (await cases.AuditLogAsync(page.ToRequest(filter), cancellationToken)).ToOk())
            .RequireAuthorization(Management.PolicyName(Permission.View), AuthorizationSetup.AdministratorPolicy);

        group.MapGet("/investigators", async ([FromQuery] string? search, IGrievanceCaseReader cases, CancellationToken cancellationToken) =>
            (await cases.InvestigatorsAsync(search, cancellationToken)).ToOk())
            .RequireAuthorization(Management.PolicyName(Permission.Edit));

        // The employee picker of triage (the subject) and of HR's intake (the employee): grievance managers need no employees:View for it.
        group.MapGet("/employees", async ([FromQuery] string? search, IGrievanceCaseReader cases, CancellationToken cancellationToken) =>
            (await cases.EmployeeOptionsAsync(search, cancellationToken)).ToOk())
            .RequireAuthorization(AuthorizationSetup.GrievanceEmployeePickerPolicy);

        group.MapGet("/attachments/{id:int}", async (int id, IGrievanceCaseReader cases, CancellationToken cancellationToken) =>
            (await cases.DownloadAttachmentAsync(id, cancellationToken)).ToAttachmentFile())
            .RequireAuthorization(AuthorizationSetup.GrievanceCaseTeamPolicy);
    }

    /// <summary>
    /// The 14 workflow commands, <c>POST /cases/{id}/{action}</c> with <c>{ rowVersion, ... }</c>; each answers with the case detail and
    /// its new row version. Management Edit, except: archive (management Delete); the decisions on a proposal (review Approve); the steps an
    /// assigned investigator may take too (case-work policy). Reopening a Closed case also needs review Approve (checked by the handler).
    /// </summary>
    private static void MapCommands(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"{Root}/cases").WithTags(Tag);
        var manage = Management.PolicyName(Permission.Edit);
        var work = AuthorizationSetup.GrievanceCaseWorkPolicy;
        var decide = Review.PolicyName(Permission.Approve);

        // HR records a grievance an employee raised outside the system: a submitted, named case (management Create).
        group.MapPost("", async ([FromBody] GrievanceIntakeRequest? body, IGrievanceIntake intake, CancellationToken cancellationToken) =>
            (await intake.RecordAsync(body ?? new GrievanceIntakeRequest(0, 0, null, null, null, null, null, null, null, false), cancellationToken))
                .ToCreated(created => $"{Root}/cases/{created.Id}"))
            .RequireAuthorization(Management.PolicyName(Permission.Create));

        Command<AcknowledgeBody, AcknowledgeGrievanceCommand>(group, "acknowledge", manage, (id, body) =>
            new AcknowledgeGrievanceCommand(id, body?.RowVersion, body?.Priority, body?.TargetResolutionDate, body?.Comment));
        Command<CommentBody, StartGrievanceReviewCommand>(group, "start-review", manage, (id, body) =>
            new StartGrievanceReviewCommand(id, body?.RowVersion, body?.Comment));
        Command<AssignBody, AssignGrievanceInvestigatorCommand>(group, "assign", manage, (id, body) =>
            new AssignGrievanceInvestigatorCommand(id, body?.RowVersion, body?.InvestigatorEmployeeId ?? 0, body?.Role, body?.Reason));
        Command<UnassignBody, UnassignGrievanceInvestigatorCommand>(group, "unassign", manage, (id, body) =>
            new UnassignGrievanceInvestigatorCommand(id, body?.RowVersion, body?.AssignmentId ?? 0, body?.Reason));
        Command<CommentBody, StartGrievanceInvestigationCommand>(group, "start-investigation", work, (id, body) =>
            new StartGrievanceInvestigationCommand(id, body?.RowVersion, body?.Comment));
        Command<RequestInfoBody, RequestGrievanceInfoCommand>(group, "request-info", work, (id, body) =>
            new RequestGrievanceInfoCommand(id, body?.RowVersion, body?.Message));
        Command<ProposeBody, ProposeGrievanceResolutionCommand>(group, "propose-resolution", work, (id, body) =>
            new ProposeGrievanceResolutionCommand(id, body?.RowVersion, body?.Summary));
        Command<ApproveBody, ApproveGrievanceResolutionCommand>(group, "approve-resolution", decide, (id, body) =>
            new ApproveGrievanceResolutionCommand(id, body?.RowVersion, body?.VisibleToSubmitter ?? false, body?.Comment));
        Command<ReasonBody, RejectGrievanceResolutionCommand>(group, "reject-resolution", decide, (id, body) =>
            new RejectGrievanceResolutionCommand(id, body?.RowVersion, body?.Reason));
        Command<ReasonBody, RejectGrievanceCommand>(group, "reject", manage, (id, body) =>
            new RejectGrievanceCommand(id, body?.RowVersion, body?.Reason));
        Command<CommentBody, CloseGrievanceCommand>(group, "close", manage, (id, body) =>
            new CloseGrievanceCommand(id, body?.RowVersion, body?.Comment));
        Command<ReasonBody, ReopenGrievanceCommand>(group, "reopen", manage, (id, body) =>
            new ReopenGrievanceCommand(id, body?.RowVersion, body?.Reason));
        Command<EscalateBody, EscalateGrievanceCommand>(group, "escalate", work, (id, body) =>
            new EscalateGrievanceCommand(id, body?.RowVersion, body?.Reason, body?.IsImmediate ?? false));
        Command<CommentBody, ArchiveGrievanceCommand>(group, "archive", Management.PolicyName(Permission.Delete), (id, body) =>
            new ArchiveGrievanceCommand(id, body?.RowVersion, body?.Comment));
    }

    /// <summary>Triage, messages, notes, files and corrective actions.</summary>
    private static void MapCaseWork(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(Root).WithTags(Tag);
        var manage = Management.PolicyName(Permission.Edit);

        group.MapPut(
            "/cases/{id:int}/triage",
            async (int id, [FromBody] TriageBody? body, ICommandHandler<TriageGrievanceCommand, GrievanceCaseDetailDto> handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(
                    new TriageGrievanceCommand(id, body?.RowVersion, body?.CategoryId ?? 0, body?.Priority, body?.TargetResolutionDate, body?.SubjectEmployeeId, body?.InvolvedDepartmentId, body?.LegalHold),
                    cancellationToken)).ToOk())
            .RequireAuthorization(manage);

        group.MapPost(
            "/cases/{id:int}/messages",
            async (int id, [FromBody] MessageBody? body, ICommandHandler<SendGrievanceCaseMessageCommand, GrievanceCaseDetailDto> handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new SendGrievanceCaseMessageCommand(id, body?.RowVersion, body?.Body), cancellationToken)).ToOk())
            .RequireAuthorization(AuthorizationSetup.GrievanceCaseWorkPolicy);

        group.MapPost(
            "/cases/{id:int}/notes",
            async (int id, [FromBody] NoteBody? body, ICommandHandler<AddGrievanceNoteCommand, GrievanceCaseDetailDto> handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new AddGrievanceNoteCommand(id, body?.RowVersion, body?.Kind, body?.Body, body?.InterviewDate, body?.IntervieweeText), cancellationToken)).ToOk())
            .RequireAuthorization(AuthorizationSetup.GrievanceCaseWorkPolicy);

        group.MapPost(
            "/cases/{id:int}/attachments",
            async (int id, HttpRequest request, IGrievanceCaseWork work, CancellationToken cancellationToken) =>
            {
                var allowed = await work.PrecheckUploadAsync(id, cancellationToken);
                if (allowed.IsFailure)
                {
                    return Results.Problem(ApiProblems.From(allowed.Error));
                }

                var form = await EvidenceUploadReader.ReadFormAsync(request, "visibility", cancellationToken);
                return form.IsFailure
                    ? Results.Problem(ApiProblems.From(form.Error))
                    : (await work.UploadAttachmentAsync(id, form.Value.Extra, Upload(form.Value), cancellationToken))
                        .ToCreated(created => $"{Root}/attachments/{created.Attachment.Id}");
            })
            .RequireAuthorization(AuthorizationSetup.GrievanceCaseWorkPolicy)
            .RequireRateLimiting(GrievanceUploadRateLimit.Policy);

        group.MapPost(
            "/cases/{id:int}/actions",
            async (int id, [FromBody] GrievanceCorrectiveActionCreateRequest? body, IGrievanceCaseWork work, CancellationToken cancellationToken) =>
                (await work.AddCorrectiveActionAsync(id, body ?? new GrievanceCorrectiveActionCreateRequest(null, null, null, null, null), cancellationToken))
                    .ToCreated(created => $"{Root}/case-actions/{created.Id}"))
            .RequireAuthorization(manage);

        group.MapPut(
            "/case-actions/{id:int}",
            async (int id, [FromBody] GrievanceCorrectiveActionUpdateRequest? body, IGrievanceCaseWork work, CancellationToken cancellationToken) =>
                (await work.UpdateCorrectiveActionAsync(id, body ?? new GrievanceCorrectiveActionUpdateRequest(null, null, null, null, null, null, null), cancellationToken)).ToOk())
            .RequireAuthorization(manage);
    }

    /// <summary>
    /// Dashboards and reports: aggregates over every counted case (not the case-team scope), the same for every viewer of a tier. The HR
    /// dashboard and the case report need management View, the review dashboard review View, the summary either.
    /// </summary>
    private static void MapInsights(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(Root).WithTags(Tag);

        group.MapGet("/dashboard/hr", async (IGrievanceInsights insights, CancellationToken cancellationToken) =>
            (await insights.HrDashboardAsync(cancellationToken)).ToOk())
            .RequireAuthorization(Management.PolicyName(Permission.View));

        group.MapGet("/dashboard/review", async (IGrievanceInsights insights, CancellationToken cancellationToken) =>
            (await insights.ReviewDashboardAsync(cancellationToken)).ToOk())
            .RequireAuthorization(Review.PolicyName(Permission.View));

        group.MapGet(
            "/reports/cases",
            async ([AsParameters] PageQuery page, [AsParameters] GrievanceReportFilter filter, IGrievanceInsights insights, CancellationToken cancellationToken) =>
                (await insights.CasesReportAsync(page.ToRequest(filter), cancellationToken)).ToOk())
            .RequireAuthorization(Management.PolicyName(Permission.View));

        group.MapGet(
            "/reports/summary",
            async ([AsParameters] GrievanceSummaryFilter filter, IGrievanceInsights insights, CancellationToken cancellationToken) =>
                (await insights.SummaryAsync(filter, cancellationToken)).ToOk())
            .RequireAuthorization(AuthorizationSetup.GrievanceInsightPolicy);
    }

    private static void Command<TBody, TCommand>(RouteGroupBuilder group, string action, string policy, Func<int, TBody?, TCommand> build)
        where TBody : class
        where TCommand : IEntityCommand =>
        group.MapPost(
            $"/{{id:int}}/{action}",
            async (int id, [FromBody] TBody? body, ICommandHandler<TCommand, GrievanceCaseDetailDto> handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(build(id, body), cancellationToken)).ToOk())
            .RequireAuthorization(policy);

    private static GrievanceFileUpload Upload(EvidenceUploadForm form) =>
        new(form.Upload.RowVersion, form.Upload.FileName, form.Upload.Content);

    // The JSON bodies of the case-team commands (camelCase), exactly the UI's request types.
    private sealed record CommentBody(string? RowVersion, string? Comment);

    private sealed record ReasonBody(string? RowVersion, string? Reason);

    private sealed record AcknowledgeBody(string? RowVersion, string? Priority, DateOnly? TargetResolutionDate, string? Comment);

    private sealed record AssignBody(string? RowVersion, int? InvestigatorEmployeeId, string? Role, string? Reason);

    private sealed record UnassignBody(string? RowVersion, int? AssignmentId, string? Reason);

    private sealed record RequestInfoBody(string? RowVersion, string? Message);

    private sealed record ProposeBody(string? RowVersion, string? Summary);

    private sealed record ApproveBody(string? RowVersion, bool? VisibleToSubmitter, string? Comment);

    private sealed record EscalateBody(string? RowVersion, string? Reason, bool? IsImmediate);

    private sealed record TriageBody(
        string? RowVersion,
        int? CategoryId,
        string? Priority,
        DateOnly? TargetResolutionDate,
        int? SubjectEmployeeId,
        int? InvolvedDepartmentId,
        bool? LegalHold);

    private sealed record MessageBody(string? RowVersion, string? Body);

    private sealed record NoteBody(string? RowVersion, string? Kind, string? Body, DateOnly? InterviewDate, string? IntervieweeText);
}
