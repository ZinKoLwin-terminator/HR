using Apex.Hrms.Mobile.Application.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.Mobile.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the mobile use cases. The ports they need (<c>IMobileSessionStore</c>, <c>IMobileEmployeeDirectory</c>,
    /// <c>IIdentityGateway</c>, <c>ITokenIssuer</c>, <c>SessionLifetimes</c>, <c>IAccountDirectory</c>, <c>TimeProvider</c>) come from
    /// the Infrastructure layer.
    /// </summary>
    public static IServiceCollection AddMobileApplication(this IServiceCollection services) =>
        services
            .AddScoped<MobileAccounts>()
            .AddScoped<TokenResponses>()
            .AddScoped<IUseCase<LoginCommand, MobileTokensDto>, LoginHandler>()
            .AddScoped<IUseCase<RefreshSessionCommand, MobileTokensDto>, RefreshSessionHandler>()
            .AddScoped<IUseCase<LogoutCommand, Done>, LogoutHandler>()
            .AddScoped<IUseCase<GetMeQuery, MobileMeDto>, GetMeHandler>()
            .AddScoped<IUseCase<ChangePasswordCommand, Done>, ChangePasswordHandler>()
            .AddScoped<IMobileAccessValidator, MobileAccessValidator>();
}
