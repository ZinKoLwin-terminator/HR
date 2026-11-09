namespace Apex.Hrms.Api.Auth;

/// <summary>
/// The claims of the session cookie. The name identifier claim holds the identity user id (auth.User.Id), the name claim
/// the display name and the email claim the address; these custom claims carry the business account.
/// Permissions are deliberately not in the cookie: they are read live so a role change applies at once.
/// </summary>
internal static class HrmsClaims
{
    /// <summary>The sec.AppUser id (the audit user id).</summary>
    public const string AccountId = "hrms:uid";

    /// <summary>The linked employee id; absent for the built-in administrator.</summary>
    public const string EmployeeId = "hrms:eid";

    /// <summary>The Identity security stamp at sign-in; a password reset changes it and so ends the session.</summary>
    public const string SecurityStamp = "hrms:stamp";

    /// <summary>The authentication property that holds the absolute end of a session cookie session (ISO 8601).</summary>
    public const string AbsoluteExpiryProperty = "hrms.absolute-expiry";

    public const string AuthenticationType = "hrms";
}
