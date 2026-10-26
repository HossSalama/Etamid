using Etamid.Application.Common.Interfaces;
using Etamid.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Etamid.Infrastructure.Persistence.Interceptors;

/// <summary>Stamps CreatedAt/UpdatedAt and turns deletes of soft-deletable entities into updates.</summary>
public sealed class AuditableEntitySaveChangesInterceptor(IClock clock) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null) return;
        var now = clock.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is ICreationAudited created && entry.State == EntityState.Added && created.CreatedAt == default)
                created.CreatedAt = now;

            if (entry.Entity is IAuditable audited && entry.State == EntityState.Modified)
                audited.UpdatedAt = now;

            if (entry.Entity is ISoftDeletable soft && entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                soft.DeletedAt = now;
            }
        }
    }
}
