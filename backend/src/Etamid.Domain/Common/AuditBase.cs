using Etamid.SharedKernel;

namespace Etamid.Domain.Common;

public interface ICreationAudited
{
    DateTime CreatedAt { get; set; }
}

public interface IAuditable : ICreationAudited
{
    DateTime? UpdatedAt { get; set; }
}

public interface ISoftDeletable
{
    DateTime? DeletedAt { get; set; }
}

/// <summary>Append-only rows: only a creation timestamp.</summary>
public abstract class CreatedEntity<TId> : Entity<TId>, ICreationAudited where TId : notnull
{
    public DateTime CreatedAt { get; set; }
}

/// <summary>Aggregates with CreatedAt / UpdatedAt maintained by the DbContext interceptor.</summary>
public abstract class AuditableEntity<TId> : AggregateRoot<TId>, IAuditable where TId : notnull
{
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>Auditable + soft delete (a delete becomes DeletedAt = now, filtered out of queries).</summary>
public abstract class SoftDeletableEntity<TId> : AuditableEntity<TId>, ISoftDeletable where TId : notnull
{
    public DateTime? DeletedAt { get; set; }
}
