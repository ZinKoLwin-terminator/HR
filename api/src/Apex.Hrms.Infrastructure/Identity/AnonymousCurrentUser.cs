using Apex.Hrms.Application.Abstractions;

namespace Apex.Hrms.Infrastructure.Identity;

/// <summary>
/// Placeholder until authentication is designed: nobody is identified, so audit user columns stay NULL.
/// This is NOT a security control. Replace it with an implementation backed by the authenticated principal
/// (and add server-side authorization) before any real data is stored.
/// </summary>
internal sealed class AnonymousCurrentUser : ICurrentUser
{
    public int? UserId => null;
}
