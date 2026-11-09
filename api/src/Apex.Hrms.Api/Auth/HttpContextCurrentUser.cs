using System.Globalization;
using System.Security.Claims;
using Apex.Hrms.Application.Abstractions;

namespace Apex.Hrms.Api.Auth;

/// <summary>
/// The caller of the current request, read from the claims of the session cookie. Without a request or a signed-in
/// principal it behaves as anonymous (no user id), so audited writes are refused. It is never the system identity.
/// </summary>
internal sealed class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public const string AnonymousName = "Anonymous";

    private ClaimsPrincipal? Principal =>
        accessor.HttpContext?.User is { Identity.IsAuthenticated: true } user ? user : null;

    public bool IsAuthenticated => Principal is not null;

    public bool IsSystem => false;

    public int? UserId => IntClaim(HrmsClaims.AccountId);

    public int? EmployeeId => IntClaim(HrmsClaims.EmployeeId);

    public string DisplayName => Principal?.FindFirstValue(ClaimTypes.Name) is { Length: > 0 } name ? name : AnonymousName;

    private int? IntClaim(string type) =>
        int.TryParse(Principal?.FindFirstValue(type), NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : null;
}
