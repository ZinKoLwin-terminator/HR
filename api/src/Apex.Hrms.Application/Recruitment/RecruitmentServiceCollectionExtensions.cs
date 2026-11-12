using Apex.Hrms.Application.Employees.Blacklist;
using Apex.Hrms.Application.Recruitment.Applications;
using Apex.Hrms.Application.Recruitment.Candidates;
using Apex.Hrms.Application.Recruitment.Interviews;
using Apex.Hrms.Application.Recruitment.JobOpenings;
using Apex.Hrms.Application.Recruitment.Views;
using Apex.Hrms.Application.References;
using Apex.Hrms.Application.Security;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Organization;
using Apex.Hrms.Domain.Recruitment;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Apex.Hrms.Application.Recruitment;

internal static class RecruitmentServiceCollectionExtensions
{
    public static IServiceCollection AddRecruitment(this IServiceCollection services)
    {
        services.AddScoped<RecruitmentLookup>();
        services.AddScoped<IRecruitmentPipelineHandler, RecruitmentPipelineHandler>();
        services.AddScoped<IRecruitmentCountsHandler, RecruitmentCountsHandler>();

        return services
            .AddJobOpenings()
            .AddCandidates()
            .AddJobApplications()
            .AddInterviews()
            .AddRecruitmentReferences()
            .AddRecruitmentScopes()
            .AddBlacklist();
    }

    /// <summary>
    /// Openings are registered piece by piece: the shared reads and delete; their own create (it makes the code) and
    /// update (it reads the selected candidates); and the status moves as commands.
    /// </summary>
    private static IServiceCollection AddJobOpenings(this IServiceCollection services)
    {
        services.AddScoped<JobOpeningMapping>();
        services.AddScoped<IEntityDescriptor<JobOpening>>(provider => provider.GetRequiredService<JobOpeningMapping>());
        services.AddScoped<IDtoMapper<JobOpening, JobOpeningDto>>(provider => provider.GetRequiredService<JobOpeningMapping>());
        services.AddScoped<IQuerySpecification<JobOpening, JobOpeningFilter>, JobOpeningQuerySpecification>();
        services.TryAddScoped<EntityLoader<JobOpening>>();
        services.AddScoped<JobOpeningRules>();
        services.AddScoped<JobOpeningCodeGenerator>();

        services.AddScoped<IGetAllHandler<JobOpeningDto>, GetAllHandler<JobOpening, JobOpeningDto>>();
        services.AddScoped<IGetByIdHandler<JobOpeningDto>, GetByIdHandler<JobOpening, JobOpeningDto>>();
        services.AddScoped<IQueryHandler<JobOpeningDto, JobOpeningFilter>, QueryHandler<JobOpening, JobOpeningDto, JobOpeningFilter>>();
        services.AddScoped<ICreateHandler<JobOpeningCreateRequest, JobOpeningDto>, JobOpeningCreateHandler>();
        services.AddScoped<IUpdateHandler<JobOpeningUpdateRequest, JobOpeningDto>, JobOpeningUpdateHandler>();
        services.AddDelete<JobOpening, JobOpeningDto>();

        return services
            .AddJobOpeningStatus<OpenJobOpeningCommand>()
            .AddJobOpeningStatus<ReopenJobOpeningCommand>()
            .AddJobOpeningStatus<HoldJobOpeningCommand>()
            .AddJobOpeningStatus<CloseJobOpeningCommand>();
    }

    private static IServiceCollection AddJobOpeningStatus<TCommand>(this IServiceCollection services)
        where TCommand : IEntityCommand, IJobOpeningStatusCommand =>
        services.AddScoped<ICommandHandler<TCommand, JobOpeningDto>, JobOpeningStatusHandler<TCommand>>();

    /// <summary>Candidates have no status or code, so they are registered piece by piece, like holidays.</summary>
    private static IServiceCollection AddCandidates(this IServiceCollection services) => services
        .AddResource<Candidate, CandidateDetails, CandidateDto, CandidateFilter, CandidateMapping, CandidateQuerySpecification>()
        .AddCreate<Candidate, CandidateDetails, CandidateCreateRequest, CandidateDto, CandidateSaveRules>()
        .AddUpdate<Candidate, CandidateDetails, CandidateUpdateRequest, CandidateDto, CandidateSaveRules>()
        .AddDelete<Candidate, CandidateDto>();

