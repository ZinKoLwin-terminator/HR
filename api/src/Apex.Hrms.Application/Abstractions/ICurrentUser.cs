namespace Apex.Hrms.Application.Abstractions;

/// <summary>The caller on whose behalf a use case runs. The Api layer supplies the real implementation.</summary>
public interface ICurrentUser
{
    /// <summary>The <c>sec.AppUser</c> id of the signed-in caller, or null when nobody is signed in.</summary>
    int? UserId { get; }

    /// <summary>The employee linked to the signed-in account; null for anonymous callers and the built-in administrator.</summary>
    int? EmployeeId { get; }

    /// <summary>True when a person has signed in. The system identity of a command line tool is not a signed-in person.</summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// True only for the explicit system identity used by the bootstrap command and deployment seeds: it may write
    /// audited rows without a user id (the audit user columns stay NULL). Never true for a web request.
    /// </summary>
    bool IsSystem { get; }

    /// <summary>
    /// The name kept in the employment history next to what the caller did ("recorded by"): the display name of the
    /// signed-in user, or a neutral label when nobody is signed in.
    /// </summary>
    string DisplayName { get; }
}
