using MediatR;

namespace His.Hope.SharedKernel.Domain.Common;

public interface IDomainEvent : INotification
{
    DateTime OccurredOn { get; }
}
