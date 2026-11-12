using Apex.Hrms.Api.Authorization;
using Apex.Hrms.Api.Http;
using Apex.Hrms.Application.Payroll.Payslips;

namespace Apex.Hrms.Api.Endpoints;

/// <summary>
/// Employee self-service (<c>/api/me</c>): any signed-in user, no module permission. The handlers return only what belongs to
/// the caller's own employee record, so the policy is simply "signed in".
/// </summary>
internal static class SelfServiceEndpoints
{
    public static IEndpointRouteBuilder MapSelfServiceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/me/payslips").WithTags("Self service");

        group.MapGet(
            "/",
            async (int? pageIndex, int? pageSize, IMyPayslipsHandler handler, CancellationToken cancellationToken) =>
                (await handler.QueryAsync(pageIndex ?? 0, pageSize ?? 12, cancellationToken)).ToOk())
            .RequireAuthorization(AuthorizationSetup.SignedInPolicy);

        group.MapGet(
            "/{id:int}",
            async (int id, IMyPayslipsHandler handler, CancellationToken cancellationToken) =>
                (await handler.GetAsync(id, cancellationToken)).ToOk())
            .RequireAuthorization(AuthorizationSetup.SignedInPolicy);

        return app;
    }
}
