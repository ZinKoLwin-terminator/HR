using System.Globalization;
using System.Security.Claims;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Mobile.Application.Abstractions;

namespace Apex.Hrms.MobileApi.Auth;

/// <summary>
/// The caller of the current request for the shared HRMS layers (audit stamping, scoping), read from the validated access
/// token. Without a signed-in principal it is anonymous, so audited writes are refused. It is never the system identity.
/// </summary>
internal sealed class MobileCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public const string AnonymousName = "Anonymous";

    private ClaimsPrincipal? Principal =>
        accessor.HttpContext?.User is { Identity.IsAuthenticated: true } user ? user : null;

    public bool IsAuthenticated => Principal is not null;

    public bool IsSystem => false;

    public int? UserId => IntClaim(MobileClaimTypes.AccountId);

    public int? EmployeeId => IntClaim(MobileClaimTypes.EmployeeId);

    public string DisplayName => Principal?.FindFirstValue(MobileClaimTypes.Name) is { Length: > 0 } name ? name : AnonymousName;

    private int? IntClaim(string type) =>
        int.TryParse(Principal?.FindFirstValue(type), NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : null;
}
