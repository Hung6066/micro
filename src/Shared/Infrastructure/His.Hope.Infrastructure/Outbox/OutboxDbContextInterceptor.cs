using System.Diagnostics;
using His.Hope.EventBus.Abstractions;
using His.Hope.SharedKernel.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Newtonsoft.Json;

namespace His.Hope.Infrastructure.Outbox;

public class OutboxDomainEventInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        SaveOutboxMessages(eventData);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        SaveOutboxMessages(eventData);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void SaveOutboxMessages(DbContextEventData eventData)
    {
        var context = eventData.Context;
        if (context is null) return;

        var entries = context.ChangeTracker
            .Entries()
            .Where(e => e.Entity is IHasDomainEvents entity && entity.DomainEvents.Count > 0)
            .Select(e => (IHasDomainEvents)e.Entity)
            .ToList();

        var domainEvents = entries
            .SelectMany(e =>
            {
                var events = e.DomainEvents.ToList();
                e.ClearDomainEvents();
                return events;
            })
            .ToList();

        if (domainEvents.Count == 0) return;

        var outboxMessages = domainEvents.Select(@event => new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = @event.GetType().FullName!,
            Content = JsonConvert.SerializeObject(@event, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            }),
            CorrelationId = Activity.Current?.Id,
            OccurredOn = DateTime.UtcNow,
            Status = OutboxStatus.Pending,
        }).ToList();

        context.Set<OutboxMessage>().AddRange(outboxMessages);
    }
}

public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}
