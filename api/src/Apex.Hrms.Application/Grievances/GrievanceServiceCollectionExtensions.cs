using Apex.Hrms.Application.Grievances.Categories;
using Apex.Hrms.Application.References;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Grievances;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Apex.Hrms.Application.Grievances;

internal static class GrievanceServiceCollectionExtensions
{
    /// <summary>
    /// The grievance module: categories (the shared CRUD flow), the submitter's service, the case-team reads, commands and case work, and
    /// what they share (access rules, redaction, the step pipeline, the access log, notifications, files, case numbers). The host provides
    /// <c>IGrievanceSubmitterDirectory</c>, <c>IGrievanceAuditLog</c>, <c>IGrievanceCaseNumberSequence</c>, <c>IGrievanceFileStorage</c>,
    /// <c>IPermissionHolders</c>, <c>INotificationWriter</c> and <c>INotificationRecipients</c>; <see cref="GrievanceSettings"/> defaults to
    /// a retention of 7 years unless the host registers its own. The dashboards and reports (<c>Insights</c>) read every counted case, not the case-team scope; the Excel export is registered with the other Excel modules.
    /// </summary>
    public static IServiceCollection AddGrievances(this IServiceCollection services)
    {
        services.AddCrudResource<GrievanceCategory, GrievanceCategoryDetails, GrievanceCategoryCreateRequest, GrievanceCategoryUpdateRequest, GrievanceCategoryDto, GrievanceCategoryFilter, GrievanceCategoryMapping, GrievanceCategoryRules, GrievanceCategoryQuerySpecification>();

        // Only the category reference is checked by name. Employees, departments, branches and positions that grievance rows point at are
        // NOT given a named reference: "used by grievance case(s)" would tell an employee or organization administrator that a person or a
        // department is involved in a grievance. The database foreign keys still refuse the delete, with the generic conflict message.
        services.AddReference<GrievanceCategory, GrievanceCase>("grievance case(s)", ReferenceUse.Delete, ownerId => grievance => grievance.CategoryId == ownerId);

        services.TryAddSingleton(new GrievanceSettings());
        services.TryAddSingleton<GrievanceUploadQuota>();
        services.AddScoped<GrievanceAccess>();
        services.AddScoped<GrievanceReader>();
        services.AddScoped<GrievanceAuditTrail>();
        services.AddScoped<GrievanceNotifications>();
        services.AddScoped<GrievanceFiles>();
        services.AddScoped<GrievanceCaseNumbers>();
        services.AddScoped<GrievanceCaseSteps>();

        services.AddScoped<IMyGrievanceService, MyGrievanceService>();
        services.AddScoped<IGrievanceCaseReader, GrievanceCaseReader>();
        services.AddScoped<IGrievanceCaseWork, GrievanceCaseWork>();
        services.AddScoped<IGrievanceIntake, GrievanceIntake>();

        services.AddScoped<Insights.GrievanceInsightSource>();
        services.AddScoped<Insights.GrievanceInsights>();
        services.AddScoped<Insights.IGrievanceInsights>(provider => provider.GetRequiredService<Insights.GrievanceInsights>());

        services.AddScoped<ICommandHandler<AcknowledgeGrievanceCommand, GrievanceCaseDetailDto>, AcknowledgeGrievanceHandler>();
        services.AddScoped<ICommandHandler<StartGrievanceReviewCommand, GrievanceCaseDetailDto>, StartGrievanceReviewHandler>();
        services.AddScoped<ICommandHandler<AssignGrievanceInvestigatorCommand, GrievanceCaseDetailDto>, AssignGrievanceInvestigatorHandler>();
        services.AddScoped<ICommandHandler<UnassignGrievanceInvestigatorCommand, GrievanceCaseDetailDto>, UnassignGrievanceInvestigatorHandler>();
        services.AddScoped<ICommandHandler<StartGrievanceInvestigationCommand, GrievanceCaseDetailDto>, StartGrievanceInvestigationHandler>();
        services.AddScoped<ICommandHandler<RequestGrievanceInfoCommand, GrievanceCaseDetailDto>, RequestGrievanceInfoHandler>();
        services.AddScoped<ICommandHandler<ProposeGrievanceResolutionCommand, GrievanceCaseDetailDto>, ProposeGrievanceResolutionHandler>();
        services.AddScoped<ICommandHandler<ApproveGrievanceResolutionCommand, GrievanceCaseDetailDto>, ApproveGrievanceResolutionHandler>();
        services.AddScoped<ICommandHandler<RejectGrievanceResolutionCommand, GrievanceCaseDetailDto>, RejectGrievanceResolutionHandler>();
        services.AddScoped<ICommandHandler<RejectGrievanceCommand, GrievanceCaseDetailDto>, RejectGrievanceHandler>();
        services.AddScoped<ICommandHandler<CloseGrievanceCommand, GrievanceCaseDetailDto>, CloseGrievanceHandler>();
        services.AddScoped<ICommandHandler<ReopenGrievanceCommand, GrievanceCaseDetailDto>, ReopenGrievanceHandler>();
        services.AddScoped<ICommandHandler<EscalateGrievanceCommand, GrievanceCaseDetailDto>, EscalateGrievanceHandler>();
        services.AddScoped<ICommandHandler<ArchiveGrievanceCommand, GrievanceCaseDetailDto>, ArchiveGrievanceHandler>();
        services.AddScoped<ICommandHandler<TriageGrievanceCommand, GrievanceCaseDetailDto>, TriageGrievanceHandler>();
        services.AddScoped<ICommandHandler<SendGrievanceCaseMessageCommand, GrievanceCaseDetailDto>, SendGrievanceCaseMessageHandler>();
        services.AddScoped<ICommandHandler<AddGrievanceNoteCommand, GrievanceCaseDetailDto>, AddGrievanceNoteHandler>();
        return services;
    }
}
