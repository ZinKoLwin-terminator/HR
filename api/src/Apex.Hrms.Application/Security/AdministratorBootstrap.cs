using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Security;

namespace Apex.Hrms.Application.Security;

/// <summary>Persistence for <see cref="AdministratorBootstrap"/>.</summary>
public interface IAdministratorStore
{
    /// <summary>True when an Active account linked to an identity user holds the HR Administrator role.</summary>
    Task<bool> ActiveAdministratorExistsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Creates the identity user and the account (no employee, role HR Administrator) in one transaction, and checks
    /// again inside it that no administrator exists.
    /// </summary>
    Task<Result> CreateAdministratorAsync(string email, string displayName, string password, CancellationToken cancellationToken);
}

/// <summary>
/// Creates the very first administrator. Reachable only from the command line (<c>bootstrap-admin</c>), never over
/// HTTP, and refuses when an active linked administrator already exists.
/// </summary>
public sealed class AdministratorBootstrap(IAdministratorStore store)
{
    public const string AlreadyExistsMessage =
        "An active HR Administrator already exists. Create further accounts through the application.";

    public async Task<Result> RunAsync(string email, string displayName, string password, CancellationToken cancellationToken)
    {
        var fieldErrors = new Dictionary<string, string[]>();
        var cleanEmail = email?.Trim() ?? string.Empty;
        if (cleanEmail.Length == 0 || cleanEmail.Length > AppAccount.EmailMaxLength || !cleanEmail.Contains('@', StringComparison.Ordinal))
        {
            fieldErrors["email"] = [$"Enter an email address of at most {AppAccount.EmailMaxLength} characters."];
        }

        var cleanName = displayName?.Trim() ?? string.Empty;
        if (cleanName.Length == 0 || cleanName.Length > AppAccount.DisplayNameMaxLength)
        {
            fieldErrors["displayName"] = [$"Enter a name of at most {AppAccount.DisplayNameMaxLength} characters."];
        }

        if (SecurityPolicy.PasswordViolation(password) is { } passwordProblem)
        {
            fieldErrors["password"] = [passwordProblem];
        }

        if (fieldErrors.Count > 0)
        {
            return Result.Failure(AppError.Validation("The administrator details are not valid.", fieldErrors));
        }

        if (await store.ActiveAdministratorExistsAsync(cancellationToken))
        {
            return Result.Failure(AppError.Conflict(AlreadyExistsMessage));
        }

        return await store.CreateAdministratorAsync(cleanEmail, cleanName, password, cancellationToken);
    }
}
