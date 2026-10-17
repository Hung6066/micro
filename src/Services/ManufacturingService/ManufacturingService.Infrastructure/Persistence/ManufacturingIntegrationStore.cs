using His.Hope.Contracts.Manufacturing;
using His.Hope.ManufacturingService.Application.Ports;
using Microsoft.EntityFrameworkCore;

public sealed class ManufacturingIntegrationStore(
    IDbContextFactory<ManufacturingDbContext> dbFactory) : IManufacturingIntegrationStore
{
    public IReadOnlyList<EventReceiptDto> GetEventReceipts(string? eventType, int limit)
    {
        using var db = dbFactory.CreateDbContext();
        var query = db.EventReceipts.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(eventType))
            query = query.Where(x => x.EventType == eventType);

        return query.OrderByDescending(x => x.ReceivedAt)
            .Take(Math.Clamp(limit, 1, 100))
            .Select(x => new EventReceiptDto(x.Id, x.EventType, x.AggregateId, x.ReceivedAt))
            .ToList();
    }
}
