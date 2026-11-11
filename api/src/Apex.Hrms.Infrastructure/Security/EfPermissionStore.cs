using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Settings;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Security;
using Apex.Hrms.Infrastructure.Persistence;
using Apex.Hrms.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Apex.Hrms.Infrastructure.Security;

/// <summary>
/// The permission matrix in the database: modules and actions are the reference data of the post-deployment script, grants
/// are the rows of sec.RolePermission. Replacing the grants of a role touches the role row first, so its row version moves and
/// two administrators saving the same role cannot overwrite each other, and writes every View grant before the others
/// (the database requires the View grant of a module for every other grant of it).
/// </summary>
internal sealed class EfPermissionStore(HrmsDbContext context, ICurrentUser currentUser) : IPermissionStore
{
    private const string ViewAction = "View";

    public async Task<IReadOnlyList<PermissionModuleInfo>> ModulesAsync(CancellationToken cancellationToken)
    {
        var modules = await context.Set<PermissionModuleRow>().AsNoTracking()
            .OrderBy(module => module.SortOrder)
            .ToListAsync(cancellationToken);
        var actions = await context.Set<PermissionModuleActionRow>().AsNoTracking()
            .OrderBy(action => action.SortOrder)
            .ToListAsync(cancellationToken);
        return [.. modules.Select(module => new PermissionModuleInfo(
            module.ModuleKey,
            module.Label,
            [.. actions.Where(action => action.ModuleKey == module.ModuleKey).Select(action => action.Action)]))];
    }

    public async Task<IReadOnlyList<RoleGrant>> GrantsAsync(int? roleId, CancellationToken cancellationToken) =>
        await context.Set<RoleGrant>().AsNoTracking()
            .Where(grant => roleId == null || grant.RoleId == roleId)
            .ToListAsync(cancellationToken);

    public async Task<Result> ReplaceGrantsAsync(AppRole role, IReadOnlyCollection<(string ModuleKey, string Action)> grants, CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            // The role row is "changed" (audit stamp, new row version) and saved with the pending activity entry. The row
            // version the caller checked is the concurrency token of this update.
            context.Entry(role).Property(nameof(AuditedEntity.ModifiedAtUtc)).IsModified = true;
            await context.SaveChangesAsync(cancellationToken);

            var roleId = role.Id;
            await context.Set<RoleGrant>().Where(grant => grant.RoleId == roleId).ExecuteDeleteAsync(cancellationToken);

            await InsertAsync(roleId, grants.Where(grant => grant.Action == ViewAction), cancellationToken);
            await InsertAsync(roleId, grants.Where(grant => grant.Action != ViewAction), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }
        catch (UnauthenticatedWriteException)
        {
            return Result.Failure(AppError.Unauthorized(UnauthenticatedWriteException.PublicMessage));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(RowVersionToken.Stale);
        }
        catch (DbUpdateException exception) when (SqlConflicts.TryTranslate(exception, out var conflict))
        {
            return Result.Failure(conflict);
        }
    }

    private async Task InsertAsync(int roleId, IEnumerable<(string ModuleKey, string Action)> grants, CancellationToken cancellationToken)
    {
        var added = false;
        foreach (var (moduleKey, action) in grants)
        {
            var grant = RoleGrant.Create(roleId, moduleKey, action);
            context.Add(grant);
            context.Entry(grant).Property("CreatedByUserId").CurrentValue = currentUser.UserId;
            added = true;
        }

        if (added)
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
