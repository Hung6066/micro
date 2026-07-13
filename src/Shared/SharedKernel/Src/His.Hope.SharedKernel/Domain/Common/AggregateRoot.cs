namespace His.Hope.SharedKernel.Domain.Common;

public abstract class AggregateRoot<TId> : Entity<TId>, IAggregateRoot
    where TId : notnull
{
    protected AggregateRoot(TId id) : base(id) { }

    protected AggregateRoot() { }
}

public interface IAggregateRoot { }
