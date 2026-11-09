namespace Apex.Hrms.Application.Abstractions;

/// <summary>The caller on whose behalf a use case runs.</summary>
public interface ICurrentUser
{
    /// <summary>The <c>sec.User</c> id, or null when the caller is not identified.</summary>
    int? UserId { get; }
}
