namespace Apex.Hrms.Application.Abstractions;

/// <summary>
/// Answers whether the HRMS database can be reached right now. Used only by the readiness health check
/// (<c>GET /health/ready</c>); it never reads or writes data.
/// </summary>
public interface IDatabaseProbe
{
    /// <summary><see langword="true"/> when a connection to the database can be opened; never throws for an unreachable database.</summary>
    Task<bool> CanConnectAsync(CancellationToken cancellationToken);
}
