namespace His.Hope.SharedKernel.Domain.Common;

public abstract class DomainEvent : IDomainEvent
{
    public DateTime OccurredOn { get; }

    protected DomainEvent() =>
        OccurredOn = DateTime.UtcNow;
}
