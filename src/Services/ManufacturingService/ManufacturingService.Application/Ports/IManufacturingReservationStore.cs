using His.Hope.Contracts.Manufacturing;

namespace His.Hope.ManufacturingService.Application.Ports;

public interface IManufacturingReservationStore
{
    (LotReservationDto? Reservation, string? Error) Reserve(string tenantKey, Guid lotId, CreateLotReservationRequest request);
    (LotReservationDto? Reservation, string? Error) Release(string tenantKey, Guid reservationId);
    IReadOnlyList<FefoLotDto> GetFefo(string tenantKey, string sku, int limit);
    (SalesAllocationDto Allocation, string? Error) AllocateSales(string tenantKey, string sku, CreateSalesAllocationRequest request);
}
