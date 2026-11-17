using Apex.Hrms.Application.Performance.Categories;
using Apex.Hrms.Application.Performance.Insights;
using Apex.Hrms.Application.Performance.Periods;
using Apex.Hrms.Application.Performance.RatingBands;
using Apex.Hrms.Application.Performance.Scorecards;
using Apex.Hrms.Application.Performance.Templates;
using Apex.Hrms.Application.References;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Organization;
using Apex.Hrms.Domain.Performance;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Apex.Hrms.Application.Performance;

internal static class PerformanceServiceCollectionExtensions
{
    public static IServiceCollection AddPerformance(this IServiceCollection services) => services
        .AddKpiCategories()
        .AddKpiTemplates()
        .AddEvaluationPeriods()
        .AddRatingBands()
        .AddScorecards()
        .AddKpiInsights()
        .AddPerformanceReferences();

    private static IServiceCollection AddKpiCategories(this IServiceCollection services) => services
        .AddCrudResource<KpiCategory, KpiCategoryDetails, KpiCategoryCreateRequest, KpiCategoryUpdateRequest, KpiCategoryDto, KpiCategoryFilter, KpiCategoryMapping, KpiCategoryRules, KpiCategoryQuerySpecification>();

    private static IServiceCollection AddKpiTemplates(this IServiceCollection services) => services
        .AddCrudResource<KpiTemplate, KpiTemplateDetails, KpiTemplateCreateRequest, KpiTemplateUpdateRequest, KpiTemplateDto, KpiTemplateFilter, KpiTemplateMapping, KpiTemplateRules, KpiTemplateQuerySpecification>();

    /// <summary>
    /// Periods have no code and no active flag: reads are shared; create and update run the shared flows inside a transaction that holds
    /// the company's period lock (<see cref="PeriodLock"/>, so the overlap check cannot race); delete has its own flow (Draft only, no
    /// scorecards); activate and complete are shared commands, and close has its own handler (refused while scorecards are in review).
    /// The period picker is a read of its own.
    /// </summary>
    private static IServiceCollection AddEvaluationPeriods(this IServiceCollection services)
    {
        services
            .AddResource<EvaluationPeriod, EvaluationPeriodDetails, EvaluationPeriodDto, EvaluationPeriodFilter, EvaluationPeriodMapping, EvaluationPeriodQuerySpecification>()
            .AddEntityCommand<EvaluationPeriod, ActivatePeriodCommand, EvaluationPeriodDto, ActivatePeriodAction>()
            .AddEntityCommand<EvaluationPeriod, CompletePeriodCommand, EvaluationPeriodDto, CompletePeriodAction>();
        services.TryAddScoped<ISaveRules<EvaluationPeriod, EvaluationPeriodDetails>, EvaluationPeriodRules>();
        services.AddScoped<CreateHandler<EvaluationPeriod, EvaluationPeriodCreateRequest, EvaluationPeriodDetails, EvaluationPeriodDto>>();
        services.AddScoped<UpdateHandler<EvaluationPeriod, EvaluationPeriodUpdateRequest, EvaluationPeriodDetails, EvaluationPeriodDto>>();
        services.AddScoped<ICreateHandler<EvaluationPeriodCreateRequest, EvaluationPeriodDto>, LockedPeriodCreateHandler>();
        services.AddScoped<IUpdateHandler<EvaluationPeriodUpdateRequest, EvaluationPeriodDto>, LockedPeriodUpdateHandler>();
        services.AddScoped<ICommandHandler<ClosePeriodCommand, EvaluationPeriodDto>, ClosePeriodHandler>();
        services.AddScoped<IDeleteHandler<EvaluationPeriodDto>, EvaluationPeriodDeleteHandler>();
        services.AddScoped<IEvaluationPeriodOptionsHandler, EvaluationPeriodOptionsHandler>();
        services.AddScoped<ICommandActivity<EvaluationPeriod, ActivatePeriodCommand>, PeriodMoveActivity<ActivatePeriodCommand>>();
        services.AddScoped<ICommandActivity<EvaluationPeriod, CompletePeriodCommand>, PeriodMoveActivity<CompletePeriodCommand>>();
        return services;
    }

