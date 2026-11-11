using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Activity;
using Apex.Hrms.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Apex.Hrms.Infrastructure.Persistence;

/// <summary>Stamps CreatedAtUtc/ModifiedAtUtc and the acting user on every audited row that is saved.</summary>
internal sealed class AuditStampInterceptor(IClock clock, ICurrentUser currentUser) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = clock.UtcNow.UtcDateTime;
        var changed = context.ChangeTracker.Entries<AuditedEntity>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
            .ToList();

        // Every audited write needs a person to attribute it to. Only the explicit system identity (bootstrap command,
        // deployment seeds) may write without one; its audit user columns stay NULL.
        var logsActivity = context.ChangeTracker.Entries<ActivityLogEntry>().Any(entry => entry.State == EntityState.Added);
        if ((changed.Count > 0 || logsActivity) && !currentUser.IsSystem && currentUser.UserId is null)
        {
            throw new UnauthenticatedWriteException();
        }

        foreach (var entry in changed)
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    Set(entry, nameof(AuditedEntity.CreatedAtUtc), now);
                    Set(entry, nameof(AuditedEntity.CreatedByUserId), currentUser.UserId);
                    Set(entry, nameof(AuditedEntity.ModifiedAtUtc), now);
                    Set(entry, nameof(AuditedEntity.ModifiedByUserId), currentUser.UserId);
                    break;
                case EntityState.Modified:
                    Set(entry, nameof(AuditedEntity.ModifiedAtUtc), now);
                    Set(entry, nameof(AuditedEntity.ModifiedByUserId), currentUser.UserId);
                    break;
            }
        }
    }

    // The audit properties have private setters, so they are written through EF's property API.
    private static void Set(EntityEntry entry, string property, object? value) =>
        entry.Property(property).CurrentValue = value;
}
