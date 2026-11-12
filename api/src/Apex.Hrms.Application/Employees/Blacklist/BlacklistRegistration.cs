using Apex.Hrms.Application.Recruitment.Applications;
using Apex.Hrms.Application.Recruitment.Candidates;
using Apex.Hrms.Application.References;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Employees;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Apex.Hrms.Application.Employees.Blacklist;

internal static class BlacklistRegistration
{
    /// <summary>
    /// The do-not-rehire list, the matcher, the delete protection of employees with blacklist history, and the decorators that
    /// set <c>blacklistMatch</c> on candidate and application responses. Call after the recruitment handlers are registered.
    /// </summary>
    public static IServiceCollection AddBlacklist(this IServiceCollection services)
    {
        services.AddScoped<IEntityDescriptor<EmployeeBlacklistEntry>, BlacklistEntryDescriptor>();
        services.TryAddScoped<EntityLoader<EmployeeBlacklistEntry>>();
        services.AddScoped<IBlacklistHandler, BlacklistHandler>();
        services.AddScoped<IBlacklistMatcher, BlacklistMatcher>();
        services.AddScoped<BlacklistFlagging<CandidateDto>, CandidateFlagging>();
        services.AddScoped<BlacklistFlagging<JobApplicationDto>, ApplicationFlagging>();

        services
            .Decorate<IGetAllHandler<CandidateDto>>((inner, sp) => new FlaggedGetAllHandler<CandidateDto>(inner, sp.GetRequiredService<BlacklistFlagging<CandidateDto>>()))
            .Decorate<IGetByIdHandler<CandidateDto>>((inner, sp) => new FlaggedGetByIdHandler<CandidateDto>(inner, sp.GetRequiredService<BlacklistFlagging<CandidateDto>>()))
            .Decorate<IQueryHandler<CandidateDto, CandidateFilter>>((inner, sp) => new FlaggedQueryHandler<CandidateDto, CandidateFilter>(inner, sp.GetRequiredService<BlacklistFlagging<CandidateDto>>()))
            .Decorate<ICreateHandler<CandidateCreateRequest, CandidateDto>>((inner, sp) => new FlaggedCreateHandler<CandidateCreateRequest, CandidateDto>(inner, sp.GetRequiredService<BlacklistFlagging<CandidateDto>>()))
            .Decorate<IUpdateHandler<CandidateUpdateRequest, CandidateDto>>((inner, sp) => new FlaggedUpdateHandler<CandidateUpdateRequest, CandidateDto>(inner, sp.GetRequiredService<BlacklistFlagging<CandidateDto>>()))
            .Decorate<IGetAllHandler<JobApplicationDto>>((inner, sp) => new FlaggedGetAllHandler<JobApplicationDto>(inner, sp.GetRequiredService<BlacklistFlagging<JobApplicationDto>>()))
            .Decorate<IGetByIdHandler<JobApplicationDto>>((inner, sp) => new FlaggedGetByIdHandler<JobApplicationDto>(inner, sp.GetRequiredService<BlacklistFlagging<JobApplicationDto>>()))
            .Decorate<IQueryHandler<JobApplicationDto, JobApplicationFilter>>((inner, sp) => new FlaggedQueryHandler<JobApplicationDto, JobApplicationFilter>(inner, sp.GetRequiredService<BlacklistFlagging<JobApplicationDto>>()))
            .Decorate<ICreateHandler<JobApplicationCreateRequest, JobApplicationDto>>((inner, sp) => new FlaggedCreateHandler<JobApplicationCreateRequest, JobApplicationDto>(inner, sp.GetRequiredService<BlacklistFlagging<JobApplicationDto>>()))
            .Decorate<IUpdateHandler<JobApplicationUpdateRequest, JobApplicationDto>>((inner, sp) => new FlaggedUpdateHandler<JobApplicationUpdateRequest, JobApplicationDto>(inner, sp.GetRequiredService<BlacklistFlagging<JobApplicationDto>>()))
            .Decorate<ICommandHandler<MoveApplicationStageCommand, JobApplicationDto>>((inner, sp) => new FlaggedCommandHandler<MoveApplicationStageCommand, JobApplicationDto>(inner, sp.GetRequiredService<BlacklistFlagging<JobApplicationDto>>()));

        // An employee with blacklist history (active or lifted) cannot be deleted; the foreign key is the backstop.
        return services.AddReference<Employee, EmployeeBlacklistEntry>(
            "do-not-rehire entry(ies)",
            ReferenceUse.Delete,
            ownerId => entry => entry.EmployeeId == ownerId);
    }

    /// <summary>Wraps the last registration of <typeparamref name="TService"/> (it keeps its lifetime).</summary>
    private static IServiceCollection Decorate<TService>(this IServiceCollection services, Func<TService, IServiceProvider, TService> wrap)
        where TService : class
    {
        var original = services.Last(descriptor => descriptor.ServiceType == typeof(TService));
        services.Remove(original);
        services.Add(ServiceDescriptor.Describe(
            typeof(TService),
            provider => wrap(Create<TService>(provider, original), provider),
            original.Lifetime));
        return services;
    }

    private static TService Create<TService>(IServiceProvider provider, ServiceDescriptor descriptor)
        where TService : class =>
        descriptor switch
        {
            { ImplementationFactory: { } factory } => (TService)factory(provider),
            { ImplementationType: { } type } => (TService)ActivatorUtilities.CreateInstance(provider, type),
            { ImplementationInstance: { } instance } => (TService)instance,
            _ => throw new InvalidOperationException($"Cannot decorate {typeof(TService).Name}."),
        };
}
