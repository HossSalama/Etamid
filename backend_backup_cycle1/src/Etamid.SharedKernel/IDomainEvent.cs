namespace Etamid.SharedKernel;

public interface IDomainEvent
{
    DateTime OccurredOnUtc { get; }
}
