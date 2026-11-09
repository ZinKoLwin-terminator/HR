using Apex.Hrms.Application.Abstractions;

namespace Apex.Hrms.Infrastructure.Identity;

/// <summary>
/// The explicit system identity of the bootstrap command and deployment seeds. It is the only caller allowed to
/// write audited rows without a user id. It is never registered by <c>AddInfrastructure</c> and the web host never
/// uses it: a tool opts in with <see cref="DependencyInjection.UseSystemIdentity"/>.
/// </summary>
internal sealed class SystemCurrentUser : ICurrentUser
{
    public int? UserId => null;

    public int? EmployeeId => null;

    public bool IsAuthenticated => false;

    public bool IsSystem => true;

    public string DisplayName => "System";
}