    /// <summary>Bands have no code, so they are registered piece by piece: the shared flows and a status command pair of their own.</summary>
    private static IServiceCollection AddRatingBands(this IServiceCollection services) => services
        .AddResource<RatingBand, RatingBandDetails, RatingBandDto, RatingBandFilter, RatingBandMapping, RatingBandQuerySpecification>()
        .AddCreate<RatingBand, RatingBandDetails, RatingBandCreateRequest, RatingBandDto, RatingBandRules>()
        .AddUpdate<RatingBand, RatingBandDetails, RatingBandUpdateRequest, RatingBandDto, RatingBandRules>()
        .AddDelete<RatingBand, RatingBandDto>()
        .AddEntityCommand<RatingBand, ActivateCommand, RatingBandDto, ActivateRatingBandAction>()
        .AddEntityCommand<RatingBand, DeactivateCommand, RatingBandDto, DeactivateRatingBandAction>();

    /// <summary>
    /// Scorecards: the scoped reads (list and get), assigning KPIs one employee at a time or in bulk, changing and removing planned
    /// KPIs, deleting a plan and finalizing it. The access scope of scorecards (own, team or all) is registered with the other
    /// per-employee scopes (<c>OwnRecordScopes</c>); the HR writes are not row-scoped. The employee's results and submission and the
    /// reviewer's steps (start, review draft, approve, return, reject, HR finalize) run inside that scope and decide who may act themselves.
    /// </summary>
    private static IServiceCollection AddScorecards(this IServiceCollection services)
    {
        services.AddScoped<ScorecardReader>();
        services.AddScoped<IQueryHandler<ScorecardDto, ScorecardFilter>, ScorecardQueryHandler>();
        services.AddScoped<IGetByIdHandler<ScorecardDetailDto>, ScorecardGetHandler>();

        services.AddScoped<AssignmentLoader>();
        services.AddScoped<AssignmentWriter>();
        services.AddScoped<IAssignKpiHandler, AssignKpiHandler>();
        services.AddScoped<IBulkAssignKpiHandler, BulkAssignKpiHandler>();

        services.AddScoped<PlanLoader>();
        services.AddScoped<IUpdateHandler<ScorecardItemUpdateRequest, ScorecardItemDto>, ScorecardItemUpdateHandler>();
        services.AddScoped<IDeleteHandler<ScorecardItemDto>, ScorecardItemDeleteHandler>();
        services.AddScoped<IDeleteHandler<ScorecardDto>, ScorecardDeleteHandler>();
        services.AddScoped<ICommandHandler<FinalizePlanCommand, ScorecardDetailDto>, FinalizePlanHandler>();

        services.AddScoped<ScorecardSteps>();
        services.AddScoped<IUpdateHandler<SaveScorecardResultsRequest, ScorecardDetailDto>, SaveScorecardResultsHandler>();
        services.AddScoped<ICommandHandler<SubmitScorecardCommand, ScorecardDetailDto>, SubmitScorecardHandler>();
        services.AddScoped<ICommandHandler<StartScorecardReviewCommand, ScorecardDetailDto>, StartScorecardReviewHandler>();
        services.AddScoped<IUpdateHandler<SaveScorecardReviewRequest, ScorecardDetailDto>, SaveScorecardReviewHandler>();
        services.AddScoped<ICommandHandler<ApproveScorecardCommand, ScorecardDetailDto>, ApproveScorecardHandler>();
        services.AddScoped<ICommandHandler<ReturnScorecardCommand, ScorecardDetailDto>, ReturnScorecardHandler>();
        services.AddScoped<ICommandHandler<RejectScorecardCommand, ScorecardDetailDto>, RejectScorecardHandler>();
        services.AddScoped<ICommandHandler<FinalizeScorecardCommand, ScorecardDetailDto>, FinalizeScorecardHandler>();
        services.AddScoped<IUploadEvidenceHandler, UploadEvidenceHandler>();
        services.AddScoped<IRemoveEvidenceHandler, RemoveEvidenceHandler>();
        services.AddScoped<IDownloadEvidenceHandler, DownloadEvidenceHandler>();
        services.AddScoped<IMyScorecardsHandler, MyScorecardsHandler>();
        services.AddScoped<IPendingReviewsHandler, PendingReviewsHandler>();
        return services;
    }

