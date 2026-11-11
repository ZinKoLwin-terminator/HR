using Apex.Hrms.Api.Authorization;
using Apex.Hrms.Api.Http;
using Apex.Hrms.Application.Recruitment.Applications;
using Apex.Hrms.Application.Recruitment.Candidates;
using Apex.Hrms.Application.Recruitment.Interviews;
using Apex.Hrms.Application.Recruitment.JobOpenings;
using Apex.Hrms.Application.Recruitment.Views;
using Microsoft.AspNetCore.Mvc;

namespace Apex.Hrms.Api.Endpoints;

internal static class RecruitmentEndpoints
{
    private const string Tag = "Recruitment";

    private static readonly PermissionModule Module = PermissionModule.Recruitment;

    public static IEndpointRouteBuilder MapRecruitmentEndpoints(this IEndpointRouteBuilder app)
    {
        MapJobOpenings(app);
        app.MapCrud<CandidateDto, CandidateCreateRequest, CandidateUpdateRequest, CandidateFilter>("/api/candidates", Tag, Module);
        MapApplications(app);
        MapInterviews(app);
        MapViews(app);
        return app;
    }

    /// <summary>
    /// The status never changes through PUT: each move is a command. The code is made by the server. Filled has no
    /// command: it follows the number of selected candidates.
    /// </summary>
    private static void MapJobOpenings(IEndpointRouteBuilder app)
    {
        var group = app.MapCrud<JobOpeningDto, JobOpeningCreateRequest, JobOpeningUpdateRequest, JobOpeningFilter>("/api/job-openings", Tag, Module);
        group
            .MapCommand<JobOpeningDto, OpenJobOpeningCommand>("open", Module)
            .MapCommand<JobOpeningDto, ReopenJobOpeningCommand>("reopen", Module)
            .MapCommand<JobOpeningDto, HoldJobOpeningCommand>("hold", Module)
            .MapCommand<JobOpeningDto, CloseJobOpeningCommand>("close", Module);

        group.MapGet(
            "/{id:int}/pipeline",
            async (int id, IRecruitmentPipelineHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, cancellationToken)).ToOk())
            .RequireAuthorization(Module.PolicyName(Permission.View));
    }

    /// <summary>Moving a stage is an edit of the application: recruitment:Edit.</summary>
    private static void MapApplications(IEndpointRouteBuilder app) => app
        .MapCrud<JobApplicationDto, JobApplicationCreateRequest, JobApplicationUpdateRequest, JobApplicationFilter>("/api/applications", Tag, Module)
        .MapCommand<JobApplicationDto, StageBody, MoveApplicationStageCommand>(
            "stage",
            Module,
            (id, body) => new MoveApplicationStageCommand(id, body?.RowVersion, body?.Stage, body?.Reason));

    /// <summary>Recording an outcome and cancelling are edits of the interview: recruitment:Edit.</summary>
    private static void MapInterviews(IEndpointRouteBuilder app) => app
        .MapCrud<InterviewDto, InterviewCreateRequest, InterviewUpdateRequest, InterviewFilter>("/api/interviews", Tag, Module)
        .MapCommand<InterviewDto, OutcomeBody, RecordInterviewOutcomeCommand>(
            "outcome",
            Module,
            (id, body) => new RecordInterviewOutcomeCommand(id, body?.RowVersion, body?.Score, body?.Recommendation, body?.Feedback))
        .MapCommand<InterviewDto, ReasonBody, CancelInterviewCommand>(
            "cancel",
            Module,
            (id, body) => new CancelInterviewCommand(id, body?.RowVersion, body?.Reason));

    private static void MapViews(IEndpointRouteBuilder app)
    {
        var view = Module.PolicyName(Permission.View);

        app.MapGet(
            "/api/recruitment/pipeline",
            async ([FromQuery] int? jobOpeningId, IRecruitmentPipelineHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(jobOpeningId, cancellationToken)).ToOk())
            .WithTags(Tag)
            .RequireAuthorization(view);

        app.MapGet(
            "/api/recruitment/counts",
            async (IRecruitmentCountsHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(cancellationToken)).ToOk())
            .WithTags(Tag)
            .RequireAuthorization(view);
    }

    /// <summary>Body of the stage move: <c>{ "rowVersion": "...", "stage": "Screening", "reason": "..." }</c>. The reason is required to reject.</summary>
    private sealed record StageBody(string? RowVersion, string? Stage, string? Reason);

    /// <summary>Body of the outcome command: <c>{ "rowVersion": "...", "score": 4, "recommendation": "Hire", "feedback": "..." }</c>.</summary>
    private sealed record OutcomeBody(string? RowVersion, int? Score, string? Recommendation, string? Feedback);

    /// <summary>Body of the cancel command: <c>{ "rowVersion": "...", "reason": "..." }</c>; the reason is optional.</summary>
    private sealed record ReasonBody(string? RowVersion, string? Reason);
}
