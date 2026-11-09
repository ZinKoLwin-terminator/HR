using Apex.Hrms.Application.Abstractions;

namespace Apex.Hrms.Infrastructure.Identity;

/// <summary>
/// Placeholder until authentication is designed: nobody is identified, so audit user columns stay NULL.
/// It is registered with TryAdd, so the Api layer replaces it by registering its own <see cref="ICurrentUser"/>
/// (backed by the authenticated principal). This is NOT a security control; add server-side authorization
/// before any real data is stored.
/// </summary>
internal sealed class AnonymousCurrentUser : ICurrentUser
{
    public int? UserId => null;
}