    /// <summary>Applications: the reads are shared; create, update, delete and the stage move have their own flows.</summary>
    private static IServiceCollection AddJobApplications(this IServiceCollection services)
    {
        services.AddScoped<JobApplicationMapping>();
        services.AddScoped<IEntityDescriptor<JobApplication>>(provider => provider.GetRequiredService<JobApplicationMapping>());
        services.AddScoped<IDtoMapper<JobApplication, JobApplicationDto>>(provider => provider.GetRequiredService<JobApplicationMapping>());
        services.AddScoped<IQuerySpecification<JobApplication, JobApplicationFilter>, JobApplicationQuerySpecification>();
        services.TryAddScoped<EntityLoader<JobApplication>>();

        services.AddScoped<IGetAllHandler<JobApplicationDto>, GetAllHandler<JobApplication, JobApplicationDto>>();
        services.AddScoped<IGetByIdHandler<JobApplicationDto>, GetByIdHandler<JobApplication, JobApplicationDto>>();
        services.AddScoped<IQueryHandler<JobApplicationDto, JobApplicationFilter>, QueryHandler<JobApplication, JobApplicationDto, JobApplicationFilter>>();
        services.AddScoped<ICreateHandler<JobApplicationCreateRequest, JobApplicationDto>, JobApplicationCreateHandler>();
        services.AddScoped<IUpdateHandler<JobApplicationUpdateRequest, JobApplicationDto>, JobApplicationUpdateHandler>();
        services.AddScoped<IDeleteHandler<JobApplicationDto>, JobApplicationDeleteHandler>();
        services.AddScoped<ICommandHandler<MoveApplicationStageCommand, JobApplicationDto>, MoveApplicationStageHandler>();
        return services;
    }

    private static IServiceCollection AddInterviews(this IServiceCollection services)
    {
        services.AddScoped<InterviewMapping>();
        services.AddScoped<IEntityDescriptor<Interview>>(provider => provider.GetRequiredService<InterviewMapping>());
        services.AddScoped<IDtoMapper<Interview, InterviewDto>>(provider => provider.GetRequiredService<InterviewMapping>());
        services.AddScoped<IQuerySpecification<Interview, InterviewFilter>, InterviewQuerySpecification>();
        services.TryAddScoped<EntityLoader<Interview>>();
        services.AddScoped<InterviewRules>();

        services.AddScoped<IGetAllHandler<InterviewDto>, GetAllHandler<Interview, InterviewDto>>();
        services.AddScoped<IGetByIdHandler<InterviewDto>, GetByIdHandler<Interview, InterviewDto>>();
        services.AddScoped<IQueryHandler<InterviewDto, InterviewFilter>, QueryHandler<Interview, InterviewDto, InterviewFilter>>();
        services.AddScoped<ICreateHandler<InterviewCreateRequest, InterviewDto>, InterviewCreateHandler>();
        services.AddScoped<IUpdateHandler<InterviewUpdateRequest, InterviewDto>, InterviewUpdateHandler>();
        services.AddScoped<IDeleteHandler<InterviewDto>, InterviewDeleteHandler>();
        services.AddScoped<ICommandHandler<RecordInterviewOutcomeCommand, InterviewDto>, RecordInterviewOutcomeHandler>();
        services.AddScoped<ICommandHandler<CancelInterviewCommand, InterviewDto>, CancelInterviewHandler>();
        return services;
    }

    /// <summary>
    /// What recruitment records block: deleting an opening or a candidate (their applications), an application (its
    /// interviews), a department or an employee (openings they belong to or manage, interviews they conduct).
    /// </summary>
    private static IServiceCollection AddRecruitmentReferences(this IServiceCollection services) => services
        .AddReference<JobOpening, JobApplication>("application(s)", ReferenceUse.Delete, ownerId => application => application.JobOpeningId == ownerId)
        .AddReference<Candidate, JobApplication>("application(s)", ReferenceUse.Delete, ownerId => application => application.CandidateId == ownerId)
        .AddReference<JobApplication, Interview>("interview(s)", ReferenceUse.Delete, ownerId => interview => interview.ApplicationId == ownerId)
        .AddReference<Department, JobOpening>("job opening(s)", ReferenceUse.Delete, ownerId => opening => opening.DepartmentId == ownerId)
        .AddReference<Employee, JobOpening>("job opening(s)", ReferenceUse.Delete, ownerId => opening => opening.HiringManagerEmployeeId == ownerId)
        .AddReference<Employee, Interview>("interview(s)", ReferenceUse.Delete, ownerId => interview => interview.InterviewerEmployeeId == ownerId);
}
