using Apex.Hrms.Application.Abstractions;

namespace Apex.Hrms.Infrastructure.Identity;

/// <summary>
/// The caller of code that runs without a request and without a signed-in person: nobody is identified, so audited
/// writes are rejected (see <see cref="Persistence.AuditStampInterceptor"/>). It is registered with TryAdd, so the Api
/// layer replaces it with its own <see cref="ICurrentUser"/> backed by the signed-in principal.
/// </summary>
internal sealed class AnonymousCurrentUser : ICurrentUser
{
    public int? UserId => null;

    public int? EmployeeId => null;

    public bool IsAuthenticated => false;

    public bool IsSystem => false;

    public string DisplayName => "Anonymous";
}
