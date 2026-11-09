using System.Data;
using Apex.Hrms.Application.Security;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Security;
using Apex.Hrms.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Apex.Hrms.Infrastructure.Security;

/// <summary>
/// Creates auth.User and sec.AppUser in one transaction. The audited account row is written by the caller's identity:
/// the bootstrap command registers the system identity (see <see cref="DependencyInjection.UseSystemIdentity"/>), so
/// the audit user columns stay NULL.
/// </summary>
internal sealed class EfAdministratorStore(HrmsDbContext context, UserManager<IdentityUser> users) : IAdministratorStore
{
    public Task<bool> ActiveAdministratorExistsAsync(CancellationToken cancellationToken) => AdministratorExistsAsync(cancellationToken);

    public async Task<Result> CreateAdministratorAsync(string email, string displayName, string password, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        if (await AdministratorExistsAsync(cancellationToken))
        {
            return Result.Failure(AppError.Conflict(AdministratorBootstrap.AlreadyExistsMessage));
        }

        var role = await context.Set<AppRole>().AsNoTracking()
            .SingleOrDefaultAsync(r => r.Name == SecurityPolicy.AdministratorRoleName && r.Status == RecordStatus.Active, cancellationToken);
        if (role is null)
        {
            return Result.Failure(AppError.NotFound(
                $"The active role '{SecurityPolicy.AdministratorRoleName}' does not exist. Deploy the database project (post-deployment script) first."));
        }

        if (await context.Set<AppAccount>().AnyAsync(account => account.Email == email, cancellationToken))
        {
            return Result.Failure(AppError.Conflict("An account with this email address already exists."));
        }

        var user = new IdentityUser { UserName = email, Email = email };
        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            // Identity error descriptions never contain the password; they are safe to show.
            return Result.Failure(AppError.Validation(
                "The administrator could not be created.",
                created.Errors.GroupBy(error => FieldFor(error.Code))
                    .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray())));
        }

        context.Add(AppAccount.Create(employeeId: null, displayName, email, role.Id, user.Id));
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }

    private static string FieldFor(string code) =>
        code.StartsWith("Password", StringComparison.Ordinal) ? "password" : "email";

    private Task<bool> AdministratorExistsAsync(CancellationToken cancellationToken) =>
        (from account in context.Set<AppAccount>()
         join role in context.Set<AppRole>() on account.RoleId equals role.Id
         where account.Status == AccountStatus.Active
            && account.ExternalSubject != null
            && role.Name == SecurityPolicy.AdministratorRoleName
         select account.Id).AnyAsync(cancellationToken);
}