    /// <summary>
    /// The read-only dashboards and reports. They read through <see cref="KpiReportSource"/>, which starts from the scorecard scope of
    /// the caller; the Excel exports of the same data are registered with the other Excel modules (<c>AddKpiExcel</c>).
    /// </summary>
    private static IServiceCollection AddKpiInsights(this IServiceCollection services) => services
        .AddScoped<KpiReportSource>()
        .AddScoped<IKpiEmployeeDashboardHandler, KpiEmployeeDashboardHandler>()
        .AddScoped<IKpiManagerDashboardHandler, KpiManagerDashboardHandler>()
        .AddScoped<IKpiHrDashboardHandler, KpiHrDashboardHandler>()
        .AddScoped<IKpiSummaryReportHandler, KpiSummaryReportHandler>()
        .AddScoped<IKpiEmployeeReportHandler, KpiEmployeeReportHandler>()
        .AddScoped<IKpiDepartmentReportHandler, KpiDepartmentReportHandler>()
        .AddScoped<IKpiRankingReportHandler, KpiRankingReportHandler>()
        .AddScoped<IKpiAchievementReportHandler, KpiAchievementReportHandler>()
        .AddScoped<IKpiTargetVsActualReportHandler, KpiTargetVsActualReportHandler>();

    /// <summary>
    /// What KPI records block. A category with templates (or assigned KPIs), a template with assigned KPIs, a period with
    /// scorecards, a company with periods, a department with templates, and an employee, company, branch, department or
    /// position that scorecards point at cannot be deleted (and a branch, department or position cannot move under another
    /// parent: the scorecard copies the placement with composite foreign keys).
    /// </summary>
    private static IServiceCollection AddPerformanceReferences(this IServiceCollection services) => services
        .AddReference<KpiCategory, KpiTemplate>("template(s)", ReferenceUse.Delete, ownerId => template => template.CategoryId == ownerId)
        .AddReference<KpiCategory, ScorecardItem>("assigned KPI(s)", ReferenceUse.Delete, ownerId => item => item.CategoryId == ownerId)
        .AddReference<KpiTemplate, ScorecardItem>("assigned KPI(s)", ReferenceUse.Delete, ownerId => item => item.TemplateId == ownerId)
        .AddReference<EvaluationPeriod, Scorecard>("scorecard(s)", ReferenceUse.Delete, ownerId => scorecard => scorecard.PeriodId == ownerId)
        .AddReference<Company, EvaluationPeriod>("evaluation period(s)", ReferenceUse.Delete, ownerId => period => period.CompanyId == ownerId)
        .AddReference<Department, KpiTemplate>("KPI template(s)", ReferenceUse.Delete, ownerId => template => template.DepartmentId == ownerId)
        .AddReference<Employee, Scorecard>("KPI scorecard(s)", ReferenceUse.Delete, ownerId => scorecard => scorecard.EmployeeId == ownerId || scorecard.ReviewerEmployeeId == ownerId)
        .AddReference<Employee, ReviewAction>("scorecard review step(s)", ReferenceUse.Delete, ownerId => action => action.ActorEmployeeId == ownerId)
        .AddReference<Company, Scorecard>("KPI scorecard(s)", ReferenceUse.Delete, ownerId => scorecard => scorecard.CompanyId == ownerId)
        .AddReference<Branch, Scorecard>("KPI scorecard(s)", ReferenceUse.Both, ownerId => scorecard => scorecard.BranchId == ownerId)
        .AddReference<Department, Scorecard>("KPI scorecard(s)", ReferenceUse.Both, ownerId => scorecard => scorecard.DepartmentId == ownerId)
        .AddReference<Position, Scorecard>("KPI scorecard(s)", ReferenceUse.Both, ownerId => scorecard => scorecard.PositionId == ownerId);
}
