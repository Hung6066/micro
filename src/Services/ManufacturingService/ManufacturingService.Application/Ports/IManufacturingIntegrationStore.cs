using His.Hope.Contracts.Manufacturing;

namespace His.Hope.ManufacturingService.Application.Ports;

public interface IManufacturingIntegrationStore
{
    IReadOnlyList<EventReceiptDto> GetEventReceipts(string? eventType, int limit);
}
