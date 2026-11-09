namespace Apex.Hrms.Application.Abstractions;

/// <summary>The caller on whose behalf a use case runs. The Api layer supplies the real implementation.</summary>
public interface ICurrentUser
{
    /// <summary>The <c>sec.User</c> id, or null when the caller is not identified.</summary>
    int? UserId { get; }

    /// <summary>
    /// The name kept in the employment history next to what the caller did ("recorded by"). TODO(Identity): the real
    /// implementation returns the display name of the signed-in user; the placeholder returns "System".
    /// </summary>
    string DisplayName { get; }
}
